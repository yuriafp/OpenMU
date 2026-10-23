// <copyright file="PeriodItemCountRef.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.PeriodItems;

using MUnique.OpenMU.Network.Packets;

/// <summary>
/// Is sent by the server when: the items with an expiry follow, as <see cref="PeriodItemRef"/>.
/// Causes reaction on client side: the client expects <see cref="Count"/> packets of <see cref="PeriodItemRef"/>.
/// </summary>
/// <remarks>
/// The packet of the classic period item list, as agreed with our MuMain client. OpenMU doesn't
/// define it (src/Network/Packets), so it's written like the structures generated there.
/// </remarks>
internal readonly ref struct PeriodItemCountRef
{
    private readonly Span<byte> _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodItemCountRef"/> struct, and writes its header.
    /// </summary>
    /// <param name="data">The underlying data.</param>
    public PeriodItemCountRef(Span<byte> data)
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
    public static byte SubCode => 0x11;

    /// <summary>
    /// Gets the length of this data packet.
    /// </summary>
    public static int Length => 5;

    /// <summary>
    /// Gets the header of this packet.
    /// </summary>
    public C1HeaderWithSubCodeRef Header => new(this._data);

    /// <summary>
    /// Gets or sets the number of the <see cref="PeriodItemRef"/> which follow.
    /// </summary>
    public byte Count
    {
        get => this._data[4];
        set => this._data[4] = value;
    }
}
