// <copyright file="BossInvasionSpawn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

/// <summary>
/// The monsters of one kind which a theme of the boss invasion puts on a map: a boss, or its escort.
/// The escorts of a map appear around the first boss of that map.
/// </summary>
public class BossInvasionSpawn
{
    /// <summary>
    /// Gets or sets the number of the monster definition, e.g. Balgass (349).
    /// </summary>
    [Display(Name = "Monster Number", Description = "The number of the monster definition, e.g. Balgass (349).", Order = 1)]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of the map, e.g. Crywolf (34).
    /// </summary>
    /// <remarks>
    /// A <see cref="short"/>, like the maps of the treasure chest: the admin panel has no field for
    /// <see cref="ushort"/>.
    /// </remarks>
    [Display(Name = "Map", Description = "The number of the map, e.g. Crywolf (34).", Order = 2)]
    public short Map { get; set; }

    /// <summary>
    /// Gets or sets the number of monsters.
    /// </summary>
    [Display(Name = "Count", Description = "The number of monsters.", Order = 3)]
    [Range(1, 50)]
    public int Count { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether it's a boss: the escorts gather around it, its death is
    /// announced and gives the prize.
    /// </summary>
    [Display(Name = "Is Boss", Description = "A boss: the escorts of its map gather around it, its death is announced and gives the prize.", Order = 4)]
    public bool IsBoss { get; set; }

    /// <summary>
    /// Gets or sets the health of each monster; 0 keeps the health of its definition, which a boss gets
    /// times the <see cref="BossInvasionConfiguration.BossHealthMultiplier"/>.
    /// </summary>
    [Display(Name = "Health", Description = "The health of each monster; 0 keeps the health of its definition, which a boss gets times the Boss Health Multiplier.", Order = 5)]
    [Range(0, int.MaxValue)]
    public int Health { get; set; }

    /// <summary>
    /// Gets or sets the number of Boxes of Kundun which a boss drops for the party which kills it.
    /// </summary>
    [Display(Name = "Prize Boxes", Description = "The number of Boxes of Kundun which a boss drops for the party which kills it.", Order = 6)]
    [Range(0, 10)]
    public int PrizeBoxes { get; set; }

    /// <summary>
    /// Gets or sets the plus of the prize boxes, 1 to 5.
    /// </summary>
    [Display(Name = "Prize Box Plus", Description = "The plus of the prize boxes, 1 to 5.", Order = 7)]
    [Range(1, BoxOfKundun.HighestPlus)]
    public int PrizeBoxPlus { get; set; } = BoxOfKundun.HighestPlus;

    /// <summary>
    /// Gets or sets a value indicating whether a boss uses skills: it hits around itself, poisons and
    /// freezes, calls the escort of its map again and rages, as the boss skills of the configuration say.
    /// </summary>
    /// <remarks>
    /// Our client shows the skills of our bosses from the Season 8 client (1003 to 1009) only; another
    /// boss hits with them all the same, without their effects.
    /// </remarks>
    [Display(Name = "Has Skills", Description = "A boss which uses skills: it hits around itself, poisons and freezes, calls the escort of its map again and rages, as the boss skills of the configuration say. Our client shows them for our Season 8 bosses (1003 to 1009) only.", Order = 8)]
    public bool HasSkills { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an escort comes only when a boss with skills of its map
    /// calls it, and not with the bosses.
    /// </summary>
    [Display(Name = "Only Called", Description = "An escort which comes only when a boss with skills of its map calls it, and not with the bosses.", Order = 9)]
    public bool OnlyCalled { get; set; }
}
