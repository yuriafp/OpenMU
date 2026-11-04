// <copyright file="JewelBankPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Banqueira, in Lorencia: she keeps the jewels of the account, each kind apart, and gives them
/// out again as a jewel or a pack, for a fee in zen per jewel (<see cref="JewelBanker"/>). Talking to
/// her opens her window in our client, whose buttons send <see cref="JewelBankRequest"/>. At the entry
/// into the world, she settles what an earlier stay left open. While it's inactive, she doesn't talk.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Banqueira", Description = "The Banqueira keeps the jewels of the account (Bless, Soul, Life, Chaos, Creation, Guardian, Harmony, Gemstone and the refine stones, also packed) for a fee in zen per jewel, and gives them out as a jewel or a pack of 10 or 30. Needs the handler 'TBT: Pedidos da Banqueira'.")]
[Guid("7D3B9E52-4A1F-4C86-B0E7-2F5A8C61D493")]
public class JewelBankPlugIn : IPlayerTalkToNpcPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<JewelBankConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public JewelBankConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new JewelBankConfiguration();

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
            eventArgs.LeavesDialogOpen = player.IsHuman() && await JewelBanker.OpenAsync(player, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would keep the other plugins
            // of the talk from running.
            player.Logger.LogWarning(ex, "Couldn't open the Banqueira for {Character}.", player.Name);
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
                     && !await player.RunPersistenceExclusiveAsync(() => JewelBanker.LeaveAsync(player)).ConfigureAwait(false))
            {
                player.Logger.LogError("The Banqueira couldn't settle the operations of {Character} at the exit.", player.Name);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the entry or the exit.
            player.Logger.LogWarning(ex, "Couldn't settle the Banqueira for {Character}.", player.Name);
        }
    }

    /// <summary>
    /// Gets the active plugin of the game, with its configuration.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The configuration; <c>null</c>, while the plugin is inactive.</returns>
    internal static JewelBankConfiguration? GetActiveConfiguration(Player player)
        => player.GameContext.PlugInManager.GetActivePlugInsOf<IPlayerTalkToNpcPlugIn>().OfType<JewelBankPlugIn>().FirstOrDefault()?.GetConfiguration();

    private static async ValueTask EnterAsync(Player player)
    {
        // Before the player can use the items of an operation which an earlier stay left open. While
        // that can't be settled, the player stays out: the items of a deposit whose save failed are
        // back in the inventory, and using them would make a later entry credit the deposit.
        if (await player.RunPersistenceExclusiveAsync(() => JewelBanker.EnterAsync(player)).ConfigureAwait(false))
        {
            return;
        }

        player.Logger.LogError("The Banqueira couldn't settle the operations of {Character} at the entry; disconnecting.", player.Name);
        await player.ShowMessageAsync("A Banqueira não conseguiu conferir as suas joias. Entre de novo em instantes.").ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
    }

    private JewelBankConfiguration GetConfiguration() => this.Configuration ??= new JewelBankConfiguration();
}
