// <copyright file="StarterKitCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The starter kits per class (PLANO-SERVIDOR.md, 5.10): a weapon, a set and a shield, all +9 with
/// three excellent options and the Luck, the mini wings +9 and the Pet Panda. Each class gets another
/// set, weapon and shield, which a new character can wear with its 500 extra points.
/// </summary>
/// <remarks>
/// The Fairy Elf gets no shield: the bow takes its hand. The mini wings have no Luck, nor excellent
/// options, in the item data, and the Panda neither.
/// </remarks>
internal static class StarterKitCatalog
{
    /// <summary>The excellent options of the weapons: excellent damage, life after a kill and attack speed.</summary>
    private static readonly int[] WeaponOptions = [6, 2, 3];

    /// <summary>The excellent options of the bows: excellent damage, mana after a kill and attack speed.</summary>
    private static readonly int[] BowOptions = [6, 1, 3];

    /// <summary>The excellent options of the staffs: wizardry damage +2%, mana after a kill and attack speed.</summary>
    private static readonly int[] StaffOptions = [4, 1, 3];

    /// <summary>The excellent options of the armor and the shields: maximum life +4%, damage decrease 4% and defense rate +10%.</summary>
    private static readonly int[] ArmorOptions = [6, 4, 2];

    private static readonly StarterKitItem PetPanda = new(13, 80, 0, [], Luck: false);

    // The Skull Staff takes one column, so the Buckler fits beside it, unlike with a staff of two.
    private static readonly StarterKitDefinition DarkWizard = new(
        "Skull Staff, set Pad e Buckler",
        "Pequenas asas do paraíso",
        [new(5, 0, 9, StaffOptions, Luck: true), .. Set(2, withHelm: true), Shield(4), Wings(133), PetPanda]);

    private static readonly StarterKitDefinition DarkKnight = new(
        "Katache, set Leather e Skull Shield",
        "Pequenas asas de Satã",
        [new(0, 3, 9, WeaponOptions, Luck: true), .. Set(5, withHelm: true), Shield(6), Wings(134), PetPanda]);

    private static readonly StarterKitDefinition FairyElf = new(
        "Elven Bow e set Vine",
        "Pequenas asas do elfo",
        [new(4, 2, 9, BowOptions, Luck: true), .. Set(10, withHelm: true), Wings(132), PetPanda]);

    // The Magic Gladiator wears no helm, and Bone has the next drop level of the sets of the others.
    // Of the mini wings it may wear, those of Satan have the higher defense.
    private static readonly StarterKitDefinition MagicGladiator = new(
        "Sword of Assassin, set Bone e Kite Shield",
        "Pequenas asas de Satã",
        [new(0, 4, 9, WeaponOptions, Luck: true), .. Set(4, withHelm: false), Shield(2), Wings(134), PetPanda]);

    private static readonly StarterKitDefinition DarkLord = new(
        "Morning Star, set Bronze e Big Round Shield",
        "Pequena capa do Lorde",
        [new(2, 1, 9, WeaponOptions, Luck: true), .. Set(0, withHelm: true), Shield(10), Wings(130), PetPanda]);

    /// <summary>
    /// Gets the kit of the class of the character, or <c>null</c> for a class without a kit.
    /// </summary>
    /// <param name="characterClass">The class of the character.</param>
    /// <returns>The kit, or <c>null</c>.</returns>
    public static StarterKitDefinition? Get(CharacterClass? characterClass)
        => characterClass is null ? null : (characterClass.Number / 4) switch
        {
            // The classes of a line share the first number of a block of four, e.g. 4 to 7 for the knights.
            0 => DarkWizard,
            1 => DarkKnight,
            2 => FairyElf,
            3 => MagicGladiator,
            4 => DarkLord,
            _ => null,
        };

    private static IEnumerable<StarterKitItem> Set(short number, bool withHelm)
    {
        // Helm, armor, pants, gloves and boots are the groups 7 to 11.
        for (byte group = withHelm ? (byte)7 : (byte)8; group <= 11; group++)
        {
            yield return new StarterKitItem(group, number, 9, ArmorOptions, Luck: true);
        }
    }

    /// <summary>
    /// Gets a shield of the group 6. Each is of the drop level of the weapon or the set of its kit.
    /// </summary>
    private static StarterKitItem Shield(short number) => new(6, number, 9, ArmorOptions, Luck: true);

    /// <summary>
    /// Gets mini wings of the group 12, which need the level 1 only.
    /// </summary>
    private static StarterKitItem Wings(short number) => new(12, number, 9, [], Luck: false);
}
