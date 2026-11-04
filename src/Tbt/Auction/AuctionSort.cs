// <copyright file="AuctionSort.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The order of the items for sale in the window of Otto. The values are in the packets.
/// </summary>
internal enum AuctionSort : byte
{
    /// <summary>The newest first.</summary>
    Newest = 0,

    /// <summary>The cheapest first, by currency.</summary>
    PriceLow = 1,

    /// <summary>The most expensive first, by currency.</summary>
    PriceHigh = 2,

    /// <summary>The sales which end first.</summary>
    EndingSoon = 3,
}
