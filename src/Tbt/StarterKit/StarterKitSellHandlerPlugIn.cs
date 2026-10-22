// <copyright file="StarterKitSellHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The sale of items to a merchant, where the items of the starter kit are worth 0 zen: so the kit
/// can leave the inventory, but gives no zen. All other items sell as before. It replaces the
/// <c>SellItemToNpcHandlerPlugIn</c> of OpenMU, which must be deactivated: of two active handlers
/// for the same packet, only one is effective.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Venda ao NPC", Description = "Replaces the handler of the sale to a merchant (8bbf8737-8731-4975-baa8-e14f77451b85, which must be deactivated): the items of the starter kit are worth 0 zen, all other items sell as before.")]
[Guid("04377C18-DF01-43C8-9846-C391886DFE2A")]
public class StarterKitSellHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly SellItemToNpcAction _sellAction = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => SellItemToNpcRequest.Code;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        SellItemToNpcRequest message = packet;
        if (player.Inventory is { } inventory
            && inventory.GetItem(message.ItemSlot) is { } item
            && StarterKitStore.IsKitItem(item)
            && player.OpenedNpc is { Definition.MerchantStore: not null } merchant
            && item.Definition is { IsSellableToNpc: true, IsBoundToCharacter: false })
        {
            await SellForNothingAsync(player, inventory, item, merchant).ConfigureAwait(false);
            return;
        }

        // Everything else, including the refusals, like the Pet Panda, which no merchant buys.
        await this._sellAction.SellItemAsync(player, message.ItemSlot).ConfigureAwait(false);
    }

    /// <summary>
    /// Sells the item like the <see cref="SellItemToNpcAction"/>, without the money.
    /// </summary>
    private static async ValueTask SellForNothingAsync(Player player, IStorage inventory, Item item, NonPlayerCharacter merchant)
    {
        await inventory.RemoveItemAsync(item).ConfigureAwait(false);
        await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IItemSoldToNpcPlugIn>(p => p.ItemSoldToNpcAsync(true)).ConfigureAwait(false);
        player.GameContext.PlugInManager.GetPlugInPoint<IItemSoldToMerchantPlugIn>()?.ItemSold(player, item, merchant);
        await player.ShowMessageAsync("Item do kit iniciante vendido por 0 zen.").ConfigureAwait(false);
    }
}
