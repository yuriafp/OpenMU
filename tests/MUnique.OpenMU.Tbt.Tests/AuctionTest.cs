// <copyright file="AuctionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Tbt.Auction;
using MUnique.OpenMU.Tbt.StarterKit;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the leilão of Otto: the item he keeps and creates again, which items he takes, the
/// currencies and kinds, his packets, and how he settles what a save or a stop of the server left open.
/// </summary>
[TestFixture]
public class AuctionTest
{
    private static readonly Guid CurrentStay = Guid.NewGuid();

    private static readonly Guid EarlierStay = Guid.NewGuid();

    private InMemoryPersistenceContextProvider _contextProvider = null!;

    private GameConfiguration _configuration = null!;

    /// <summary>
    /// Creates the data of a new Season 6 database in memory.
    /// </summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task CreateSeasonSixDataAsync()
    {
        this._contextProvider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(this._contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = this._contextProvider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// An excellent item with luck, an option and the skill comes back with all of it, through its JSON.
    /// </summary>
    [Test]
    public void ExcellentItemComesBackWhole()
    {
        var definition = this._configuration.Items.First(d => d.Group == 0 && d.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent)));
        var item = new BasicModel.Item { Definition = definition, Level = 11, Durability = 37, HasSkill = true };
        var index = 0;
        foreach (var type in new[] { ItemOptionTypes.Luck, ItemOptionTypes.Option, ItemOptionTypes.Excellent })
        {
            var option = definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).First(p => p.OptionType == type);
            item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = option, Level = type == ItemOptionTypes.Option ? 3 : 0, Index = index++ });
        }

        var copy = this.RoundTrip(item);

        Assert.That(copy, Is.Not.Null);
        Assert.That((copy!.Definition, copy.Level, copy.Durability, copy.HasSkill), Is.EqualTo((item.Definition, item.Level, item.Durability, item.HasSkill)));
        Assert.That(
            copy.ItemOptions.Select(o => (o.ItemOption, o.Level, o.Index)),
            Is.EquivalentTo(item.ItemOptions.Select(o => (o.ItemOption, o.Level, o.Index))));
    }

    /// <summary>
    /// An ancient item keeps its set and its bonus option.
    /// </summary>
    [Test]
    public void AncientItemKeepsItsSet()
    {
        var set = this._configuration.ItemSetGroups.SelectMany(g => g.Items).First(i => i.BonusOption is not null && i.ItemDefinition is not null);
        var item = new BasicModel.Item { Definition = set.ItemDefinition, Level = 7, Durability = 20, SocketCount = 0 };
        item.ItemSetGroups.Add(set);
        item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = set.BonusOption, Level = 2 });

        var copy = this.RoundTrip(item);

        Assert.That(copy?.ItemSetGroups, Is.EquivalentTo(new[] { set }));
        Assert.That(copy?.ItemOptions.Single().ItemOption, Is.SameAs(set.BonusOption));
        Assert.That(copy?.ItemOptions.Single().Level, Is.EqualTo(2));
    }

    /// <summary>
    /// A pet keeps its experience.
    /// </summary>
    [Test]
    public void PetKeepsItsExperience()
    {
        var item = new BasicModel.Item { Definition = this.Definition(13, 4), Level = 12, Durability = 255, PetExperience = 123_456 };

        Assert.That(this.RoundTrip(item)?.PetExperience, Is.EqualTo(123_456));
    }

    /// <summary>
    /// An item with an option which the configuration doesn't know isn't taken, so nothing of it can be lost.
    /// </summary>
    [Test]
    public void ItemWithAnUnknownOptionIsRefused()
    {
        var item = new BasicModel.Item { Definition = this.Definition(0, 0), Durability = 20 };
        item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = new BasicModel.IncreasableItemOption { Id = Guid.NewGuid() } });

        Assert.That(AuctionItem.Of(item, this._configuration), Is.Null);
    }

    /// <summary>
    /// Otto takes the items of the bag which can be traded, but not the equipped ones, those of the
    /// personal store, those bound to the character and those of the starter kit.
    /// </summary>
    [Test]
    public void TakesOnlyTradableItemsOfTheBag()
    {
        var inBag = new BasicModel.Item { Definition = this.Definition(0, 0), ItemSlot = 20 };
        var equipped = new BasicModel.Item { Definition = this.Definition(0, 0), ItemSlot = 0 };
        var inStore = new BasicModel.Item { Definition = this.Definition(0, 0), ItemSlot = InventoryConstants.FirstStoreItemSlotIndex };
        var notTradable = this._configuration.Items.FirstOrDefault(d => !d.IsTradable);
        var bound = this._configuration.Items.FirstOrDefault(d => d.IsBoundToCharacter && d.IsTradable);
        var kitItem = new BasicModel.Item { Definition = this.Definition(0, 0), ItemSlot = 21, Id = Guid.NewGuid() };
        var kitCharacter = Guid.NewGuid();
        StarterKitStore.Activate(kitCharacter, DateTime.UtcNow.AddDays(1), [kitItem.Id]);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(Auctioneer.RefusalOf(inBag), Is.Null);
                Assert.That(Auctioneer.RefusalOf(equipped), Is.Not.Null);
                Assert.That(Auctioneer.RefusalOf(inStore), Is.Not.Null);
                Assert.That(Auctioneer.RefusalOf(kitItem), Is.Not.Null);
                if (notTradable is not null)
                {
                    Assert.That(Auctioneer.RefusalOf(new BasicModel.Item { Definition = notTradable, ItemSlot = 20 }), Is.Not.Null);
                }

                if (bound is not null)
                {
                    Assert.That(Auctioneer.RefusalOf(new BasicModel.Item { Definition = bound, ItemSlot = 20 }), Is.Not.Null);
                }
            });
        }
        finally
        {
            StarterKitStore.Forget(kitCharacter);
        }
    }

    /// <summary>
    /// The codes of the currencies are a contract with the client: 0 for zen, then the jewels of the
    /// Banqueira in her order; the names are those of <c>tbt.jewel_bank</c>.
    /// </summary>
    [Test]
    public void CurrenciesKeepTheirCodes()
    {
        Assert.That(AuctionCurrency.NameOf(0), Is.EqualTo("zen"));
        Assert.That(AuctionCurrency.NameOf(1), Is.EqualTo("Bless"));
        Assert.That(AuctionCurrency.NameOf(10), Is.EqualTo("HigherRefineStone"));
        Assert.That(AuctionCurrency.NameOf(11), Is.Null);
        Assert.That(AuctionCurrency.NameOf(AuctionCurrency.Any), Is.Null);
        Assert.That(AuctionCurrency.CodeOf("Soul"), Is.EqualTo(2));
        Assert.That(AuctionCurrency.CodeOf("zen"), Is.EqualTo(0));
        Assert.That(AuctionCurrency.JewelOf("zen"), Is.Null);
        Assert.That(AuctionCurrency.Describe("Bless", 1_000), Is.EqualTo($"{1_000:N0} Bless"));
    }

    /// <summary>
    /// The kind of an item, for the filter of the window.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="category">The name of the kind.</param>
    [TestCase(0, 0, "Weapons")]
    [TestCase(5, 0, "Weapons")]
    [TestCase(6, 0, "Shields")]
    [TestCase(8, 0, "Armor")]
    [TestCase(12, 0, "Wings")]
    [TestCase(12, 36, "Wings")]
    [TestCase(14, 13, "Jewels")]
    [TestCase(12, 30, "Jewels")]
    [TestCase(13, 0, "Pets")]
    [TestCase(13, 3, "Pets")]
    [TestCase(13, 8, "Accessories")]
    [TestCase(13, 12, "Accessories")]
    [TestCase(14, 0, "Others")]
    public void KindOfAnItem(int group, int number, string category)
    {
        var item = new BasicModel.Item { Definition = this.Definition((byte)group, (short)number) };

        Assert.That(AuctionCategories.Of(item), Is.EqualTo(Enum.Parse<AuctionCategory>(category)));
    }

    /// <summary>
    /// The request has the action, the arguments, the value and the price; another packet isn't one.
    /// </summary>
    [Test]
    public void RequestIsRead()
    {
        var packet = new byte[] { 0xC1, 20, 0xFB, 0x05, 4, 0, 1, 0, 0x39, 0x30, 0, 0, 0, 0, 0, 0, 0x10, 0x27, 0, 0 };

        Assert.That(AuctionRequest.Read(packet), Is.EqualTo(new AuctionRequest(AuctionAction.Buy, 0, 1, 12345, 10_000)));
        Assert.That(AuctionRequest.Read(packet[..19]), Is.Null);
        packet[4] = 9;
        Assert.That(AuctionRequest.Read(packet), Is.Null, "an unknown action");
        packet[4] = 4;
        packet[3] = 0x03;
        Assert.That(AuctionRequest.Read(packet), Is.Null, "the request of the Banqueira");
    }

    /// <summary>
    /// A search goes on after the request: the quality, and the item types of a search by name.
    /// </summary>
    [Test]
    public void SearchIsRead()
    {
        var packet = new byte[] { 0xC1, 28, 0xFB, 0x05, 1, 3, 0xFF, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 3, 0, 0, 1, 0, 0x01, 0x18 };

        var request = AuctionRequest.Read(packet);
        Assert.That(request, Is.Not.Null);
        var search = Auctioneer.SearchOf(request!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(request.Value.Action, Is.EqualTo(AuctionAction.Browse));
            Assert.That(request.Value.Value, Is.EqualTo(2));
            Assert.That(search.Category, Is.EqualTo(AuctionCategory.Armor));
            Assert.That(search.Currency, Is.Null);
            Assert.That(search.Quality, Is.EqualTo(AuctionQuality.Level9));
            Assert.That(search.MinimumLevel, Is.EqualTo(9));
            Assert.That(search.ItemTypes, Is.EqualTo(new[] { 0, 1, (12 * 512) + 1 }));
        });

        packet[21] = 4;
        Assert.That(AuctionRequest.Read(packet), Is.Null, "fewer item types than it says");
        packet[21] = AuctionRequest.NoNameSearch;
        Assert.That(AuctionRequest.Read(packet)!.Value.ItemTypes, Is.Null, "without a search by name");
        packet[20] = 99;
        Assert.That(AuctionRequest.Read(packet)!.Value.Quality, Is.EqualTo(AuctionQuality.Any), "an unknown quality");
        Assert.That(AuctionRequest.Read(packet[..20])!.Value.Quality, Is.EqualTo(AuctionQuality.Any), "a request without a search");
    }

    /// <summary>
    /// A kind or a currency which the server doesn't know searches all.
    /// </summary>
    [Test]
    public void UnknownSearchIsAll()
    {
        var search = Auctioneer.SearchOf(new AuctionRequest(AuctionAction.Browse, 200, 77, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(search.Category, Is.EqualTo(AuctionCategory.All));
            Assert.That(search.Currency, Is.Null);
            Assert.That(Auctioneer.SearchOf(new AuctionRequest(AuctionAction.Browse, 8, 2, 0, 0)).Currency, Is.EqualTo(AuctionCurrency.NameOf(2)));
        });
    }

    /// <summary>
    /// A page has its header, the fees, the zen to take and each item with its flags and bytes.
    /// </summary>
    [Test]
    public void PageHasTheItemsAndTheFees()
    {
        var account = Guid.NewGuid();
        var listings = new List<StoredListing>
        {
            new(7, account, "Vendedor", "{}", [1, 2, 3], "Kris +0", "zen", 5_000_000, AuctionStore.Active, 3_600),
            new(5, Guid.NewGuid(), "Outro", "{}", Enumerable.Range(1, 16).Select(i => (byte)i).ToArray(), "Bless +0", "Soul", 12, AuctionStore.Delivering, -10),
        };
        var page = new AuctionPage(AuctionView.Browse, true, AuctionCategory.Jewels, 2, 1, 3, 4, 7_000_000_000, listings, account);
        var configuration = new AuctionConfiguration { ListingFee = 100_000, CancelFee = 50_000, BuyerFee = 200_000, DurationDays = 7, MaximumListingsPerAccount = 20 };
        var buffer = Enumerable.Repeat((byte)0xFF, 300).ToArray();

        var length = AuctionContentsWriter.Write(buffer, page, configuration);

        Assert.That(length, Is.EqualTo(AuctionContentsWriter.EntriesOffset + (2 * AuctionContentsWriter.EntryLength)));
        Assert.That(buffer[..10], Is.EqualTo(new byte[] { 0xC2, 0, (byte)length, 0xFB, 0x04, 1, 1, 5, 2, 2 }));
        Assert.That(buffer[length..], Is.All.EqualTo((byte)0xFF));
        Assert.That(BitConverter.ToUInt16(buffer, 10), Is.EqualTo(1));
        Assert.That(BitConverter.ToUInt16(buffer, 12), Is.EqualTo(3));
        Assert.That((BitConverter.ToInt32(buffer, 14), BitConverter.ToInt32(buffer, 18), BitConverter.ToInt32(buffer, 22)), Is.EqualTo((100_000, 50_000, 200_000)));
        Assert.That(buffer[26..29], Is.EqualTo(new byte[] { 7, 20, 4 }));
        Assert.That(BitConverter.ToInt64(buffer, 30), Is.EqualTo(7_000_000_000));

        var first = buffer.AsSpan(AuctionContentsWriter.EntriesOffset, AuctionContentsWriter.EntryLength).ToArray();
        Assert.That(BitConverter.ToInt64(first, 0), Is.EqualTo(7));
        Assert.That((first[8], first[9]), Is.EqualTo(((byte)0, (byte)1)), "zen, mine");
        Assert.That(BitConverter.ToUInt32(first, 12), Is.EqualTo(5_000_000));
        Assert.That(BitConverter.ToInt32(first, 16), Is.EqualTo(3_600));
        Assert.That(System.Text.Encoding.ASCII.GetString(first, 20, 10).TrimEnd('\0'), Is.EqualTo("Vendedor"));
        Assert.That(first[30..35], Is.EqualTo(new byte[] { 3, 0, 1, 2, 3 }));

        var second = buffer.AsSpan(AuctionContentsWriter.EntriesOffset + AuctionContentsWriter.EntryLength, AuctionContentsWriter.EntryLength).ToArray();
        Assert.That((second[8], second[9]), Is.EqualTo(((byte)2, (byte)6)), "Soul, ended and pending");
        Assert.That(second[30], Is.EqualTo(16));
        Assert.That(second[32..48], Is.EqualTo(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray()));
    }

    /// <summary>
    /// The result names the action and whether it was done.
    /// </summary>
    [Test]
    public void ResultNamesTheAction()
    {
        var buffer = new byte[8];

        Assert.That(AuctionContentsWriter.WriteResult(buffer, AuctionAction.Return, true), Is.EqualTo(8));
        Assert.That(buffer, Is.EqualTo(new byte[] { 0xC1, 8, 0xFB, 0x06, 5, 1, 0, 0 }));
    }

    /// <summary>
    /// An operation is settled by the outcome which the stay knows, or else by the saved data: an
    /// earlier stay's listing happened when its item is gone, its delivery when its new item is
    /// there; a listing of this stay waits until its item is gone, a delivery of this stay waits.
    /// </summary>
    /// <param name="kind">The operation.</param>
    /// <param name="earlierStay">Whether an earlier stay wrote it.</param>
    /// <param name="itemSaved">Whether its item is in the saved data.</param>
    /// <param name="known">The outcome which the stay knows.</param>
    /// <param name="settlement">The name of the settlement (the enum is internal).</param>
    [TestCase(AuctionStore.ListKind, true, false, null, "Complete")]
    [TestCase(AuctionStore.ListKind, true, true, null, "Undo")]
    [TestCase(AuctionStore.BuyKind, true, true, null, "Complete")]
    [TestCase(AuctionStore.BuyKind, true, false, null, "Undo")]
    [TestCase(AuctionStore.ReturnKind, true, true, null, "Complete")]
    [TestCase(AuctionStore.ReturnKind, true, false, null, "Undo")]
    [TestCase(AuctionStore.ListKind, false, false, null, "Complete")]
    [TestCase(AuctionStore.ListKind, false, true, null, "Wait")]
    [TestCase(AuctionStore.BuyKind, false, true, null, "Wait")]
    [TestCase(AuctionStore.BuyKind, false, false, null, "Wait")]
    [TestCase(AuctionStore.ListKind, false, true, true, "Complete")]
    [TestCase(AuctionStore.BuyKind, false, true, false, "Undo")]
    [TestCase(AuctionStore.ReturnKind, true, false, true, "Complete")]
    public void SettlesByTheKnownOutcomeOrTheSavedData(string kind, bool earlierStay, bool itemSaved, bool? known, string settlement)
    {
        var operation = new PendingOperation(1, kind, earlierStay ? EarlierStay : CurrentStay, itemSaved);

        Assert.That(Auctioneer.Decide(operation, CurrentStay, known), Is.EqualTo(Enum.Parse<Auctioneer.Settlement>(settlement)));
    }

    /// <summary>
    /// A taking of zen of an earlier stay was saved when its inventory has the zen with it; else the
    /// zen goes back to Otto. One of this stay waits for its outcome.
    /// </summary>
    /// <param name="earlierStay">Whether an earlier stay wrote it.</param>
    /// <param name="savedMoney">The saved zen of the inventory.</param>
    /// <param name="known">The outcome which the stay knows.</param>
    /// <param name="settlement">The name of the settlement (the enum is internal).</param>
    [TestCase(true, 1_500L, null, "Complete")]
    [TestCase(true, 500L, null, "Undo")]
    [TestCase(true, null, null, "Undo")]
    [TestCase(false, 1_500L, null, "Wait")]
    [TestCase(false, 500L, true, "Complete")]
    [TestCase(false, 1_500L, false, "Undo")]
    public void SettlesTakingsOfZenByTheSavedZen(bool earlierStay, long? savedMoney, bool? known, string settlement)
    {
        var claim = new ZenClaim(Guid.NewGuid(), Guid.NewGuid(), earlierStay ? EarlierStay : CurrentStay, 1_000, Guid.NewGuid(), 1_500);

        Assert.That(Auctioneer.DecideClaim(claim, savedMoney, CurrentStay, known), Is.EqualTo(Enum.Parse<Auctioneer.Settlement>(settlement)));
    }

    /// <summary>
    /// The highest price in zen is what a character carries; in jewels, the configuration.
    /// </summary>
    [Test]
    public void HighestPriceDependsOnTheCurrency()
    {
        var configuration = new AuctionConfiguration { MaximumJewelPrice = 500 };

        Assert.That(Auctioneer.MaximumPriceOf("zen", this._configuration, configuration), Is.EqualTo(this._configuration.MaximumInventoryMoney));
        Assert.That(Auctioneer.MaximumPriceOf("Bless", this._configuration, configuration), Is.EqualTo(500));
    }

    private Item? RoundTrip(Item item)
    {
        var stored = AuctionItem.Of(item, this._configuration);
        Assert.That(stored, Is.Not.Null, "Otto takes the item");
        using var context = this._contextProvider.CreateNewContext();
        return AuctionItem.FromJson(stored!.ToJson()).Create(context, this._configuration);
    }

    private ItemDefinition Definition(byte group, short number) => this._configuration.Items.Single(d => d.Group == group && d.Number == number);
}
