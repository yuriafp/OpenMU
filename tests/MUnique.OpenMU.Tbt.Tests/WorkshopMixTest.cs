// <copyright file="WorkshopMixTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using System.Text.Json.Serialization;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Workshop;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the rules of the Oficina (<see cref="WorkshopCraftingHandler.GetMix"/>).
/// </summary>
[TestFixture]
public class WorkshopMixTest
{
    private const byte LuckChoice = 0;

    private readonly WorkshopConfiguration _configuration = new();

    private BasicModel.IncreasableItemOption _luck = null!;

    private BasicModel.ItemOptionDefinition _excellentOptions = null!;

    private BasicModel.ItemDefinition _sword = null!;

    private BasicModel.ItemDefinition _soul = null!;

    private BasicModel.ItemDefinition _creation = null!;

    private BasicModel.ItemDefinition _bless = null!;

    private BasicModel.ItemOptionDefinition _secondWingOptions = null!;

    private BasicModel.ItemDefinition _firstWing = null!;

    private BasicModel.ItemDefinition _secondWing = null!;

    private BasicModel.ItemDefinition _thirdWing = null!;

    private BasicModel.ItemDefinition _capeOfLord = null!;

    private BasicModel.ItemDefinition _ring = null!;

    /// <summary>
    /// Sets up a sword with the Luck and six excellent options, wings and capes with the Luck and
    /// their wing options, a ring and the jewels.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._luck = new BasicModel.IncreasableItemOption { OptionType = ItemOptionTypes.Luck, Number = 0 };
        var luckOptions = new BasicModel.ItemOptionDefinition();
        luckOptions.PossibleOptions.Add(this._luck);

        this._excellentOptions = new BasicModel.ItemOptionDefinition { MaximumOptionsPerItem = 2 };
        for (var number = 1; number <= 6; number++)
        {
            this._excellentOptions.PossibleOptions.Add(new BasicModel.IncreasableItemOption { OptionType = ItemOptionTypes.Excellent, Number = number });
        }

        this._sword = new BasicModel.ItemDefinition { Group = 0, Number = 1, Name = "Short Sword" };
        this._sword.PossibleItemOptions.Add(luckOptions);
        this._sword.PossibleItemOptions.Add(this._excellentOptions);

        var wingsSlot = new BasicModel.ItemSlotType();
        wingsSlot.ItemSlots.Add(InventoryConstants.WingsSlot);
        this._secondWingOptions = CreateWingOptions(3);
        this._firstWing = CreateWing(12, 0, "Wings of Elf", wingsSlot, luckOptions, null);
        this._secondWing = CreateWing(12, 3, "Wings of Spirits", wingsSlot, luckOptions, this._secondWingOptions);
        this._thirdWing = CreateWing(12, 36, "Wing of Storm", wingsSlot, luckOptions, CreateWingOptions(4));
        this._capeOfLord = CreateWing(13, 30, "Cape of Lord", wingsSlot, luckOptions, CreateWingOptions(4));

        var ringSlot = new BasicModel.ItemSlotType();
        ringSlot.ItemSlots.Add(InventoryConstants.Ring1Slot);
        this._ring = new BasicModel.ItemDefinition { Group = 13, Number = 8, Name = "Ring of Ice", ItemSlot = ringSlot };
        this._ring.PossibleItemOptions.Add(luckOptions);

        this._soul = new BasicModel.ItemDefinition { Group = 14, Number = 14, Name = "Jewel of Soul" };
        this._creation = new BasicModel.ItemDefinition { Group = 14, Number = 22, Name = "Jewel of Creation" };
        this._bless = new BasicModel.ItemDefinition { Group = 14, Number = 13, Name = "Jewel of Bless" };
    }

    /// <summary>
    /// The Luck takes one Jewel of Soul and the price of the Luck.
    /// </summary>
    [Test]
    public void LuckWithOneSoul()
    {
        var sword = this.Sword();
        var soul = this.Item(this._soul);

        var mix = this.Mix(LuckChoice, sword, soul);

        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
        Assert.That(mix.Item, Is.SameAs(sword));
        Assert.That(mix.Option, Is.SameAs(this._luck));
        Assert.That(mix.Jewels, Is.EqualTo(new[] { soul }));
        Assert.That(mix.Price, Is.EqualTo(this._configuration.LuckZen));
        Assert.That(mix.SuccessRate, Is.EqualTo(this._configuration.LuckSuccessRate));
    }

    /// <summary>
    /// The Luck without the Jewel of Soul lacks items, and with two it's incorrect, so it consumes nothing.
    /// </summary>
    /// <param name="souls">The number of Jewels of Soul.</param>
    /// <param name="expected">The expected error.</param>
    [TestCase(0, CraftingResult.LackingMixItems)]
    [TestCase(2, CraftingResult.IncorrectMixItems)]
    public void LuckWithWrongNumberOfSouls(int souls, CraftingResult expected)
    {
        var items = new List<Item> { this.Sword() };
        items.AddRange(Enumerable.Range(0, souls).Select(_ => this.Item(this._soul)));

        var mix = this.Mix(LuckChoice, items.ToArray());

        Assert.That(mix?.Error, Is.EqualTo(expected));
    }

    /// <summary>
    /// An item with the Luck doesn't get it again.
    /// </summary>
    [Test]
    public void LuckTwice()
    {
        var sword = this.Sword();
        sword.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = this._luck });

        Assert.That(this.Mix(LuckChoice, sword, this.Item(this._soul)), Is.Null);
    }

    /// <summary>
    /// The Luck with a Jewel of Creation in the mix doesn't fit.
    /// </summary>
    [Test]
    public void LuckWithCreation()
    {
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Item(this._soul), this.Item(this._creation)), Is.Null);
    }

    /// <summary>
    /// The first excellent option takes one Jewel of Creation; the third takes three and costs three times the price.
    /// </summary>
    /// <param name="existingOptions">The number of excellent options of the item.</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ExcellentOption(int existingOptions)
    {
        var sword = this.Sword(existingOptions: existingOptions);
        var creations = Enumerable.Range(0, existingOptions + 1).Select(_ => this.Item(this._creation)).ToList();
        var mix = this.Mix(6, [sword, .. creations]);

        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
        Assert.That(mix.Option.Number, Is.EqualTo(6));
        Assert.That(mix.Jewels, Is.EquivalentTo(creations));
        Assert.That(mix.Price, Is.EqualTo((existingOptions + 1) * this._configuration.ExcellentZenPerOption));
        Assert.That(mix.SuccessRate, Is.EqualTo(this._configuration.ExcellentSuccessRate));
    }

    /// <summary>
    /// The second excellent option with one Jewel of Creation lacks items, and with three it's incorrect.
    /// </summary>
    /// <param name="creations">The number of Jewels of Creation.</param>
    /// <param name="expected">The expected error.</param>
    [TestCase(1, CraftingResult.LackingMixItems)]
    [TestCase(3, CraftingResult.IncorrectMixItems)]
    public void ExcellentOptionWithWrongNumberOfCreations(int creations, CraftingResult expected)
    {
        var items = new List<Item> { this.Sword(existingOptions: 1) };
        items.AddRange(Enumerable.Range(0, creations).Select(_ => this.Item(this._creation)));

        Assert.That(this.Mix(6, items.ToArray())?.Error, Is.EqualTo(expected));
    }

    /// <summary>
    /// An item with three excellent options gets no fourth, even though the option is valid.
    /// </summary>
    [Test]
    public void FourthExcellentOption()
    {
        var items = new List<Item> { this.Sword(existingOptions: 3) };
        items.AddRange(Enumerable.Range(0, 4).Select(_ => this.Item(this._creation)));

        Assert.That(this.Mix(6, items.ToArray()), Is.Null);
    }

    /// <summary>
    /// An option which the item has already, or which doesn't exist, doesn't fit.
    /// </summary>
    /// <param name="choice">The choice.</param>
    [TestCase(1)]
    [TestCase(7)]
    public void UnavailableExcellentOption(int choice)
    {
        Assert.That(this.Mix((byte)choice, this.Sword(existingOptions: 1), this.Item(this._creation), this.Item(this._creation)), Is.Null);
    }

    /// <summary>
    /// An item above the maximum level is refused with the level error, before the jewels.
    /// </summary>
    [Test]
    public void ItemAboveMaximumLevel()
    {
        Assert.That(this.Mix(LuckChoice, this.Sword(level: 12), this.Item(this._soul))?.Error, Is.EqualTo(CraftingResult.InvalidItemLevel));
        Assert.That(this.Mix(LuckChoice, this.Sword(level: 11), this.Item(this._soul))?.Error, Is.Null);
    }

    /// <summary>
    /// Anything which isn't one weapon, armor, shield or wing with the jewels doesn't fit.
    /// </summary>
    [Test]
    public void ItemsWhichDontFit()
    {
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Sword(), this.Item(this._soul)), Is.Null, "two items");
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Item(this._soul), this.Item(this._bless)), Is.Null, "another jewel");
        Assert.That(this.Mix(LuckChoice, this.Item(this._soul)), Is.Null, "no item");
        Assert.That(this.Mix(LuckChoice, this.Item(this._ring), this.Item(this._soul)), Is.Null, "a ring of the group 13");
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Item(this._secondWing), this.Item(this._soul)), Is.Null, "an item and a wing");

        var socketSword = this.Sword();
        socketSword.SocketCount = 1;
        Assert.That(this.Mix(LuckChoice, socketSword, this.Item(this._soul)), Is.Null, "socket item");

        var ancientSword = this.Sword();
        ancientSword.ItemSetGroups.Add(new BasicModel.ItemOfItemSet());
        Assert.That(this.Mix(LuckChoice, ancientSword, this.Item(this._soul)), Is.Null, "ancient item");
    }

    /// <summary>
    /// The wings and the capes get the Luck as the other items, also the first wings, which have no wing option.
    /// </summary>
    [Test]
    public void LuckOnWings()
    {
        foreach (var definition in new[] { this._firstWing, this._secondWing, this._thirdWing, this._capeOfLord })
        {
            var wing = this.Item(definition);
            var mix = this.Mix(LuckChoice, wing, this.Item(this._soul));

            Assert.That(mix, Is.Not.Null, definition.Name.ValueInNeutralLanguage);
            Assert.That(mix!.Error, Is.Null);
            Assert.That(mix.Option, Is.SameAs(this._luck));
            Assert.That(mix.Price, Is.EqualTo(this._configuration.LuckZen));
            Assert.That(mix.SuccessRate, Is.EqualTo(this._configuration.LuckSuccessRate));
        }
    }

    /// <summary>
    /// A wing option of a second wing, also the second option, takes the Jewels of Creation and the price of
    /// the second wings, at the rate of the wing options.
    /// </summary>
    /// <param name="existingOptions">The number of wing options of the wing.</param>
    [TestCase(0)]
    [TestCase(1)]
    public void SecondWingOption(int existingOptions)
    {
        var wing = this.Wing(this._secondWing, existingOptions);
        var creations = Enumerable.Range(0, this._configuration.SecondWingJewelsOfCreation).Select(_ => this.Item(this._creation)).ToList();

        var mix = this.Mix(3, [wing, .. creations]);

        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
        Assert.That(mix.Option.OptionType, Is.EqualTo(ItemOptionTypes.Wing));
        Assert.That(mix.Option.Number, Is.EqualTo(3));
        Assert.That(mix.Jewels, Is.EquivalentTo(creations));
        Assert.That(mix.Price, Is.EqualTo(this._configuration.SecondWingZen));
        Assert.That(mix.SuccessRate, Is.EqualTo(this._configuration.WingSuccessRate));
    }

    /// <summary>
    /// A wing option of a third wing takes the Jewels of Creation and the price of the third wings.
    /// </summary>
    [Test]
    public void ThirdWingOption()
    {
        var creations = Enumerable.Range(0, this._configuration.ThirdWingJewelsOfCreation).Select(_ => this.Item(this._creation)).ToList();

        var mix = this.Mix(4, [this.Item(this._thirdWing), .. creations]);

        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
        Assert.That(mix.Option.Number, Is.EqualTo(4));
        Assert.That(mix.Jewels, Is.EquivalentTo(creations));
        Assert.That(mix.Price, Is.EqualTo(this._configuration.ThirdWingZen));
    }

    /// <summary>
    /// The Cape of Lord, of the group 13, gets its fourth option, the command, at the price of the second wings.
    /// </summary>
    [Test]
    public void CapeOfLordCommand()
    {
        var mix = this.Mix(4, this.Item(this._capeOfLord), this.Item(this._creation));

        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
        Assert.That(mix.Option.Number, Is.EqualTo(4));
        Assert.That(mix.Price, Is.EqualTo(this._configuration.SecondWingZen));
    }

    /// <summary>
    /// A third wing with the wrong number of Jewels of Creation lacks items, or is incorrect.
    /// </summary>
    /// <param name="creations">The number of Jewels of Creation.</param>
    /// <param name="expected">The expected error.</param>
    [TestCase(1, CraftingResult.LackingMixItems)]
    [TestCase(3, CraftingResult.IncorrectMixItems)]
    public void ThirdWingOptionWithWrongNumberOfCreations(int creations, CraftingResult expected)
    {
        var items = new List<Item> { this.Item(this._thirdWing) };
        items.AddRange(Enumerable.Range(0, creations).Select(_ => this.Item(this._creation)));

        Assert.That(this.Mix(1, items.ToArray())?.Error, Is.EqualTo(expected));
    }

    /// <summary>
    /// A wing gets no option which it has, or which its wings don't have, nor more than the maximum
    /// wing options, nor a wing option for a Jewel of Soul.
    /// </summary>
    [Test]
    public void WingOptionsWhichDontFit()
    {
        Assert.That(this.Mix(1, this.Wing(this._secondWing, 1), this.Item(this._creation)), Is.Null, "the option it has");
        Assert.That(this.Mix(4, this.Item(this._secondWing), this.Item(this._creation)), Is.Null, "an option of the cape");
        Assert.That(this.Mix(1, this.Item(this._firstWing), this.Item(this._creation)), Is.Null, "a first wing");
        Assert.That(this.Mix(3, this.Wing(this._secondWing, 2), this.Item(this._creation)), Is.Null, "a third option");
        Assert.That(this.Mix(3, this.Item(this._secondWing), this.Item(this._soul)), Is.Null, "a Jewel of Soul");
    }

    /// <summary>
    /// The small wings, which have neither the Luck nor wing options, get nothing.
    /// </summary>
    [Test]
    public void SmallWings()
    {
        var smallWing = new BasicModel.ItemDefinition { Group = 12, Number = 130, Name = "Small Cape of Lord", ItemSlot = this._firstWing.ItemSlot };

        Assert.That(this.Mix(LuckChoice, this.Item(smallWing), this.Item(this._soul)), Is.Null, "the Luck");
        Assert.That(this.Mix(1, this.Item(smallWing), this.Item(this._creation)), Is.Null, "a wing option");
    }

    /// <summary>
    /// A wing above the maximum level is refused with the level error, and one at the maximum level isn't.
    /// </summary>
    [Test]
    public void WingAboveMaximumLevel()
    {
        var wing = this.Item(this._secondWing);
        wing.Level = 12;
        Assert.That(this.Mix(1, wing, this.Item(this._creation))?.Error, Is.EqualTo(CraftingResult.InvalidItemLevel));

        wing.Level = 11;
        var mix = this.Mix(1, wing, this.Item(this._creation));
        Assert.That(mix, Is.Not.Null);
        Assert.That(mix!.Error, Is.Null);
    }

    /// <summary>
    /// The configuration which the database keeps from before the wings gets the defaults of the wings.
    /// </summary>
    [Test]
    public void StoredConfigurationGetsTheDefaultsOfTheWings()
    {
        var stored = new BasicModel.PlugInConfiguration
        {
            CustomConfiguration = """
                {
                  "$id": "",
                  "MaximumItemLevel": 11,
                  "MaximumExcellentOptions": 3,
                  "LuckSuccessRate": 100,
                  "LuckJewelsOfSoul": 1,
                  "LuckZen": 1000000,
                  "ExcellentSuccessRate": 75,
                  "JewelsOfCreationPerOption": 1,
                  "ExcellentZenPerOption": 2000000
                }
                """,
        };

        var configuration = stored.GetConfiguration<WorkshopConfiguration>(ReferenceHandler.Preserve);

        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration!.MaximumWingOptions, Is.EqualTo(this._configuration.MaximumWingOptions));
        Assert.That(configuration.WingSuccessRate, Is.EqualTo(this._configuration.WingSuccessRate));
        Assert.That(configuration.SecondWingJewelsOfCreation, Is.EqualTo(this._configuration.SecondWingJewelsOfCreation));
        Assert.That(configuration.SecondWingZen, Is.EqualTo(this._configuration.SecondWingZen));
        Assert.That(configuration.ThirdWingJewelsOfCreation, Is.EqualTo(this._configuration.ThirdWingJewelsOfCreation));
        Assert.That(configuration.ThirdWingZen, Is.EqualTo(this._configuration.ThirdWingZen));
        Assert.That(configuration.ThirdWingNumbers, Is.EqualTo(new short[] { 36, 37, 38, 39, 40, 43, 50 }));
    }

    private static BasicModel.ItemOptionDefinition CreateWingOptions(int count)
    {
        var options = new BasicModel.ItemOptionDefinition { MaximumOptionsPerItem = 1 };
        for (var number = 1; number <= count; number++)
        {
            options.PossibleOptions.Add(new BasicModel.IncreasableItemOption { OptionType = ItemOptionTypes.Wing, Number = number });
        }

        return options;
    }

    private static BasicModel.ItemDefinition CreateWing(byte group, short number, string name, ItemSlotType slot, ItemOptionDefinition luckOptions, ItemOptionDefinition? wingOptions)
    {
        var wing = new BasicModel.ItemDefinition { Group = group, Number = number, Name = name, ItemSlot = slot };
        wing.PossibleItemOptions.Add(luckOptions);
        if (wingOptions is not null)
        {
            wing.PossibleItemOptions.Add(wingOptions);
        }

        return wing;
    }

    private WorkshopCraftingHandler.Mix? Mix(byte choice, params Item[] items)
        => WorkshopCraftingHandler.GetMix(items, choice, this._configuration);

    private Item Wing(ItemDefinition definition, int existingOptions)
    {
        var wing = this.Item(definition);
        foreach (var option in this._secondWingOptions.PossibleOptions.Take(existingOptions))
        {
            wing.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = option });
        }

        return wing;
    }

    private Item Sword(byte level = 0, int existingOptions = 0)
    {
        var sword = this.Item(this._sword);
        sword.Level = level;
        foreach (var option in this._excellentOptions.PossibleOptions.Take(existingOptions))
        {
            sword.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = option });
        }

        return sword;
    }

    private Item Item(ItemDefinition definition) => new BasicModel.Item { Definition = definition, Durability = 1 };
}
