// <copyright file="BoxOfKundun.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

/// <summary>
/// The Box of Kundun, the prize of our events: the item 14/11, whose levels 8 to 12 are the boxes
/// +1 to +5. Its definition is named after its level 0, the Box of Luck.
/// </summary>
internal static class BoxOfKundun
{
    /// <summary>The item group of the box.</summary>
    public const byte Group = 14;

    /// <summary>The item number of the box.</summary>
    public const short Number = 11;

    /// <summary>The highest box, +5.</summary>
    public const int HighestPlus = 5;

    /// <summary>The item level of the box +1.</summary>
    private const byte PlusOneLevel = 8;

    /// <summary>
    /// Gets the item level of a box, e.g. 12 for the box +5.
    /// </summary>
    /// <param name="plus">The plus of the box, 1 to 5.</param>
    /// <returns>The item level.</returns>
    public static byte LevelOf(int plus) => (byte)(PlusOneLevel + plus - 1);

    /// <summary>
    /// Gets the plus of a box of the item level, e.g. 5 for the level 12.
    /// </summary>
    /// <param name="level">The item level.</param>
    /// <returns>The plus.</returns>
    public static int PlusOf(byte level) => level - PlusOneLevel + 1;

    /// <summary>
    /// Gets the name of a box for the messages, e.g. "Box of Kundun +5".
    /// </summary>
    /// <param name="plus">The plus of the box.</param>
    /// <returns>The name.</returns>
    public static string NameOf(int plus) => $"Box of Kundun +{plus}";
}
