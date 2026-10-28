// <copyright file="WeeklyGoalRef.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using System.Text;
using MUnique.OpenMU.Network.Packets;
using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// Is sent by the server when: a player asks for the weekly goal with <c>/meta</c>.
/// Causes reaction on client side: the client opens the window of the weekly goal, with its progress bar.
/// </summary>
/// <remarks>
/// A packet of our MuMain client, like <see cref="PeriodItems.PeriodItemRef"/>. The head code 0xFB is
/// free in OpenMU and MuMain. It has the kind of the goal and the amounts of the reward, which the
/// client names in its language; the name of the goal is for a kind which the client doesn't know.
/// It's UTF-8, cut at a whole character and ended by a zero.
/// </remarks>
internal readonly ref struct WeeklyGoalRef
{
    /// <summary>The length of the name of the goal, with its ending zero.</summary>
    internal const int NameLength = 64;

    /// <summary>Where the name of the goal starts.</summary>
    internal const int NameOffset = 36;

    /// <summary>The bits which tell a following byte of a UTF-8 character: <c>10xxxxxx</c>.</summary>
    private const byte Utf8ContinuationMask = 0xC0;

    /// <summary>The value of the <see cref="Utf8ContinuationMask"/> bits of a following byte.</summary>
    private const byte Utf8Continuation = 0x80;

    private readonly Span<byte> _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeeklyGoalRef"/> struct, and writes its header.
    /// </summary>
    /// <param name="data">The underlying data.</param>
    public WeeklyGoalRef(Span<byte> data)
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
    internal enum WeeklyGoalFlags : byte
    {
        /// <summary>No flags: the week has no goal.</summary>
        None = 0,

        /// <summary>The week has a goal.</summary>
        HasGoal = 1,

        /// <summary>The goal is reached.</summary>
        Reached = 2,

        /// <summary>The account of the player got the reward of the week.</summary>
        Rewarded = 4,
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
    public static byte SubCode => 0x01;

    /// <summary>
    /// Gets the length of this data packet.
    /// </summary>
    public static int Length => NameOffset + NameLength;

    /// <summary>
    /// Gets the header of this packet.
    /// </summary>
    public C1HeaderWithSubCodeRef Header => new(this._data);

    /// <summary>
    /// Gets or sets the flags.
    /// </summary>
    public WeeklyGoalFlags Flags
    {
        get => (WeeklyGoalFlags)this._data[4];
        set => this._data[4] = (byte)value;
    }

    /// <summary>
    /// Gets or sets the experience bonus for all, in percent, once the goal is reached.
    /// </summary>
    public byte ExperienceBonusPercent
    {
        get => this._data[5];
        set => this._data[5] = value;
    }

    /// <summary>
    /// Gets or sets what counts for the goal.
    /// </summary>
    public WeeklyGoalKind Kind
    {
        get => (WeeklyGoalKind)this._data[6];
        set => this._data[6] = (byte)value;
    }

    /// <summary>
    /// Gets or sets the plus of the Box of Kundun of the reward, or 0 for none.
    /// </summary>
    public byte BoxOfKundunPlus
    {
        get => this._data[7];
        set => this._data[7] = value;
    }

    /// <summary>
    /// Gets or sets the count of the week.
    /// </summary>
    public int Count
    {
        get => ReadInt32LittleEndian(this._data[8..]);
        set => WriteInt32LittleEndian(this._data[8..], value);
    }

    /// <summary>
    /// Gets or sets the count which reaches the goal.
    /// </summary>
    public int Target
    {
        get => ReadInt32LittleEndian(this._data[12..]);
        set => WriteInt32LittleEndian(this._data[12..], value);
    }

    /// <summary>
    /// Gets or sets what the account of the player counted.
    /// </summary>
    public int Contribution
    {
        get => ReadInt32LittleEndian(this._data[16..]);
        set => WriteInt32LittleEndian(this._data[16..], value);
    }

    /// <summary>
    /// Gets or sets the end of the week, as Unix time in seconds (UTC).
    /// </summary>
    public int WeekEnd
    {
        get => ReadInt32LittleEndian(this._data[20..]);
        set => WriteInt32LittleEndian(this._data[20..], value);
    }

    /// <summary>
    /// Gets or sets the Jewels of Bless of the reward.
    /// </summary>
    public int JewelsOfBless
    {
        get => ReadInt32LittleEndian(this._data[24..]);
        set => WriteInt32LittleEndian(this._data[24..], value);
    }

    /// <summary>
    /// Gets or sets the Jewels of Soul of the reward.
    /// </summary>
    public int JewelsOfSoul
    {
        get => ReadInt32LittleEndian(this._data[28..]);
        set => WriteInt32LittleEndian(this._data[28..], value);
    }

    /// <summary>
    /// Gets or sets the Goblin Points of the reward.
    /// </summary>
    public int GoblinPoints
    {
        get => ReadInt32LittleEndian(this._data[32..]);
        set => WriteInt32LittleEndian(this._data[32..], value);
    }

    /// <summary>
    /// Gets or sets the name of the goal, e.g. "Blood Castles completos".
    /// </summary>
    public string Name
    {
        get => ReadText(this._data.Slice(NameOffset, NameLength));
        set => WriteText(this._data.Slice(NameOffset, NameLength), value);
    }

    private static string ReadText(ReadOnlySpan<byte> field)
    {
        var end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    private static void WriteText(Span<byte> field, string? text)
    {
        field.Clear();
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);

        // One byte stays for the ending zero, and a cut never splits a character.
        var length = Math.Min(bytes.Length, field.Length - 1);
        while (length > 0 && length < bytes.Length && (bytes[length] & Utf8ContinuationMask) == Utf8Continuation)
        {
            length--;
        }

        bytes.AsSpan(0, length).CopyTo(field);
    }
}
