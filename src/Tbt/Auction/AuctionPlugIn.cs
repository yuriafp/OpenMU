// <copyright file="AuctionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Otto, o Leiloeiro, in Lorencia: a market between the players. A seller puts an item of the bag
/// up for sale, at a price in zen or in jewels, for a fee; a buyer pays the price and a fee and gets
/// the item; the zen of a sale waits with Otto, the jewels go to the Banqueira (<see cref="Auctioneer"/>).
/// Talking to him opens his window in our client, whose buttons send <see cref="AuctionRequest"/>.
/// At the entry into the world, he settles what an earlier stay left open. While it's inactive, he doesn't talk.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Leilão do Otto", Description = "Otto, o Leiloeiro: the players sell items of the bag for zen or jewels (paid from and to the Banqueira), with fees in zen to list, to take back and to buy. Needs the handler 'TBT: Pedidos da Banqueira', which also takes his requests (C1 FB 05).")]
[Guid("3E9C5A71-8D24-4B6F-A1C3-5F7E2D9B0A48")]
public class AuctionPlugIn : IPlayerTalkToNpcPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<AuctionConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public AuctionConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AuctionConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        var configuration = this.GetConfiguration();
        if (npc.Definition.Number != configuration.NpcNumber)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        try
        {
            // The window stays open until the client closes the dialog of the NPC, like the merchants.
            eventArgs.LeavesDialogOpen = player.IsHuman() && await Auctioneer.OpenAsync(player, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would keep the other plugins
            // of the talk from running.
            player.Logger.LogWarning(ex, "Couldn't open Otto for {Character}.", player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (!player.IsHuman())
        {
            return;
        }

        try
        {
            if (TbtPlayerExtensions.IsEnteringWorld(previousState, currentState))
            {
                await EnterAsync(player).ConfigureAwait(false);
            }
            else if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState)
                     && !await player.RunPersistenceExclusiveAsync(() => Auctioneer.LeaveAsync(player)).ConfigureAwait(false))
            {
                player.Logger.LogError("Otto couldn't settle the operations of {Character} at the exit.", player.Name);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the entry or the exit.
            player.Logger.LogWarning(ex, "Couldn't settle Otto for {Character}.", player.Name);
        }
    }

    /// <summary>
    /// Gets the active plugin of the game, with its configuration.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The configuration; <c>null</c>, while the plugin is inactive.</returns>
    internal static AuctionConfiguration? GetActiveConfiguration(Player player)
        => player.GameContext.PlugInManager.GetActivePlugInsOf<IPlayerTalkToNpcPlugIn>().OfType<AuctionPlugIn>().FirstOrDefault()?.GetConfiguration();

    private static async ValueTask EnterAsync(Player player)
    {
        // Before the player can use the item of an operation which an earlier stay left open: the item
        // of a listing whose save failed is back in the inventory, and using it would make a later
        // entry put it up for sale.
        if (await player.RunPersistenceExclusiveAsync(() => Auctioneer.EnterAsync(player)).ConfigureAwait(false))
        {
            return;
        }

        player.Logger.LogError("Otto couldn't settle the operations of {Character} at the entry; disconnecting.", player.Name);
        await player.ShowMessageAsync("O Otto não conseguiu conferir os seus itens. Entre de novo em instantes.").ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
    }

    private AuctionConfiguration GetConfiguration() => this.Configuration ??= new AuctionConfiguration();
}
