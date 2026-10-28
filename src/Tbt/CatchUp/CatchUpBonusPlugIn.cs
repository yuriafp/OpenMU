// <copyright file="CatchUpBonusPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.CatchUp;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The catch-up bonus (PLANO-SERVIDOR.md, 5.2, item 7 of 9.5): a human whose account is behind the
/// most advanced human (see <see cref="HumanProgress"/>) gets more experience, growing with the
/// distance in resets. It's set when the character enters the world, which also happens after a
/// reset, since the reset logs the player out, and checked again every minute, e.g. for a reset of
/// the most advanced human.
/// </summary>
/// <remarks>
/// <para>
/// An account counts with its most advanced character, like the most advanced human does, so the
/// alts of the most advanced human get nothing, and the alts of who is behind get the bonus, too.
/// </para>
/// <para>
/// Like the Happy Hour, it multiplies the experience rate of the player, so both stack (2x with +50%
/// is 3x), and it also counts for the experience of a party, which OpenMU takes from the experience
/// rate of each member. Bots get nothing. Every change is written to the registro.
/// </para>
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Bônus de alcance", Description = "Gives more experience to the humans who are behind the most advanced human of the accounts which played recently, growing with the distance in resets.")]
[Guid("F8D6ABF0-7579-46C5-8DBA-1FEF78DA57FE")]
public class CatchUpBonusPlugIn : IPlayerStateChangedPlugIn, IPeriodicTaskPlugIn, ISupportCustomConfiguration<CatchUpBonusConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly ExperienceBonuses _bonuses = new(typeof(CatchUpBonusPlugIn));

    private readonly ConcurrentDictionary<GameContext, PeriodicCheck> _checks = new();

    private ILogger? _logger;

    /// <inheritdoc />
    public CatchUpBonusConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CatchUpBonusConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState))
            {
                this._bonuses.Detach(player);
                return;
            }

            if (!TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) || !player.IsHuman() || this._bonuses.Attach(player) is not { } bonus)
            {
                return;
            }

            // The multiplier starts neutral, and gets its value when the most advanced human is known.
            this._bonuses.SubscribeToDeactivation(player.GameContext.PlugInManager);
            var configuration = this.Configuration ??= new CatchUpBonusConfiguration();
            var leaderResets = await HumanProgress.GetMaximumResetsAsync(configuration.ActiveDays, configuration.GetExcludedAccounts(), this.GetLogger(player.GameContext)).ConfigureAwait(false);
            if (this._bonuses.IsAttached(player, bonus))
            {
                // Otherwise the player left during the read.
                await UpdateAsync(player, bonus, leaderResets, configuration).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would keep the other plugins
            // of the state change, like the registro, from running.
            player.Logger.LogWarning(ex, "Couldn't set the catch-up bonus of {Character}.", player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var check = this._checks.GetOrAdd(gameContext, _ => new PeriodicCheck());
        if (!check.TryStart(DateTime.UtcNow, CheckInterval))
        {
            return;
        }

        try
        {
            this._bonuses.SubscribeToDeactivation(gameContext.PlugInManager);
            var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
            this._bonuses.DetachLeftPlayers(gameContext, players);
            var humans = players.Where(player => player.IsHuman() && player.IsInWorld()).ToList();
            if (humans.Count == 0)
            {
                return;
            }

            var configuration = this.Configuration ??= new CatchUpBonusConfiguration();
            var leaderResets = await HumanProgress.GetMaximumResetsAsync(configuration.ActiveDays, configuration.GetExcludedAccounts(), this.GetLogger(gameContext)).ConfigureAwait(false);
            foreach (var player in humans)
            {
                try
                {
                    // It also attaches the bonus of who entered while the plugin was off.
                    if (this._bonuses.Attach(player) is { } bonus)
                    {
                        await UpdateAsync(player, bonus, leaderResets, configuration).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    player.Logger.LogWarning(ex, "Couldn't update the catch-up bonus of {Character}.", player.Name);
                }
            }
        }
        catch (Exception ex)
        {
            this.GetLogger(gameContext).LogWarning(ex, "Couldn't check the catch-up bonuses.");
        }
        finally
        {
            check.End();
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        foreach (var check in this._checks.Values)
        {
            check.ForceStart();
        }
    }

    /// <summary>
    /// Gets the bonus of an account: the distance in resets to the most advanced human times the
    /// percent per reset, up to the maximum.
    /// </summary>
    /// <param name="resets">The resets of the account, i.e. of its most advanced character.</param>
    /// <param name="leaderResets">The resets of the most advanced human.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The bonus, in percent.</returns>
    internal static int GetPercent(int resets, int leaderResets, CatchUpBonusConfiguration configuration)
    {
        var distance = leaderResets - resets;
        if (distance <= 0)
        {
            return 0;
        }

        // The ranges of the admin panel don't protect a configuration which was changed elsewhere.
        return (int)Math.Max(0, Math.Min((long)distance * configuration.PercentPerReset, configuration.MaximumPercent));
    }

    /// <summary>
    /// Formats the message of a bonus. A message which the admin panel left broken falls back to the
    /// default one, so the player still learns about the bonus.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="percent">The bonus, in percent.</param>
    /// <param name="leaderResets">The resets of the most advanced human.</param>
    /// <returns>The message, or <c>null</c> when the configuration has none.</returns>
    internal static string? FormatMessage(CatchUpBonusConfiguration configuration, int percent, int leaderResets)
    {
        if (string.IsNullOrWhiteSpace(configuration.Message))
        {
            return null;
        }

        try
        {
            return string.Format(configuration.Message, percent, leaderResets);
        }
        catch (FormatException)
        {
            return string.Format(new CatchUpBonusConfiguration().Message, percent, leaderResets);
        }
    }

    private static async ValueTask UpdateAsync(Player player, ExperienceBonus bonus, int? leaderResets, CatchUpBonusConfiguration configuration)
    {
        // While the most advanced human is unknown, e.g. when the database fails, the bonus stays as it is.
        if (leaderResets is not { } leader)
        {
            return;
        }

        var resets = GetAccountResets(player, bonus.Attributes);
        var percent = GetPercent(resets, leader, configuration);
        if (!bonus.TrySetPercent(percent))
        {
            return;
        }

        ActivityLog.Write("catch_up", player, new { percent, resets, leader });
        try
        {
            var message = percent > 0 ? FormatMessage(configuration, percent, leader) : configuration.EndMessage;
            if (!string.IsNullOrWhiteSpace(message))
            {
                await player.ShowMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't tell {Character} about the catch-up bonus.", player.Name);
        }
    }

    /// <summary>
    /// Gets the resets of the most advanced character of the account of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="attributes">The attributes of the character in the world.</param>
    /// <returns>The resets.</returns>
    private static int GetAccountResets(Player player, AttributeSystem attributes)
    {
        var resets = (int)attributes[Stats.Resets];
        foreach (var character in player.Account?.Characters ?? [])
        {
            var value = character.Attributes.FirstOrDefault(a => a.Definition == Stats.Resets)?.Value ?? 0;
            resets = Math.Max(resets, (int)value);
        }

        return resets;
    }

    private ILogger GetLogger(IGameContext gameContext)
        => this._logger ??= gameContext.LoggerFactory.CreateLogger(this.GetType().Name);
}
