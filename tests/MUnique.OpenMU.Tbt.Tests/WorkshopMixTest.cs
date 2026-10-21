// <copyright file="WorkshopMixTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.NPC;
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

    /// <summary>
    /// Sets up a sword with the Luck and six excellent options, and the jewels.
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
    /// Anything which isn't one weapon, armor or shield with the jewels doesn't fit.
    /// </summary>
    [Test]
    public void ItemsWhichDontFit()
    {
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Sword(), this.Item(this._soul)), Is.Null, "two items");
        Assert.That(this.Mix(LuckChoice, this.Sword(), this.Item(this._soul), this.Item(this._bless)), Is.Null, "another jewel");
        Assert.That(this.Mix(LuckChoice, this.Item(this._soul)), Is.Null, "no item");

        var wings = new BasicModel.ItemDefinition { Group = 12, Number = 0, Name = "Wings of Elf" };
        wings.PossibleItemOptions.Add(this._sword.PossibleItemOptions.First());
        Assert.That(this.Mix(LuckChoice, this.Item(wings), this.Item(this._soul)), Is.Null, "wings");

        var socketSword = this.Sword();
        socketSword.SocketCount = 1;
        Assert.That(this.Mix(LuckChoice, socketSword, this.Item(this._soul)), Is.Null, "socket item");

        var ancientSword = this.Sword();
        ancientSword.ItemSetGroups.Add(new BasicModel.ItemOfItemSet());
        Assert.That(this.Mix(LuckChoice, ancientSword, this.Item(this._soul)), Is.Null, "ancient item");
    }

    private WorkshopCraftingHandler.Mix? Mix(byte choice, params Item[] items)
        => WorkshopCraftingHandler.GetMix(items, choice, this._configuration);

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
