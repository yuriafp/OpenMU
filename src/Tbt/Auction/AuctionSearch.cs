// <copyright file="AuctionSearch.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// What the window of Otto looks for among the items for sale.
/// </summary>
/// <param name="Category">The kind of item; <see cref="AuctionCategory.All"/> for all.</param>
/// <param name="Currency">The currency; <c>null</c> for all.</param>
/// <param name="Quality">What the item has.</param>
/// <param name="ItemTypes">
/// The items by name, as the client found them in its own names (they're translated there):
/// group * 512 + number. <c>null</c> without a search by name; empty when the name matched nothing.
/// </param>
internal sealed record AuctionSearch(AuctionCategory Category, string? Currency, AuctionQuality Quality, int[]? ItemTypes)
{
    /// <summary>
    /// The number of item numbers in a group, as the client counts the item types.
    /// </summary>
    public const int ItemsPerGroup = 512;

    /// <summary>
    /// Gets the search of everything.
    /// </summary>
    public static AuctionSearch All { get; } = new(AuctionCategory.All, null, AuctionQuality.Any, null);

    /// <summary>
    /// Gets the lowest level of the items, by <see cref="Quality"/>.
    /// </summary>
    public short MinimumLevel => this.Quality switch
    {
        AuctionQuality.Level7 => 7,
        AuctionQuality.Level9 => 9,
        AuctionQuality.Level11 => 11,
        AuctionQuality.Level13 => 13,
        _ => 0,
    };
}
