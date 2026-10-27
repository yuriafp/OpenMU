// <copyright file="BossInvasionTheme.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

/// <summary>
/// A theme of the boss invasion: bosses of one origin with an escort of their own, e.g. Balgass and
/// the Dark Elf with the werewolves in Crywolf.
/// </summary>
public class BossInvasionTheme
{
    /// <summary>
    /// Gets or sets the name of the theme in the messages, e.g. "Cerco de Crywolf".
    /// </summary>
    [Display(Name = "Name", Description = "The name of the theme in the messages, e.g. \"Cerco de Crywolf\".", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bosses and their escorts.
    /// </summary>
    [Display(Name = "Spawns", Description = "The bosses and their escorts.", Order = 2)]
    public IList<BossInvasionSpawn> Spawns { get; set; } = new List<BossInvasionSpawn>();

    /// <summary>
    /// Creates a boss, with the health of its definition times the boss health multiplier.
    /// </summary>
    /// <param name="monsterNumber">The number of the monster definition.</param>
    /// <param name="map">The number of the map.</param>
    /// <param name="prizeBoxes">The number of prize boxes.</param>
    /// <param name="prizeBoxPlus">The plus of the prize boxes.</param>
    /// <returns>The boss.</returns>
    internal static BossInvasionSpawn Boss(short monsterNumber, short map, int prizeBoxes, int prizeBoxPlus) => new()
    {
        MonsterNumber = monsterNumber,
        Map = map,
        IsBoss = true,
        PrizeBoxes = prizeBoxes,
        PrizeBoxPlus = prizeBoxPlus,
    };

    /// <summary>
    /// Creates a boss which uses skills, with the health of its definition times the boss health multiplier.
    /// </summary>
    /// <param name="monsterNumber">The number of the monster definition.</param>
    /// <param name="map">The number of the map.</param>
    /// <param name="prizeBoxes">The number of prize boxes.</param>
    /// <param name="prizeBoxPlus">The plus of the prize boxes.</param>
    /// <returns>The boss.</returns>
    internal static BossInvasionSpawn BossWithSkills(short monsterNumber, short map, int prizeBoxes, int prizeBoxPlus)
    {
        var boss = Boss(monsterNumber, map, prizeBoxes, prizeBoxPlus);
        boss.HasSkills = true;
        return boss;
    }

    /// <summary>
    /// Creates escorts, with the health of their definition.
    /// </summary>
    /// <param name="monsterNumber">The number of the monster definition.</param>
    /// <param name="map">The number of the map.</param>
    /// <param name="count">The number of monsters.</param>
    /// <returns>The escorts.</returns>
    internal static BossInvasionSpawn Escort(short monsterNumber, short map, int count) => new()
    {
        MonsterNumber = monsterNumber,
        Map = map,
        Count = count,
    };

    /// <summary>
    /// Creates escorts which come only when the boss with skills of their map calls them.
    /// </summary>
    /// <param name="monsterNumber">The number of the monster definition.</param>
    /// <param name="map">The number of the map.</param>
    /// <param name="count">The number of monsters of each call.</param>
    /// <returns>The escorts.</returns>
    internal static BossInvasionSpawn CalledEscort(short monsterNumber, short map, int count)
    {
        var escort = Escort(monsterNumber, map, count);
        escort.OnlyCalled = true;
        return escort;
    }
}
