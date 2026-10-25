// <copyright file="JewelCount.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// The jewels of the registro, by kind. A jewel counts one, and a packed jewel ten per level
/// (+0 is 10, +1 is 20 and +2 is 30), as in the weekly snapshot.
/// </summary>
/// <param name="Bless">The Jewels of Bless.</param>
/// <param name="Soul">The Jewels of Soul.</param>
/// <param name="Chaos">The Jewels of Chaos.</param>
/// <param name="Life">The Jewels of Life.</param>
/// <param name="Creation">The Jewels of Creation.</param>
internal readonly record struct JewelCount(int Bless, int Soul, int Chaos, int Life, int Creation)
{
    /// <summary>
    /// The jewels per level of a packed jewel: the jewel mix packs 10, 20 or 30 into the levels 0 to 2.
    /// </summary>
    internal const int JewelsPerPackLevel = 10;

    // The packed jewels of the jewel mix; GameLogic has no constants for them.
    private static readonly ItemIdentifier PackedBless = new(30, 12);
    private static readonly ItemIdentifier PackedSoul = new(31, 12);
    private static readonly ItemIdentifier PackedChaos = new(141, 12);
    private static readonly ItemIdentifier PackedLife = new(136, 12);
    private static readonly ItemIdentifier PackedCreation = new(137, 12);

    /// <summary>
    /// Adds two counts.
    /// </summary>
    /// <param name="left">The first count.</param>
    /// <param name="right">The second count.</param>
    /// <returns>The sum.</returns>
    public static JewelCount operator +(JewelCount left, JewelCount right)
        => new(left.Bless + right.Bless, left.Soul + right.Soul, left.Chaos + right.Chaos, left.Life + right.Life, left.Creation + right.Creation);

    /// <summary>
    /// Counts the jewels of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The jewels of the item; none, when it isn't a jewel.</returns>
    public static JewelCount Of(Item item)
    {
        if (item.Definition is not { } definition)
        {
            return default;
        }

        var id = new ItemIdentifier(definition.Number, definition.Group);
        var packed = JewelsPerPackLevel * (item.Level + 1);
        return id switch
        {
            _ when id == ItemConstants.JewelOfBless => new() { Bless = 1 },
            _ when id == PackedBless => new() { Bless = packed },
            _ when id == ItemConstants.JewelOfSoul => new() { Soul = 1 },
            _ when id == PackedSoul => new() { Soul = packed },
            _ when id == ItemConstants.JewelOfChaos => new() { Chaos = 1 },
            _ when id == PackedChaos => new() { Chaos = packed },
            _ when id == ItemConstants.JewelOfLife => new() { Life = 1 },
            _ when id == PackedLife => new() { Life = packed },
            _ when id == ItemConstants.JewelOfCreation => new() { Creation = 1 },
            _ when id == PackedCreation => new() { Creation = packed },
            _ => default,
        };
    }

    /// <summary>
    /// Counts the jewels of some items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The jewels of the items.</returns>
    public static JewelCount Of(IEnumerable<Item> items)
        => items.Aggregate(default(JewelCount), (count, item) => count + Of(item));

    /// <summary>
    /// Gets the count as the data of a registro event.
    /// </summary>
    /// <returns>The data, serialized as a JSON object.</returns>
    public object ToData() => new { bless = this.Bless, soul = this.Soul, chaos = this.Chaos, life = this.Life, creation = this.Creation };
}
