// <copyright file="AuctionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The configuration of the leilão of Otto (<see cref="AuctionPlugIn"/>). The fees, the duration and
/// the limit are the decisions of the owner of 2026-11-04 (PLANO-SERVIDOR.md, 9.14), values for the calibration.
/// </summary>
public class AuctionConfiguration
{
    /// <summary>
    /// Gets or sets the number of the NPC of Otto.
    /// </summary>
    /// <remarks>
    /// Our client shows the NPC 999 as Otto, with the model of Jeweler Raul of the Loren Market; another
    /// number needs the same change in its <c>Monsters.json</c>.
    /// </remarks>
    [Display(Name = "NPC Number", Description = "The number of the NPC of Otto, o Leiloeiro. Our client shows the NPC 999 as Otto.")]
    public short NpcNumber { get; set; } = 999;

    /// <summary>
    /// Gets or sets the fee to put an item up for sale, in zen.
    /// </summary>
    [Display(Name = "Listing Fee", Description = "The fee to put an item up for sale, in zen. It isn't given back.")]
    [Range(0, 100_000_000)]
    public int ListingFee { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the fee to take back an item before its sale ends, in zen. An item whose sale ended comes back for free.
    /// </summary>
    [Display(Name = "Cancel Fee", Description = "The fee to take back an item before its sale ends, in zen. An item whose sale ended comes back for free.")]
    [Range(0, 100_000_000)]
    public int CancelFee { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets the fee of the buyer, in zen, on top of the price.
    /// </summary>
    [Display(Name = "Buyer Fee", Description = "The fee of the buyer, in zen, on top of the price, whatever its currency.")]
    [Range(0, 100_000_000)]
    public int BuyerFee { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets how many days an item stays for sale.
    /// </summary>
    [Display(Name = "Duration in Days", Description = "How many days an item stays for sale. Then its seller takes it back for free.")]
    [Range(1, 30)]
    public int DurationDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets how many items an account has with Otto at most: for sale, or whose sale ended and which weren't taken back.
    /// </summary>
    [Display(Name = "Maximum Items per Account", Description = "How many items an account has with Otto at most: for sale, or whose sale ended and which weren't taken back.")]
    [Range(1, AuctionContentsWriter.MaximumEntries)]
    public int MaximumListingsPerAccount { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many items a page of the window shows: the client shows 8 rows at once, and
    /// a longer page in parts of 8.
    /// </summary>
    [Display(Name = "Items per Page", Description = "How many items a page of the window of Otto shows.")]
    [Range(1, AuctionContentsWriter.MaximumEntries)]
    public int PageSize { get; set; } = 8;

    /// <summary>
    /// Gets or sets the highest price in jewels.
    /// </summary>
    [Display(Name = "Maximum Jewel Price", Description = "The highest price in jewels. The highest price in zen is the most zen a character can carry.")]
    [Range(1, 1_000_000)]
    public int MaximumJewelPrice { get; set; } = 9_999;
}
