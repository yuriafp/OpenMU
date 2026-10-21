// <copyright file="WelcomePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Welcome;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The welcome (PLANO-SERVIDOR.md, 5.8, item 5 of 9.5): when a human enters the world, the other
/// players see it, and the player gets two tips and the next events. It replaces the
/// <c>ShowMessageToAllWhenPlayerEnteredWorldPlugIn</c>, which announces the bots, too.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Boas-vindas", Description = "Announces the humans entering the world (never the bots) and shows them tips and the next events.")]
[Guid("4E8B2D61-7A3C-4B19-9F5E-1C6A8D3B7E52")]
public class WelcomePlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<WelcomeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public WelcomeConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new WelcomeConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (!TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) || !player.IsHuman())
        {
            return;
        }

        try
        {
            await WelcomeAsync(player, this.Configuration ??= new WelcomeConfiguration()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the entry.
            player.Logger.LogWarning(ex, "Couldn't welcome {Character}.", player.Name);
        }
    }

    private static async ValueTask WelcomeAsync(Player player, WelcomeConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.EntryMessage) && player.Account?.State != AccountState.GameMasterInvisible)
        {
            var entryMessage = string.Format(configuration.EntryMessage, player.Name);
            await player.GameContext.ForEachPlayerAsync(other => ReferenceEquals(other, player)
                ? Task.CompletedTask
                : other.ShowMessageAsync(entryMessage).AsTask()).ConfigureAwait(false);
        }

        foreach (var tip in new[] { configuration.FirstTip, configuration.SecondTip })
        {
            if (!string.IsNullOrWhiteSpace(tip))
            {
                await player.ShowMessageAsync(tip).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.NextEventsMessage)
            && EventSchedule.GetNextEvents(player.GameContext, configuration.NextEventsCount).ToList() is { Count: > 0 } events)
        {
            var list = string.Join(", ", events.Select(e => $"{TbtTime.Format(e.Start)} {e.Name}"));
            await player.ShowMessageAsync(string.Format(configuration.NextEventsMessage, list)).ConfigureAwait(false);
        }
    }
}
