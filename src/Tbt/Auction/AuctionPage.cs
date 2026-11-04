// <copyright file="AuctionPage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// A page of the window of Otto.
/// </summary>
/// <param name="View">The page.</param>
/// <param name="Open">Whether the client opens the window.</param>
/// <param name="Category">The kind of the filter.</param>
/// <param name="Currency">The currency of the filter, as its code.</param>
/// <param name="Page">The page, from 0.</param>
/// <param name="Pages">How many pages there are.</param>
/// <param name="MineCount">How many items Otto keeps for the account.</param>
/// <param name="Earnings">The zen of the sales to take.</param>
/// <param name="Listings">The items of the page.</param>
/// <param name="AccountId">The account of the player, which marks its own items.</param>
internal sealed record AuctionPage(
    AuctionView View,
    bool Open,
    AuctionCategory Category,
    byte Currency,
    int Page,
    int Pages,
    int MineCount,
    long Earnings,
    IReadOnlyList<StoredListing> Listings,
    Guid AccountId);
