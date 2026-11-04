// <copyright file="AuctionRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// Is sent by the client when: the player uses the window of Otto.
/// Causes reaction on server side: Otto answers a page with <see cref="AuctionContentsWriter"/>
/// (C2 FB 04), or does what was asked and answers with its result (C1 FB 06).
/// </summary>
/// <remarks>
/// C1 14 FB 05, a packet of our MuMain client: the action (byte 4), two arguments (bytes 5 and 6),
/// a value (bytes 8 to 15: a page or the identifier of an item) and a price (bytes 16 to 19), little endian.
/// A search (Browse) may go on: the quality (byte 20, <see cref="AuctionQuality"/>), the order (byte 21,
/// <see cref="AuctionSort"/>), how many item types follow (byte 22; 0xFF without a search by name), and
/// the item types (2 bytes each from byte 23).
/// </remarks>
/// <param name="Action">The action.</param>
/// <param name="Argument1">The first argument: a kind of item, a slot of the inventory, or whether a sale ended.</param>
/// <param name="Argument2">The second argument: a currency.</param>
/// <param name="Value">The value: a page, or the identifier of an item.</param>
/// <param name="Price">The price.</param>
/// <param name="Quality">The quality which a search looks for.</param>
/// <param name="ItemTypes">The item types which a search by name looks for; <c>null</c> without one.</param>
/// <param name="Sort">The order of the items which a search shows.</param>
internal readonly record struct AuctionRequest(AuctionAction Action, byte Argument1, byte Argument2, long Value, uint Price, AuctionQuality Quality = AuctionQuality.Any, int[]? ItemTypes = null, AuctionSort Sort = AuctionSort.Newest)
{
    /// <summary>
    /// The count of item types of a search without a search by name.
    /// </summary>
    public const byte NoNameSearch = 0xFF;

    /// <summary>
    /// The length of a search without its item types: the request, the quality, the order and the count.
    /// </summary>
    public const int SearchLength = 23;

    /// <summary>
    /// The most item types a search by name sends: what fits in a C1 packet.
    /// </summary>
    public const int MaximumItemTypes = (255 - SearchLength) / 2;

    /// <summary>
    /// Gets the operation code of this data packet.
    /// </summary>
    public static byte Code => 0xFB;

    /// <summary>
    /// Gets the operation sub-code of this data packet.
    /// </summary>
    public static byte SubCode => 0x05;

    /// <summary>
    /// Gets the length of this data packet.
    /// </summary>
    public static int Length => 20;

    /// <summary>
    /// Reads the request.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The request; <c>null</c>, if the packet isn't one.</returns>
    public static AuctionRequest? Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < Length || packet[0] != 0xC1 || packet[2] != Code || packet[3] != SubCode
            || !Enum.IsDefined((AuctionAction)packet[4]))
        {
            return null;
        }

        var quality = AuctionQuality.Any;
        var sort = AuctionSort.Newest;
        int[]? itemTypes = null;
        if (packet.Length >= SearchLength)
        {
            quality = Enum.IsDefined((AuctionQuality)packet[20]) ? (AuctionQuality)packet[20] : AuctionQuality.Any;
            sort = Enum.IsDefined((AuctionSort)packet[21]) ? (AuctionSort)packet[21] : AuctionSort.Newest;
            var count = packet[22];
            if (count != NoNameSearch)
            {
                if (count > MaximumItemTypes || packet.Length < SearchLength + (count * 2))
                {
                    return null;
                }

                itemTypes = new int[count];
                for (var i = 0; i < count; i++)
                {
                    itemTypes[i] = ReadUInt16LittleEndian(packet[(SearchLength + (i * 2))..]);
                }
            }
        }

        return new AuctionRequest((AuctionAction)packet[4], packet[5], packet[6], ReadInt64LittleEndian(packet[8..]), ReadUInt32LittleEndian(packet[16..]), quality, itemTypes, sort);
    }
}
