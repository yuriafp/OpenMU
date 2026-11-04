// <copyright file="JewelBankContentsRef.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using MUnique.OpenMU.Network.Packets;
using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// Is sent by the server when: a player talks to the Banqueira, and after each request of her window.
/// Causes reaction on client side: the client opens the window of the Banqueira, or updates it, with
/// the jewels which the account keeps and the fees.
/// </summary>
/// <remarks>
/// A packet of our MuMain client, like <see cref="WeeklyGoal.WeeklyGoalRef"/> (C1 FB 01). Each jewel
/// is its item group, item number and amount, so the client names it with its own item names. The
/// position of a jewel is its index in the request C1 FB 03 (<see cref="JewelBankRequest"/>).
/// </remarks>
internal readonly ref struct JewelBankContentsRef
{
    /// <summary>Where the jewels start.</summary>
    internal const int JewelsOffset = 16;

    /// <summary>The length of one jewel.</summary>
    internal const int JewelLength = 8;

    private readonly Span<byte> _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankContentsRef"/> struct, and writes its header.
    /// </summary>
    /// <param name="data">The underlying data.</param>
    public JewelBankContentsRef(Span<byte> data)
    {
        this._data = data;
        var header = this.Header;
        header.Type = HeaderType;
        header.Code = Code;
        header.Length = (byte)Math.Min(data.Length, Length);
        header.SubCode = SubCode;
    }

    /// <summary>
    /// The flags of the packet.
    /// </summary>
    [Flags]
    internal enum JewelBankFlags : byte
    {
        /// <summary>No flags: an update of an open window.</summary>
        None = 0,

        /// <summary>The client opens the window.</summary>
        Open = 1,
    }

    /// <summary>
    /// Gets the header type of this data packet.
    /// </summary>
    public static byte HeaderType => 0xC1;

    /// <summary>
    /// Gets the operation code of this data packet.
    /// </summary>
    public static byte Code => 0xFB;

    /// <summary>
    /// Gets the operation sub-code of this data packet.
    /// </summary>
    public static byte SubCode => 0x02;

    /// <summary>
    /// Gets the length of this data packet, with all jewels which the Banqueira keeps.
    /// </summary>
    public static int Length => JewelsOffset + (BankJewel.All.Count * JewelLength);

    /// <summary>
    /// Gets the header of this packet.
    /// </summary>
    public C1HeaderWithSubCodeRef Header => new(this._data);

    /// <summary>
    /// Gets or sets the flags.
    /// </summary>
    public JewelBankFlags Flags
    {
        get => (JewelBankFlags)this._data[4];
        set => this._data[4] = (byte)value;
    }

    /// <summary>
    /// Gets or sets the number of jewels in the packet.
    /// </summary>
    public byte JewelCount
    {
        get => this._data[5];
        set => this._data[5] = value;
    }

    /// <summary>
    /// Gets or sets the fee to keep one jewel, in zen.
    /// </summary>
    public int DepositFeePerJewel
    {
        get => ReadInt32LittleEndian(this._data[8..]);
        set => WriteInt32LittleEndian(this._data[8..], value);
    }

    /// <summary>
    /// Gets or sets the fee to take out one jewel, in zen.
    /// </summary>
    public int WithdrawalFeePerJewel
    {
        get => ReadInt32LittleEndian(this._data[12..]);
        set => WriteInt32LittleEndian(this._data[12..], value);
    }

    /// <summary>
    /// Writes a jewel: its item group (byte 0), its item number (bytes 2 and 3) and its amount (bytes 4 to 7).
    /// </summary>
    /// <param name="index">The index of the jewel.</param>
    /// <param name="jewel">The jewel.</param>
    /// <param name="amount">The amount which the account keeps.</param>
    public void SetJewel(int index, BankJewel jewel, long amount)
    {
        var data = this._data.Slice(JewelsOffset + (index * JewelLength), JewelLength);
        data[0] = jewel.Group;
        data[1] = 0;
        WriteInt16LittleEndian(data[2..], jewel.Number);
        WriteInt32LittleEndian(data[4..], (int)Math.Clamp(amount, 0, int.MaxValue));
    }

    /// <summary>
    /// Gets the amount of a jewel.
    /// </summary>
    /// <param name="index">The index of the jewel.</param>
    /// <returns>The amount.</returns>
    public int GetAmount(int index) => ReadInt32LittleEndian(this._data[(JewelsOffset + (index * JewelLength) + 4)..]);
}
