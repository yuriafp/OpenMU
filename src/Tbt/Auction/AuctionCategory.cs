// <copyright file="AuctionCategory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The kinds of items of the filter of the window of Otto. The values are in the packets and in
/// <c>tbt.auction_listing</c>, so they never change; a new kind gets the next value.
/// </summary>
internal enum AuctionCategory : byte
{
    /// <summary>All kinds; only a filter, no item has it.</summary>
    All = 0,

    /// <summary>Swords, axes, maces, spears, bows and staffs: the item groups 0 to 5.</summary>
    Weapons = 1,

    /// <summary>Shields: the item group 6.</summary>
    Shields = 2,

    /// <summary>Helms, armors, pants, gloves and boots: the item groups 7 to 11.</summary>
    Armor = 3,

    /// <summary>Wings, capes and the like.</summary>
    Wings = 4,

    /// <summary>The jewels and packs which the Banqueira keeps.</summary>
    Jewels = 5,

    /// <summary>Everything else: potions, scrolls, boxes, quest items.</summary>
    Others = 6,

    /// <summary>Rings and pendants.</summary>
    Accessories = 7,

    /// <summary>Pets: the items of the pet slot.</summary>
    Pets = 8,
}
