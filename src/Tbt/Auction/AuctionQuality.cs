// <copyright file="AuctionQuality.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The filter of the window of Otto by what an item has: an option or a level. The values are in the packets.
/// </summary>
internal enum AuctionQuality : byte
{
    /// <summary>Any item.</summary>
    Any = 0,

    /// <summary>Excellent items.</summary>
    Excellent = 1,

    /// <summary>Ancient items.</summary>
    Ancient = 2,

    /// <summary>Items with sockets.</summary>
    Socket = 3,

    /// <summary>Items of level 7 or more.</summary>
    Level7 = 4,

    /// <summary>Items of level 9 or more.</summary>
    Level9 = 5,

    /// <summary>Items of level 11 or more.</summary>
    Level11 = 6,

    /// <summary>Items of level 13 or more.</summary>
    Level13 = 7,
}
