// <copyright file="PeriodItemRef.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.PeriodItems;

using MUnique.OpenMU.Network.Packets;
using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// Is sent by the server when: after the <see cref="PeriodItemCountRef"/>, for each item with an expiry.
/// Causes reaction on client side: the client marks the item at the slot as temporary and shows its expiry, like on the timed items of the cash shop.
/// </summary>
/// <remarks>
/// The packet of the classic period item list, as agreed with our MuMain client. OpenMU doesn't
/// define it (src/Network/Packets), so it's written like the structures generated there.
/// </remarks>
internal readonly ref struct PeriodItemRef
{
    private readonly Span<byte> _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodItemRef"/> struct, and writes its header.
    /// </summary>
    /// <param name="data">The underlying data.</param>
    public PeriodItemRef(Span<byte> data)
    {
        this._data = data;
        var header = this.Header;
        header.Type = HeaderType;
        header.Code = Code;
        header.Length = (byte)Math.Min(data.Length, Length);
        header.SubCode = SubCode;
    }

    /// <summary>
    /// Gets the header type of this data packet.
    /// </summary>
    public static byte HeaderType => 0xC1;

    /// <summary>
    /// Gets the operation code of this data packet.
    /// </summary>
    public static byte Code => 0xD2;

    /// <summary>
    /// Gets the operation sub-code of this data packet.
    /// </summary>
    public static byte SubCode => 0x12;

    /// <summary>
    /// Gets the length of this data packet.
    /// </summary>
    public static int Length => 12;

    /// <summary>
    /// Gets the header of this packet.
    /// </summary>
    public C1HeaderWithSubCodeRef Header => new(this._data);

    /// <summary>
    /// Gets or sets the item code: the group times 512, plus the number.
    /// </summary>
    public ushort ItemCode
    {
        get => ReadUInt16LittleEndian(this._data[4..]);
        set => WriteUInt16LittleEndian(this._data[4..], value);
    }

    /// <summary>
    /// Gets or sets the slot of the item in the inventory, as the server numbers them: 0 to 11 equipped, then the bag.
    /// </summary>
    public ushort Slot
    {
        get => ReadUInt16LittleEndian(this._data[6..]);
        set => WriteUInt16LittleEndian(this._data[6..], value);
    }

    /// <summary>
    /// Gets or sets the expiry, as Unix time in seconds (UTC).
    /// </summary>
    public int ExpireTime
    {
        get => ReadInt32LittleEndian(this._data[8..]);
        set => WriteInt32LittleEndian(this._data[8..], value);
    }
}
