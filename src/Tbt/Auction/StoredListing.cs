// <copyright file="StoredListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// An item which Otto keeps.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="SellerAccountId">The account of the seller.</param>
/// <param name="SellerCharacter">The character of the seller.</param>
/// <param name="ItemJson">The item, as JSON (<see cref="AuctionItem"/>).</param>
/// <param name="ItemBytes">The item as the game client gets it.</param>
/// <param name="ItemName">The name of the item.</param>
/// <param name="Currency">The currency of the price.</param>
/// <param name="Price">The price.</param>
/// <param name="State">The state.</param>
/// <param name="SecondsLeft">The seconds until its sale ends; 0 or less, when it ended.</param>
internal sealed record StoredListing(
    long Id,
    Guid SellerAccountId,
    string SellerCharacter,
    string ItemJson,
    byte[] ItemBytes,
    string ItemName,
    string Currency,
    long Price,
    string State,
    int SecondsLeft)
{
    /// <summary>
    /// Gets a value indicating whether its sale ended.
    /// </summary>
    public bool Ended => this.SecondsLeft <= 0;
}
