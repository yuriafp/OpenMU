// <copyright file="EventGoblinPointsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.GoblinPoints;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.MiniGames.Kanturu;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// Goblin Points for a completed event (PLANO-SERVIDOR.md, 5.1, item 16 of 9.5): every human who
/// stays in a mini game until its end gets them, and its winner more. They add to the 10 GP per hour
/// online of OpenMU's <see cref="GoblinPointsForPlayTimePlugIn"/>, to buy in the X shop.
/// </summary>
/// <remarks>
/// Who dies or leaves before the end isn't there when the mini game ends, so only the players which
/// are still in it get the points, like OpenMU gives the rewards of the end. A failed run pays too,
/// since it took the ticket and the time; the plugin point doesn't tell a failure anyway. The Tower
/// of Refinement of Kanturu, which is open for a while after a won battle, isn't an event, so it pays
/// nothing. Bots get nothing. Every grant is written to the registro.
/// </remarks>
[PlugIn]
[Display(Name = "TBT: GP por evento", Description = "Gives Goblin Points to the humans who stay in an event (Blood Castle, Devil Square, Chaos Castle and the other mini games) until its end, and more to its winner.")]
[Guid("4E8B2D71-9C3A-4F56-B1E0-7A5D2C8F3B49")]
public class EventGoblinPointsPlugIn : IMiniGameEndedPlugIn, ISupportCustomConfiguration<EventGoblinPointsConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public EventGoblinPointsConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EventGoblinPointsConfiguration();

    /// <inheritdoc />
    public async ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        try
        {
            if (miniGame is KanturuContext { TowerMode: true } || CashShopFeaturePlugIn.GetSettings(miniGame.GameContext) is null)
            {
                return;
            }

            var configuration = this.Configuration ??= new EventGoblinPointsConfiguration();
            foreach (var (player, points) in GetGrants(finishers, winner, configuration, TbtPlayerExtensions.IsHuman).ToList())
            {
                await GivePointsAsync(player, points, ReferenceEquals(player, winner), miniGame.Definition, configuration).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would keep the other plugins
            // of the end of the mini game, like the registro, from running.
            miniGame.GameContext.LoggerFactory.CreateLogger(this.GetType().Name).LogWarning(ex, "Couldn't give the Goblin Points of the event {Event}.", miniGame.Definition.Name.ValueInNeutralLanguage);
        }
    }

    /// <summary>
    /// Gets who gets how many points at the end of a mini game: the players still in it, and its
    /// winner, also when it isn't in the mini game anymore but still in the world, with the winner
    /// points on top.
    /// </summary>
    /// <param name="finishers">The players which are in the mini game at its end.</param>
    /// <param name="winner">The winner, if any.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isHuman">Determines whether a player is a human in the world; only those get points.</param>
    /// <returns>The players and their points.</returns>
    internal static IEnumerable<(Player Player, int Points)> GetGrants(
        IReadOnlyCollection<Player> finishers, Player? winner, EventGoblinPointsConfiguration configuration, Func<Player, bool> isHuman)
    {
        var players = winner is not null && !finishers.Contains(winner) ? finishers.Append(winner) : finishers;
        foreach (var player in players.Distinct().Where(isHuman))
        {
            var points = configuration.Points + (ReferenceEquals(player, winner) ? configuration.WinnerPoints : 0);
            if (points > 0)
            {
                yield return (player, points);
            }
        }
    }

    /// <summary>
    /// Formats the message of a grant. A message which the admin panel left broken falls back to
    /// the default one, so the player still learns about the points.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="points">The points.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <returns>The message, or <c>null</c> when the configuration has none.</returns>
    internal static string? FormatMessage(EventGoblinPointsConfiguration configuration, int points, string eventName)
    {
        if (string.IsNullOrWhiteSpace(configuration.Message))
        {
            return null;
        }

        try
        {
            return string.Format(configuration.Message, points, eventName);
        }
        catch (FormatException)
        {
            return string.Format(new EventGoblinPointsConfiguration().Message, points, eventName);
        }
    }

    private static async ValueTask GivePointsAsync(Player player, int points, bool isWinner, MiniGameDefinition definition, EventGoblinPointsConfiguration configuration)
    {
        if (player.Account is not { } account)
        {
            return;
        }

        var eventName = definition.Name.ValueInNeutralLanguage;
        try
        {
            await player.RunPersistenceExclusiveAsync(() =>
            {
                account.GoblinPoints = (int)Math.Min((long)account.GoblinPoints + points, int.MaxValue);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // One player's failure mustn't keep the others from their points.
            player.Logger.LogWarning(ex, "Couldn't give {Points} Goblin Points for the event {Event} to {Player}.", points, eventName, player.Name);
            return;
        }

        ActivityLog.Write("goblin_points", player, new { type = definition.Type.ToString(), level = definition.GameLevel, points, winner = isWinner });
        try
        {
            if (FormatMessage(configuration, points, eventName) is { } message)
            {
                await player.ShowMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't tell {Player} about the Goblin Points of the event {Event}.", player.Name, eventName);
        }
    }
}
