// <copyright file="StarterKitCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The starter kits per class (PLANO-SERVIDOR.md, 5.10): a weapon and a set, all +9 with three
/// excellent options and without Luck, and the Pet Panda. Each class gets another set and another
/// weapon, which a new character can wear with its 500 extra points.
/// </summary>
internal static class StarterKitCatalog
{
    /// <summary>The excellent options of the weapons: excellent damage, life after a kill and attack speed.</summary>
    private static readonly int[] WeaponOptions = [6, 2, 3];

    /// <summary>The excellent options of the bows: excellent damage, mana after a kill and attack speed.</summary>
    private static readonly int[] BowOptions = [6, 1, 3];

    /// <summary>The excellent options of the staffs: wizardry damage +2%, mana after a kill and attack speed.</summary>
    private static readonly int[] StaffOptions = [4, 1, 3];

    /// <summary>The excellent options of the armor: maximum life +4%, damage decrease 4% and defense rate +10%.</summary>
    private static readonly int[] ArmorOptions = [6, 4, 2];

    private static readonly StarterKitItem PetPanda = new(13, 80, 0, []);

    private static readonly StarterKitDefinition DarkWizard = new(
        "Angelic Staff e set Pad",
        [new(5, 1, 9, StaffOptions), .. Set(2, withHelm: true), PetPanda]);

    private static readonly StarterKitDefinition DarkKnight = new(
        "Katache e set Leather",
        [new(0, 3, 9, WeaponOptions), .. Set(5, withHelm: true), PetPanda]);

    private static readonly StarterKitDefinition FairyElf = new(
        "Elven Bow e set Vine",
        [new(4, 2, 9, BowOptions), .. Set(10, withHelm: true), PetPanda]);

    // The Magic Gladiator wears no helm, and Bone has the next drop level of the sets of the others.
    private static readonly StarterKitDefinition MagicGladiator = new(
        "Sword of Assassin e set Bone",
        [new(0, 4, 9, WeaponOptions), .. Set(4, withHelm: false), PetPanda]);

    private static readonly StarterKitDefinition DarkLord = new(
        "Morning Star e set Bronze",
        [new(2, 1, 9, WeaponOptions), .. Set(0, withHelm: true), PetPanda]);

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
            yield return new StarterKitItem(group, number, 9, ArmorOptions);
        }
    }
}
