// <copyright file="AuctionContentsWriter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using System.Text;
using MUnique.OpenMU.Network.Packets;
using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// The packets of Otto to our MuMain client: a page of his window, and the result of a request.
/// </summary>
/// <remarks>
/// <para>
/// C2 FB 04, a page: the page (byte 5: 1 for sale, 2 of the account), flags (byte 6: 1 opens the
/// window), the filter (bytes 7 and 8: kind and currency), the count of items (byte 9), the page
/// and the pages (bytes 10 to 13), the fees (bytes 14 to 25: listing, cancel, buyer), the days
/// (byte 26), the limit (byte 27), the items of the account (byte 28), the zen to take (bytes 30
/// to 37), then the items, of <see cref="EntryLength"/> bytes: the identifier (0 to 7), the
/// currency (8), flags (9: <see cref="EntryFlags"/>), the price (12 to 15), the seconds left (16 to
/// 19, 0 or less when the sale ended), the seller (20 to 29) and the item as the client gets it
/// in the inventory (its length at 30, the item at 32 to 47). Numbers are little endian.
/// </para>
/// <para>
/// C1 08 FB 06, a result: the action (byte 4) and whether it was done (byte 5). The window then
/// asks for its page again.
/// </para>
/// </remarks>
internal static class AuctionContentsWriter
{
    /// <summary>The most items of a page.</summary>
    internal const int MaximumEntries = 40;

    /// <summary>Where the items start.</summary>
    internal const int EntriesOffset = 38;

    /// <summary>The length of an item of the page.</summary>
    internal const int EntryLength = 48;

    /// <summary>The most bytes of an item as the client gets it.</summary>
    internal const int ItemLength = 16;

    /// <summary>The length of the name of the seller.</summary>
    internal const int NameLength = 10;

    /// <summary>The length of the result.</summary>
    internal const int ResultLength = 8;

    /// <summary>The operation code of the packets.</summary>
    internal const byte Code = 0xFB;

    /// <summary>The sub-code of a page.</summary>
    internal const byte PageSubCode = 0x04;

    /// <summary>The sub-code of a result.</summary>
    internal const byte ResultSubCode = 0x06;

    /// <summary>
    /// The flags of an item of the page.
    /// </summary>
    [Flags]
    internal enum EntryFlags : byte
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>The item is of the account of the player.</summary>
        Mine = 1,

        /// <summary>Its sale ended.</summary>
        Ended = 2,

        /// <summary>It's on its way: its listing or delivery isn't settled yet.</summary>
        Pending = 4,
    }

    /// <summary>
    /// Gets the length of a page.
    /// </summary>
    /// <param name="count">The count of items.</param>
    /// <returns>The length.</returns>
    public static int LengthOf(int count) => EntriesOffset + (Math.Min(count, MaximumEntries) * EntryLength);

    /// <summary>
    /// Writes a page.
    /// </summary>
    /// <param name="target">The target, of <see cref="LengthOf"/> bytes.</param>
    /// <param name="page">The page.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The length of the packet.</returns>
    public static int Write(Span<byte> target, AuctionPage page, AuctionConfiguration configuration)
    {
        var count = Math.Min(page.Listings.Count, MaximumEntries);
        var length = LengthOf(count);
        target = target[..length];
        target.Clear();
        var header = new C2HeaderWithSubCodeRef(target);
        header.Type = 0xC2;
        header.Length = (ushort)length;
        header.Code = Code;
        header.SubCode = PageSubCode;
        target[5] = (byte)page.View;
        target[6] = page.Open ? (byte)1 : (byte)0;
        target[7] = (byte)page.Category;
        target[8] = page.Currency;
        target[9] = (byte)count;
        WriteUInt16LittleEndian(target[10..], (ushort)Math.Clamp(page.Page, 0, ushort.MaxValue));
        WriteUInt16LittleEndian(target[12..], (ushort)Math.Clamp(page.Pages, 1, ushort.MaxValue));
        WriteInt32LittleEndian(target[14..], configuration.ListingFee);
        WriteInt32LittleEndian(target[18..], configuration.CancelFee);
        WriteInt32LittleEndian(target[22..], configuration.BuyerFee);
        target[26] = (byte)Math.Clamp(configuration.DurationDays, 0, byte.MaxValue);
        target[27] = (byte)Math.Clamp(configuration.MaximumListingsPerAccount, 0, byte.MaxValue);
        target[28] = (byte)Math.Clamp(page.MineCount, 0, byte.MaxValue);
        WriteInt64LittleEndian(target[30..], page.Earnings);
        for (var i = 0; i < count; i++)
        {
            WriteEntry(target.Slice(EntriesOffset + (i * EntryLength), EntryLength), page.Listings[i], page.AccountId);
        }

        return length;
    }

    /// <summary>
    /// Writes a result.
    /// </summary>
    /// <param name="target">The target, of <see cref="ResultLength"/> bytes.</param>
    /// <param name="action">The action.</param>
    /// <param name="done">Whether it was done.</param>
    /// <returns>The length of the packet.</returns>
    public static int WriteResult(Span<byte> target, AuctionAction action, bool done)
    {
        target = target[..ResultLength];
        target.Clear();
        target[0] = 0xC1;
        target[1] = ResultLength;
        target[2] = Code;
        target[3] = ResultSubCode;
        target[4] = (byte)action;
        target[5] = done ? (byte)1 : (byte)0;
        return ResultLength;
    }

    private static void WriteEntry(Span<byte> target, StoredListing listing, Guid accountId)
    {
        WriteInt64LittleEndian(target, listing.Id);
        target[8] = AuctionCurrency.CodeOf(listing.Currency) ?? AuctionCurrency.Any;
        var flags = EntryFlags.None;
        if (listing.SellerAccountId == accountId)
        {
            flags |= EntryFlags.Mine;
        }

        if (listing.Ended)
        {
            flags |= EntryFlags.Ended;
        }

        if (listing.State != AuctionStore.Active)
        {
            flags |= EntryFlags.Pending;
        }

        target[9] = (byte)flags;
        WriteUInt32LittleEndian(target[12..], (uint)Math.Clamp(listing.Price, 0, uint.MaxValue));
        WriteInt32LittleEndian(target[16..], listing.SecondsLeft);
        var name = target.Slice(20, NameLength);
        Encoding.Latin1.GetBytes(listing.SellerCharacter.AsSpan(0, Math.Min(listing.SellerCharacter.Length, NameLength)), name);
        var item = listing.ItemBytes.AsSpan(0, Math.Min(listing.ItemBytes.Length, ItemLength));
        target[30] = (byte)item.Length;
        item.CopyTo(target[32..]);
    }
}
