// <copyright file="AuctionCategories.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// The kind of an item.
/// </summary>
internal static class AuctionCategories
{
    /// <summary>
    /// Gets the kind of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The kind.</returns>
    public static AuctionCategory Of(Item item)
    {
        if (BankJewel.Of(item) is not null)
        {
            return AuctionCategory.Jewels;
        }

        if (item.Definition is { } definition && definition.IsWing())
        {
            return AuctionCategory.Wings;
        }

        if (item.Definition?.ItemSlot?.ItemSlots is { } slots)
        {
            if (slots.Contains(InventoryConstants.PetSlot))
            {
                return AuctionCategory.Pets;
            }

            if (slots.Contains(InventoryConstants.PendantSlot) || slots.Contains(InventoryConstants.Ring1Slot) || slots.Contains(InventoryConstants.Ring2Slot))
            {
                return AuctionCategory.Accessories;
            }
        }

        return item.Definition?.Group switch
        {
            <= 5 => AuctionCategory.Weapons,
            6 => AuctionCategory.Shields,
            >= 7 and <= 11 => AuctionCategory.Armor,
            _ => AuctionCategory.Others,
        };
    }
}
