// <copyright file="WeeklyGoalPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The collective goal of the week (PLANO-SERVIDOR.md, 5.5, item 14 of 9.5): one goal per week, in
/// turn, like "20 Blood Castles completos", counted from the registro for all humans together. Every
/// account which counted at least one that week gets a reward, also when it counts after the goal is
/// reached, and from then until the week turns all humans in the world get more experience.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>An account counts with all its characters, and gets the reward once per week: the character
/// in the world when the goal is reached gets it, or the next one which enters, also in a later week
/// (up to <see cref="PendingRewardWeeks"/> weeks).</item>
/// <item>A reward which doesn't fit into the inventory, or fails, is undone and tried again after a
/// while, like the milestones.</item>
/// <item>The messages to all go to the players of each game server, which checks the progress every
/// minute: the new goal when the week turns, the steps of the progress and the reached goal. A check
/// right after a start only learns the progress, so a restart doesn't repeat a message.</item>
/// <item><c>/meta</c> opens the window of the goal in our client (<see cref="WeeklyGoalRef"/>).</item>
/// <item>Bots don't count, and get neither reward nor bonus. The registro counts what it wrote, a few
/// seconds late, and nothing while it's off.</item>
/// </list>
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Meta da semana", Description = "A collective goal per week, in turn, counted from the registro for the humans: every account which counted gets a reward, and once it's reached, all humans get more experience until the week turns.")]
[Guid("C3E7A2B5-1D84-4F69-9A0B-6E5D2F8C4A17")]
public class WeeklyGoalPlugIn : IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<WeeklyGoalConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// A whole goal, in percent.
    /// </summary>
    internal const int FullPercent = 100;

    /// <summary>
    /// The weeks, the current one included, whose rewards an account still gets when it enters. Not
    /// configuration: a month covers a vacation, and the reached goals stay in the database for a GM
    /// to give an older one.
    /// </summary>
    private const int PendingRewardWeeks = 4;

    /// <summary>
    /// How often the goal is counted. Not configuration: the announcements, the bonus and the rewards
    /// only need a minute's precision, and each check is a query on the registro.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a reward which didn't fit into the inventory waits for the next attempt. Not
    /// configuration: it only keeps the message about the full inventory from coming every minute.
    /// </summary>
    private static readonly TimeSpan RewardRetryDelay = TimeSpan.FromMinutes(5);

    private static readonly WeeklyGoalConfiguration Defaults = WeeklyGoalConfiguration.CreateDefault();

    private readonly ExperienceBonuses _bonuses = new(typeof(WeeklyGoalPlugIn));

    private readonly ConcurrentDictionary<GameContext, ContextState> _contexts = new();

    private readonly ConcurrentDictionary<(WeeklyGoalWeek Week, Guid AccountId), byte> _rewarding = new();

    private readonly ConcurrentDictionary<Player, DateTime> _nextRewardAttempts = new();

    private ILogger? _logger;

    /// <inheritdoc />
    public WeeklyGoalConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => WeeklyGoalConfiguration.CreateDefault();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState))
            {
                this._bonuses.Detach(player);
                this._nextRewardAttempts.TryRemove(player, out _);
                return;
            }

            if (!TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) || !player.IsHuman() || player.Account is not { } account)
            {
                return;
            }

            this._bonuses.SubscribeToDeactivation(player.GameContext.PlugInManager);
            var configuration = this.GetConfiguration();
            var now = DateTime.UtcNow;
            if (await GetProgressAsync(now, configuration).ConfigureAwait(false) is { } progress)
            {
                await this.ShowEntryAsync(player, account, progress, configuration).ConfigureAwait(false);
            }

            await this.GivePendingRewardsAsync(player, account, WeeklyGoalWeek.Of(now), configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the entry.
            player.Logger.LogWarning(ex, "Couldn't check the weekly goal of {Character}.", player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var state = this._contexts.GetOrAdd(gameContext, _ => new ContextState());
        if (!state.Check.TryStart(DateTime.UtcNow, CheckInterval))
        {
            return;
        }

        try
        {
            this._bonuses.SubscribeToDeactivation(gameContext.PlugInManager);
            var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
            this._bonuses.DetachLeftPlayers(gameContext, players);
            var configuration = this.GetConfiguration();
            var now = DateTime.UtcNow;
            var progress = await GetProgressAsync(now, configuration).ConfigureAwait(false);
            await this.AnnounceAsync(gameContext, state, progress, configuration).ConfigureAwait(false);
            await this.RetryRewardsAsync(gameContext, WeeklyGoalWeek.Of(now), configuration).ConfigureAwait(false);
            if (progress is not { IsReached: true })
            {
                // The week turned, or the goal changed in the admin panel.
                this._bonuses.DetachAll(gameContext);
                return;
            }

            await this.RewardPlayersAsync(players, progress, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.GetLogger(gameContext).LogWarning(ex, "Couldn't check the weekly goal.");
        }
        finally
        {
            state.Check.End();
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        foreach (var state in this._contexts.Values)
        {
            state.Check.ForceStart();
        }
    }

    /// <summary>
    /// Chooses the goal of the week: the goals take turns, one per week.
    /// </summary>
    /// <param name="goals">The goals.</param>
    /// <param name="week">The week.</param>
    /// <returns>The goal, or <c>null</c> without goals.</returns>
    internal static WeeklyGoalDefinition? ChooseGoal(IList<WeeklyGoalDefinition> goals, WeeklyGoalWeek week)
    {
        // The ranges of the admin panel don't protect a configuration which was changed elsewhere.
        var valid = goals.Where(goal => goal.Target > 0).ToList();
        return valid.Count == 0 ? null : valid[week.Number % valid.Count];
    }

    /// <summary>
    /// Gets the items of the reward.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The items.</returns>
    internal static IReadOnlyList<RewardItem> GetRewardItems(WeeklyGoalConfiguration configuration)
    {
        var items = new List<RewardItem>();
        if (configuration.JewelsOfBless > 0)
        {
            items.Add(Of(ItemConstants.JewelOfBless, configuration.JewelsOfBless));
        }

        if (configuration.JewelsOfSoul > 0)
        {
            items.Add(Of(ItemConstants.JewelOfSoul, configuration.JewelsOfSoul));
        }

        if (configuration.BoxOfKundunPlus is > 0 and <= BoxOfKundun.HighestPlus)
        {
            items.Add(new RewardItem(BoxOfKundun.Group, BoxOfKundun.Number, BoxOfKundun.LevelOf(configuration.BoxOfKundunPlus)));
        }

        return items;
    }

    /// <summary>
    /// Gets the reward as shown to the players, e.g. "1 Jewel of Bless, 1 Box of Kundun +3 e 100 Goblin Points".
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The reward, or an empty text without one.</returns>
    internal static string GetRewardText(WeeklyGoalConfiguration configuration)
    {
        var parts = new List<string>();
        if (configuration.JewelsOfBless > 0)
        {
            parts.Add($"{configuration.JewelsOfBless} Jewel of Bless");
        }

        if (configuration.JewelsOfSoul > 0)
        {
            parts.Add($"{configuration.JewelsOfSoul} Jewel of Soul");
        }

        if (configuration.BoxOfKundunPlus is > 0 and <= BoxOfKundun.HighestPlus)
        {
            parts.Add($"1 {BoxOfKundun.NameOf(configuration.BoxOfKundunPlus)}");
        }

        if (configuration.GoblinPoints > 0)
        {
            parts.Add($"{configuration.GoblinPoints} Goblin Points");
        }

        return parts.Count <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts.Take(parts.Count - 1))} e {parts[^1]}";
    }

    /// <summary>
    /// Formats a message. A message which the admin panel left broken falls back to the default one,
    /// so the player still learns about the goal.
    /// </summary>
    /// <param name="message">The configured message.</param>
    /// <param name="defaultMessage">The default message.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The message, or <c>null</c> when the configuration has none.</returns>
    internal static string? Format(string? message, string defaultMessage, params object?[] arguments)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, message, arguments);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, defaultMessage, arguments);
        }
    }

    /// <summary>
    /// Writes the packet of the window of the goal.
    /// </summary>
    /// <param name="span">The span to write into, at least <see cref="WeeklyGoalRef.Length"/> long.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="contribution">What the account of the player counted.</param>
    /// <param name="rewarded">Whether the account of the player got the reward.</param>
    /// <param name="configuration">The configuration, with the bonus and the reward.</param>
    /// <returns>The length of the packet.</returns>
    internal static int WritePacket(Span<byte> span, WeeklyGoalProgress progress, int contribution, bool rewarded, WeeklyGoalConfiguration configuration)
    {
        span = span[..WeeklyGoalRef.Length];
        span.Clear();
        var packet = new WeeklyGoalRef(span);
        packet.Flags = WeeklyGoalRef.WeeklyGoalFlags.HasGoal
            | (progress.IsReached ? WeeklyGoalRef.WeeklyGoalFlags.Reached : WeeklyGoalRef.WeeklyGoalFlags.None)
            | (rewarded ? WeeklyGoalRef.WeeklyGoalFlags.Rewarded : WeeklyGoalRef.WeeklyGoalFlags.None);
        packet.ExperienceBonusPercent = (byte)Math.Clamp(configuration.ExperienceBonusPercent, 0, byte.MaxValue);
        packet.Kind = progress.Goal.Kind;
        packet.BoxOfKundunPlus = configuration.BoxOfKundunPlus is > 0 and <= BoxOfKundun.HighestPlus ? (byte)configuration.BoxOfKundunPlus : (byte)0;
        packet.Count = progress.Count;
        packet.Target = progress.Goal.Target;
        packet.Contribution = contribution;
        packet.WeekEnd = (int)new DateTimeOffset(progress.Week.End).ToUnixTimeSeconds();
        packet.JewelsOfBless = Math.Max(configuration.JewelsOfBless, 0);
        packet.JewelsOfSoul = Math.Max(configuration.JewelsOfSoul, 0);
        packet.GoblinPoints = Math.Max(configuration.GoblinPoints, 0);
        packet.Name = progress.Goal.Name;
        return WeeklyGoalRef.Length;
    }

    /// <summary>
    /// Shows the goal of the week to the player, for <c>/meta</c>: the window of our client, or a
    /// message in a week without a goal.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    internal async ValueTask ShowAsync(Player player)
    {
        var configuration = this.GetConfiguration();
        if (player.Account is not { } account || await GetProgressAsync(DateTime.UtcNow, configuration).ConfigureAwait(false) is not { } progress)
        {
            if (Format(configuration.NoGoalMessage, Defaults.NoGoalMessage) is { } message)
            {
                await player.ShowMessageAsync(message).ConfigureAwait(false);
            }

            return;
        }

        if (player.GetConnection() is not { } connection)
        {
            return;
        }

        var accountId = account.GetId();
        var rewarded = progress.IsReached && (await WeeklyGoalStore.GetRewardedAccountsAsync(progress.Week).ConfigureAwait(false)).Contains(accountId);
        await connection.SendAsync(() => WritePacket(connection.Output.GetSpan(WeeklyGoalRef.Length)[..WeeklyGoalRef.Length], progress, progress.Of(accountId), rewarded, configuration)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the progress of the goal of the week, and records it as reached when the count got there.
    /// </summary>
    private static async ValueTask<WeeklyGoalProgress?> GetProgressAsync(DateTime utcNow, WeeklyGoalConfiguration configuration)
    {
        var week = WeeklyGoalWeek.Of(utcNow);

        // A reached goal stays the goal of its week, also when the goals change in the admin panel.
        var reached = await WeeklyGoalStore.GetReachedAsync(week).ConfigureAwait(false);
        if ((reached?.Goal ?? ChooseGoal(configuration.Goals, week)) is not { } goal)
        {
            return null;
        }

        var contributions = await WeeklyGoalStore.GetContributionsAsync(goal.Kind, week).ConfigureAwait(false);
        var progress = new WeeklyGoalProgress(week, goal, contributions, reached?.ReachedAt);
        if (!progress.IsReached && progress.Count >= goal.Target
            && await WeeklyGoalStore.RecordReachedAsync(week, goal).ConfigureAwait(false) is { } recorded)
        {
            // Another game server may have recorded it first, with the goals it had then.
            if (recorded.Goal.Kind != goal.Kind)
            {
                contributions = await WeeklyGoalStore.GetContributionsAsync(recorded.Goal.Kind, week).ConfigureAwait(false);
            }

            progress = new WeeklyGoalProgress(week, recorded.Goal, contributions, recorded.ReachedAt);
        }

        return progress;
    }

    private static RewardItem Of(ItemIdentifier item, int count)
        => new(item.Group, item.Number ?? throw new ArgumentException("The item needs a number.", nameof(item)), 0, count);

    /// <summary>
    /// Puts the reward into the inventory and the Goblin Points onto the account, all or none, under
    /// the persistence lock of the player, like its packet handlers.
    /// </summary>
    /// <returns><c>false</c>, if a trade or mix is open, or the items don't fit into the inventory.</returns>
    private static async ValueTask<bool> GiveAsync(Player player, Account account, IReadOnlyList<RewardItem> items, WeeklyGoalConfiguration configuration)
    {
        return await player.RunPersistenceExclusiveAsync(async () =>
        {
            // An open mix or trade keeps a backup of the inventory, which a disconnect restores
            // without the reward; so the reward waits. A player who left was saved for the last
            // time; one changing the map gets it after, like the starter kit.
            if (player.BackupInventory is not null
                || !player.IsInWorld()
                || player.PlayerState.CurrentState == PlayerState.ChangingMap)
            {
                return false;
            }

            var previousPoints = account.GoblinPoints;
            account.GoblinPoints = (int)Math.Min((long)previousPoints + Math.Max(configuration.GoblinPoints, 0), int.MaxValue);
            var addedPoints = account.GoblinPoints - previousPoints;
            var given = false;
            try
            {
                // Saves the player, with the points, when the items were added.
                given = await RewardDelivery.TryGiveAsync(player, items).ConfigureAwait(false);
            }
            finally
            {
                if (!given)
                {
                    account.GoblinPoints -= addedPoints;
                }
            }

            if (!given && Format(configuration.NoSpaceMessage, Defaults.NoSpaceMessage) is { } message)
            {
                await player.ShowMessageAsync(message).ConfigureAwait(false);
            }

            return given;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Undoes a recorded reward which wasn't given. When that fails too, the reward stays recorded
    /// without being given, which a GM has to give.
    /// </summary>
    private static async ValueTask UndoRewardAsync(Player player, WeeklyGoalWeek week, Guid accountId)
    {
        try
        {
            await WeeklyGoalStore.RemoveRewardAsync(week, accountId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The reward of the weekly goal of {Week} for {Account} is recorded, but it wasn't given to {Character}.", week.Date, player.Account?.LoginName, player.Name);
        }
    }

    private async ValueTask ShowEntryAsync(Player player, Account account, WeeklyGoalProgress progress, WeeklyGoalConfiguration configuration)
    {
        string? message;
        if (progress.IsReached)
        {
            this.SetBonus(player, configuration);
            message = Format(configuration.EntryReachedMessage, Defaults.EntryReachedMessage, configuration.ExperienceBonusPercent, TbtTime.Format(progress.Week.End));
        }
        else
        {
            message = Format(configuration.EntryMessage, Defaults.EntryMessage, progress.Count, progress.Goal.Target, progress.Goal.Name, progress.Of(account.GetId()));
        }

        if (message is not null)
        {
            await player.ShowMessageAsync(message).ConfigureAwait(false);
        }
    }

    private async ValueTask AnnounceAsync(GameContext gameContext, ContextState state, WeeklyGoalProgress? progress, WeeklyGoalConfiguration configuration)
    {
        var step = progress?.GetStep(configuration.AnnouncementStep) ?? 0;
        var week = progress?.Week;
        if (!state.IsInitialized || week != state.Week)
        {
            var isTurn = state.IsInitialized;
            state.IsInitialized = true;
            state.Week = week;
            state.Step = step;
            if (isTurn && progress is not null)
            {
                await GlobalNotification.SendAsync(
                    gameContext,
                    configuration.NewGoalMessage,
                    Defaults.NewGoalMessage,
                    this.GetLogger(gameContext),
                    progress.Goal.Target,
                    progress.Goal.Name,
                    configuration.ExperienceBonusPercent,
                    TbtTime.Format(progress.Week.End)).ConfigureAwait(false);
            }

            return;
        }

        if (progress is null || step <= state.Step)
        {
            return;
        }

        state.Step = step;
        if (step >= FullPercent)
        {
            await GlobalNotification.SendAsync(
                gameContext,
                configuration.ReachedMessage,
                Defaults.ReachedMessage,
                this.GetLogger(gameContext),
                progress.Goal.Target,
                progress.Goal.Name,
                configuration.ExperienceBonusPercent,
                TbtTime.Format(progress.Week.End)).ConfigureAwait(false);
        }
        else
        {
            await GlobalNotification.SendAsync(
                gameContext,
                configuration.ProgressMessage,
                Defaults.ProgressMessage,
                this.GetLogger(gameContext),
                step,
                progress.Count,
                progress.Goal.Target,
                progress.Goal.Name).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the bonus to the humans in the world, and the reward to those whose account counted.
    /// </summary>
    private async ValueTask RewardPlayersAsync(IList<Player> players, WeeklyGoalProgress progress, WeeklyGoalConfiguration configuration)
    {
        HashSet<Guid>? rewarded = null;
        foreach (var player in players.Where(player => player.IsHuman() && player.IsInWorld()))
        {
            try
            {
                // It also attaches the bonus of who entered while the plugin was off.
                this.SetBonus(player, configuration);
                if (player.Account is not { } account || progress.Of(account.GetId()) <= 0)
                {
                    continue;
                }

                rewarded ??= await WeeklyGoalStore.GetRewardedAccountsAsync(progress.Week).ConfigureAwait(false);
                if (!rewarded.Contains(account.GetId()))
                {
                    await this.TryRewardAsync(player, account, progress.Week, configuration).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Couldn't check the weekly goal of {Character}.", player.Name);
            }
        }
    }

    /// <summary>
    /// Gives the rewards of the reached goals of the last weeks, in which the account counted, but
    /// which it didn't get, e.g. because none of its characters was in the world since.
    /// </summary>
    private async ValueTask GivePendingRewardsAsync(Player player, Account account, WeeklyGoalWeek currentWeek, WeeklyGoalConfiguration configuration)
    {
        var accountId = account.GetId();
        foreach (var (week, kind) in await WeeklyGoalStore.GetUnrewardedWeeksAsync(accountId, currentWeek.Before(PendingRewardWeeks - 1)).ConfigureAwait(false))
        {
            var counted = await WeeklyGoalStore.GetContributionsAsync(kind, week, accountId).ConfigureAwait(false);
            if (counted.Count > 0 && !await this.TryRewardAsync(player, account, week, configuration).ConfigureAwait(false))
            {
                // The inventory is full, or the reward failed: the next ones wait as well.
                return;
            }
        }
    }

    /// <summary>
    /// Tries the rewards again which waited for a player of the game server in the world, also those
    /// of the past weeks, when their delay is over.
    /// </summary>
    private async ValueTask RetryRewardsAsync(IGameContext gameContext, WeeklyGoalWeek currentWeek, WeeklyGoalConfiguration configuration)
    {
        var now = DateTime.UtcNow;
        foreach (var (player, nextAttempt) in this._nextRewardAttempts)
        {
            // A failed attempt waits again (TryRewardAsync).
            if (!ReferenceEquals(player.GameContext, gameContext)
                || now < nextAttempt
                || !this._nextRewardAttempts.TryRemove(new KeyValuePair<Player, DateTime>(player, nextAttempt))
                || !player.IsInWorld()
                || player.Account is not { } account)
            {
                continue;
            }

            try
            {
                await this.GivePendingRewardsAsync(player, account, currentWeek, configuration).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Couldn't give the waiting rewards of the weekly goal to {Character}.", player.Name);
            }
        }
    }

    /// <summary>
    /// Gives the reward of the week to the account, once: reserved in the process, recorded in the
    /// database and then given. A reward which doesn't fit into the inventory, or fails, is undone,
    /// so it's tried again after the <see cref="RewardRetryDelay"/>.
    /// </summary>
    private async ValueTask<bool> TryRewardAsync(Player player, Account account, WeeklyGoalWeek week, WeeklyGoalConfiguration configuration)
    {
        var items = GetRewardItems(configuration);
        if ((items.Count == 0 && configuration.GoblinPoints <= 0)
            || (this._nextRewardAttempts.TryGetValue(player, out var nextAttempt) && DateTime.UtcNow < nextAttempt))
        {
            return false;
        }

        var accountId = account.GetId();
        if (!this._rewarding.TryAdd((week, accountId), 0))
        {
            return false;
        }

        try
        {
            if (!await WeeklyGoalStore.TryRecordRewardAsync(week, accountId, player.Name).ConfigureAwait(false))
            {
                // Given before, e.g. to another character of the account.
                return false;
            }

            bool given;
            try
            {
                given = await GiveAsync(player, account, items, configuration).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Couldn't give the reward of the weekly goal to {Character}.", player.Name);
                given = false;
            }

            if (!given)
            {
                this._nextRewardAttempts[player] = DateTime.UtcNow + RewardRetryDelay;
                await UndoRewardAsync(player, week, accountId).ConfigureAwait(false);
                return false;
            }

            ActivityLog.Write("weekly_goal_reward", player, new { week = week.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
            if (Format(configuration.RewardMessage, Defaults.RewardMessage, GetRewardText(configuration)) is { } message)
            {
                await player.ShowMessageAsync(message, MessageType.GoldenCenter).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            this._rewarding.TryRemove((week, accountId), out _);
        }
    }

    private void SetBonus(Player player, WeeklyGoalConfiguration configuration)
        => this._bonuses.Attach(player)?.TrySetPercent(configuration.ExperienceBonusPercent);

    private WeeklyGoalConfiguration GetConfiguration() => this.Configuration ??= WeeklyGoalConfiguration.CreateDefault();

    private ILogger GetLogger(IGameContext gameContext)
        => this._logger ??= gameContext.LoggerFactory.CreateLogger(this.GetType().Name);

    /// <summary>
    /// The check and the announced progress of one game server.
    /// </summary>
    private sealed class ContextState
    {
        /// <summary>
        /// Gets the periodic check.
        /// </summary>
        public PeriodicCheck Check { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether a check learned the progress since the start.
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Gets or sets the week of the announced progress, or <c>null</c> for a week without a goal.
        /// </summary>
        public WeeklyGoalWeek? Week { get; set; }

        /// <summary>
        /// Gets or sets the announced step, in percent.
        /// </summary>
        public int Step { get; set; }
    }
}
