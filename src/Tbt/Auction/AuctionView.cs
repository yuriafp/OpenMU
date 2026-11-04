// <copyright file="AuctionView.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The page which the window of Otto shows.
/// </summary>
internal enum AuctionView : byte
{
    /// <summary>The items for sale.</summary>
    Browse = 1,

    /// <summary>The items which Otto keeps for the account.</summary>
    Mine = 2,
}
