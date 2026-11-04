// <copyright file="BankJewel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// A jewel which the Banqueira keeps: the jewel itself and its pack of the jewel mix, which holds
/// 10, 20 or 30 jewels in its levels 0 to 2.
/// </summary>
/// <param name="Name">The name of the jewel in <c>tbt.jewel_bank</c>; it's persisted, so it never changes.</param>
/// <param name="Group">The item group of the jewel.</param>
/// <param name="Number">The item number of the jewel.</param>
/// <param name="PackedNumber">The item number of the pack, in the item group 12.</param>
internal sealed record BankJewel(string Name, byte Group, short Number, short PackedNumber)
{
    /// <summary>The item group of the packs.</summary>
    internal const byte PackedGroup = 12;

    /// <summary>The jewels per level of a pack: the levels 0 to 2 hold 10, 20 and 30.</summary>
    internal const int JewelsPerPackLevel = 10;

    /// <summary>The highest level of a pack.</summary>
    internal const byte MaximumPackLevel = 2;

    /// <summary>
    /// Gets the jewels, in the order of the window of the Banqueira. The position is the index of the
    /// jewel in the packets C1 FB 02 and C1 FB 03, so a new jewel goes to the end.
    /// </summary>
    public static IReadOnlyList<BankJewel> All { get; } =
    [
        new("Bless", 14, 13, 30),
        new("Soul", 14, 14, 31),
        new("Life", 14, 16, 136),
        new("Chaos", 12, 15, 141),
        new("Creation", 14, 22, 137),
        new("Guardian", 14, 31, 138),
        new("Harmony", 14, 42, 140),
        new("Gemstone", 14, 41, 139),
        new("LowerRefineStone", 14, 43, 142),
        new("HigherRefineStone", 14, 44, 143),
    ];

    /// <summary>
    /// Gets the jewel of an item, and how many jewels it is.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The jewel and the jewels of the item: 1 for a jewel, 10, 20 or 30 for a pack; <c>null</c>, if the Banqueira doesn't keep it.</returns>
    public static (BankJewel Jewel, int Amount)? Of(Item item)
    {
        if (item.Definition is not { } definition)
        {
            return null;
        }

        foreach (var jewel in All)
        {
            if (definition.Group == jewel.Group && definition.Number == jewel.Number)
            {
                return (jewel, 1);
            }

            if (definition.Group == PackedGroup && definition.Number == jewel.PackedNumber && item.Level <= MaximumPackLevel)
            {
                return (jewel, JewelsPerPackLevel * (item.Level + 1));
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the jewel by its name in <c>tbt.jewel_bank</c>.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The jewel; <c>null</c> for a name which this version doesn't know.</returns>
    public static BankJewel? ByName(string name) => All.FirstOrDefault(j => j.Name == name);

    /// <summary>
    /// Gets the item which holds an amount of this jewel: the jewel for 1, a pack for 10, 20 or 30.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The item group, number and level; <c>null</c> for an amount which no item holds.</returns>
    public (byte Group, short Number, byte Level)? ItemFor(int amount) => amount switch
    {
        1 => (this.Group, this.Number, 0),
        _ when amount % JewelsPerPackLevel == 0 && amount / JewelsPerPackLevel is >= 1 and <= MaximumPackLevel + 1
            => (PackedGroup, this.PackedNumber, (byte)((amount / JewelsPerPackLevel) - 1)),
        _ => null,
    };
}
