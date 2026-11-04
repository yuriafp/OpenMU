// <copyright file="AuctionAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// What the window of Otto asks. The values are in the packets, so they never change.
/// </summary>
internal enum AuctionAction : byte
{
    /// <summary>A page of the items for sale: the kind (argument 1), the currency (argument 2, 0xFF for all) and the page (value).</summary>
    Browse = 1,

    /// <summary>The items which Otto keeps for the account.</summary>
    Mine = 2,

    /// <summary>Puts up for sale the item of a slot of the inventory (argument 1), at a price (price) in a currency (argument 2).</summary>
    List = 3,

    /// <summary>Buys an item (value), at the currency (argument 2) and price (price) which the window showed.</summary>
    Buy = 4,

    /// <summary>Takes back an item (value); argument 1 is 1, when the window showed that its sale ended.</summary>
    Return = 5,

    /// <summary>Takes the zen of the sales.</summary>
    ClaimZen = 6,
}
