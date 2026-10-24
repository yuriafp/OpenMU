// <copyright file="StarterKitDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// Tests the starter kits against the item data of a new Season 6 database, as OpenMU creates it:
/// the server gives a kit all or none, and an item which the data doesn't allow stops the whole kit.
/// </summary>
[TestFixture]
public class StarterKitDataTest
{
    /// <summary>The base stats which a new character raises with its extra points, by the stat of the requirement.</summary>
    private static readonly Dictionary<AttributeDefinition, AttributeDefinition> BaseStats = new()
    {
        { Stats.TotalStrength, Stats.BaseStrength },
        { Stats.TotalAgility, Stats.BaseAgility },
        { Stats.TotalVitality, Stats.BaseVitality },
        { Stats.TotalEnergy, Stats.BaseEnergy },
        { Stats.TotalLeadership, Stats.BaseLeadership },
    };

    private IContext _context = null!;

    private GameConfiguration _configuration = null!;

    /// <summary>
    /// Creates the data of a new Season 6 database in memory.
    /// </summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task CreateSeasonSixDataAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        this._context = contextProvider.CreateNewContext();
        this._configuration = (await this._context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// Disposes the context.
    /// </summary>
    [OneTimeTearDown]
    public void DisposeContext() => this._context.Dispose();

    /// <summary>
    /// Every item of the kit exists with its level, its excellent options and the Luck where the
    /// kit wants it, and the class may wear it.
    /// </summary>
    /// <param name="classNumber">The number of the class.</param>
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    public void ItemsAreAllowedForTheClass(int classNumber)
    {
        var (characterClass, kit, items) = this.GiveKit(classNumber);

        Assert.Multiple(() =>
        {
            foreach (var (kitItem, item) in kit.Items.Zip(items))
            {
                var name = item.Definition!.Name.ValueInNeutralLanguage;
                Assert.That(item.Level, Is.EqualTo(kitItem.Level).And.LessThanOrEqualTo(item.Definition.MaximumItemLevel), name);
                Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(kitItem.ExcellentOptions.Count), name);
                Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck), Is.EqualTo(kitItem.Luck ? 1 : 0), name);
                Assert.That(item.Definition.QualifiedCharacters, Is.Empty.Or.Contains(characterClass), name);
            }
        });
    }

    /// <summary>
    /// The weapon leaves the hand of the shield free, so both can be equipped at once.
    /// </summary>
    /// <param name="classNumber">The number of the class with a shield in its kit.</param>
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(12)]
    [TestCase(16)]
    public async Task ShieldFitsBesideTheWeaponAsync(int classNumber)
    {
        var (_, _, items) = this.GiveKit(classNumber);
        // The equipped slots only, which come before the box of the bag.
        var equipment = new Storage(InventoryConstants.EquippableSlotsCount, InventoryConstants.EquippableSlotsCount, 0, this._context.CreateNew<ItemStorage>());
        var weapon = items[0];
        var shield = items.Single(i => i.Definition!.Group == 6);

        Assert.That(await equipment.AddItemAsync(InventoryConstants.LeftHandSlot, weapon).ConfigureAwait(false), Is.True);
        Assert.That(shield.Definition!.ConflictsWithEquippedHands(equipment, InventoryConstants.RightHandSlot), Is.False);
    }

    /// <summary>
    /// The kit fits into the empty inventory of a new character, and the character can wear all of
    /// it with its 500 extra points, at the level 1.
    /// </summary>
    /// <param name="classNumber">The number of the class.</param>
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    public async Task NewCharacterCanTakeAndWearTheKitAsync(int classNumber)
    {
        var (characterClass, _, items) = this.GiveKit(classNumber);
        var bag = new Storage(
            InventoryConstants.EquippableSlotsCount + (InventoryConstants.InventoryRows * InventoryConstants.RowSize),
            InventoryConstants.EquippableSlotsCount,
            0,
            this._context.CreateNew<ItemStorage>());
        foreach (var item in items)
        {
            Assert.That(await bag.AddItemAsync(item).ConfigureAwait(false), Is.True, item.Definition!.Name.ValueInNeutralLanguage);
        }

        var requirements = items
            .SelectMany(item => item.Definition!.Requirements.Select(requirement => item.GetRequirement(requirement)))
            .GroupBy(r => r.Attr, r => r.Value)
            .ToDictionary(g => g.Key, g => g.Max());
        var points = requirements
            .Where(r => BaseStats.ContainsKey(r.Key))
            .Sum(r => Math.Max(0, r.Value - (int)characterClass.StatAttributes.Single(a => a.Attribute == BaseStats[r.Key]).BaseValue));

        Assert.That(requirements.Keys.Where(a => !BaseStats.ContainsKey(a)), Is.All.EqualTo(Stats.Level));
        Assert.That(requirements.GetValueOrDefault(Stats.Level), Is.LessThanOrEqualTo(1));
        Assert.That(points, Is.LessThanOrEqualTo(new StarterKitConfiguration().LevelUpPoints));
    }

    private (CharacterClass CharacterClass, StarterKitDefinition Kit, List<Item> Items) GiveKit(int classNumber)
    {
        var characterClass = this._configuration.CharacterClasses.Single(c => c.Number == classNumber);
        var kit = StarterKitCatalog.Get(characterClass)!;
        var items = new List<Item>();
        foreach (var kitItem in kit.Items)
        {
            var item = this._context.CreateNew<Item>();
            StarterKitPlugIn.Configure(this._configuration, this._context, item, kitItem);
            items.Add(item);
        }

        return (characterClass, kit, items);
    }
}
