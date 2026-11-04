// <copyright file="NewListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// An item which a seller puts up for sale.
/// </summary>
/// <param name="SellerAccountId">The account of the seller.</param>
/// <param name="SellerCharacter">The character of the seller.</param>
/// <param name="Item">The item.</param>
/// <param name="ItemBytes">The item as the game client gets it, for the window.</param>
/// <param name="ItemName">The name of the item, for the records.</param>
/// <param name="Category">The kind of the item.</param>
/// <param name="Currency">The currency of the price.</param>
/// <param name="Price">The price.</param>
/// <param name="Session">The stay of the seller in the world.</param>
/// <param name="ItemId">The identifier of the item in the inventory.</param>
/// <param name="ItemType">The item type as the client counts it, for the search by name: group * 512 + number.</param>
/// <param name="Excellent">Whether the item is excellent, for the search.</param>
/// <param name="Ancient">Whether the item is ancient, for the search.</param>
/// <param name="Sockets">How many sockets the item has, for the search.</param>
/// <param name="Level">The level of the item, for the search.</param>
internal sealed record NewListing(
    Guid SellerAccountId,
    string SellerCharacter,
    AuctionItem Item,
    byte[] ItemBytes,
    string ItemName,
    AuctionCategory Category,
    string Currency,
    long Price,
    Guid Session,
    Guid ItemId,
    int ItemType,
    bool Excellent,
    bool Ancient,
    short Sockets,
    short Level);
