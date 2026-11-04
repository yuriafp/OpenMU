// <copyright file="JewelBankTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Tbt.JewelBank;
using MUnique.OpenMU.Tbt.Registro;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the Banqueira: which items she keeps and as how many jewels, the items she gives out, her
/// packets, and how she settles the operations which a save or a stop of the server left open.
/// </summary>
[TestFixture]
public class JewelBankTest
{
    private static readonly Guid CurrentStay = Guid.NewGuid();

    private static readonly Guid EarlierStay = Guid.NewGuid();

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
        using var context = contextProvider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// Each jewel and its pack exist in the data, the pack goes up to +2, and neither stacks: so a
    /// jewel counts one, and a pack ten per level.
    /// </summary>
    [Test]
    public void JewelsAndPacksExistInTheData()
    {
        Assert.Multiple(() =>
        {
            foreach (var jewel in BankJewel.All)
            {
                var single = this._configuration.Items.SingleOrDefault(d => d.Group == jewel.Group && d.Number == jewel.Number);
                var packed = this._configuration.Items.SingleOrDefault(d => d.Group == BankJewel.PackedGroup && d.Number == jewel.PackedNumber);
                Assert.That(single, Is.Not.Null, jewel.Name);
                Assert.That(packed, Is.Not.Null, jewel.Name);
                Assert.That(single?.Durability, Is.EqualTo(1), jewel.Name);
                Assert.That(packed?.MaximumItemLevel, Is.EqualTo(BankJewel.MaximumPackLevel), jewel.Name);
            }
        });
    }

    /// <summary>
    /// The names in <c>tbt.jewel_bank</c> and the order of the window are a contract with the
    /// database and the client.
    /// </summary>
    [Test]
    public void JewelsKeepTheirNamesAndOrder()
    {
        Assert.That(
            BankJewel.All.Select(j => j.Name),
            Is.EqualTo(new[] { "Bless", "Soul", "Life", "Chaos", "Creation", "Guardian", "Harmony", "Gemstone", "LowerRefineStone", "HigherRefineStone" }));
    }

    /// <summary>
    /// A jewel counts one, a pack 10, 20 or 30, and other items nothing.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <param name="name">The name of the jewel, or <c>null</c>.</param>
    /// <param name="amount">The amount.</param>
    [TestCase(14, 13, 0, "Bless", 1)]
    [TestCase(12, 15, 0, "Chaos", 1)]
    [TestCase(14, 41, 0, "Gemstone", 1)]
    [TestCase(12, 30, 0, "Bless", 10)]
    [TestCase(12, 31, 1, "Soul", 20)]
    [TestCase(12, 141, 2, "Chaos", 30)]
    [TestCase(12, 143, 2, "HigherRefineStone", 30)]
    [TestCase(12, 30, 3, null, 0)]
    [TestCase(14, 15, 0, null, 0)]
    [TestCase(0, 13, 0, null, 0)]
    public void CountsTheJewelsOfAnItem(int group, int number, int level, string? name, int amount)
    {
        var item = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Group = (byte)group, Number = (short)number }, Level = (byte)level };

        var found = BankJewel.Of(item);

        Assert.That(found?.Jewel.Name, Is.EqualTo(name));
        Assert.That(found?.Amount ?? 0, Is.EqualTo(amount));
    }

    /// <summary>
    /// A withdrawal of 1 is the jewel, of 10 a pack +0, of 30 a pack +2; other amounts have no item.
    /// </summary>
    [Test]
    public void WithdrawalsGetTheJewelOrAPack()
    {
        var bless = BankJewel.All[0];

        Assert.That(bless.ItemFor(1), Is.EqualTo(((byte)14, (short)13, (byte)0)));
        Assert.That(bless.ItemFor(10), Is.EqualTo(((byte)12, (short)30, (byte)0)));
        Assert.That(bless.ItemFor(30), Is.EqualTo(((byte)12, (short)30, (byte)2)));
        Assert.That(bless.ItemFor(0), Is.Null);
        Assert.That(bless.ItemFor(5), Is.Null);
        Assert.That(bless.ItemFor(40), Is.Null);
    }

    /// <summary>
    /// The Banqueira takes the jewels of the bag, but not those in the personal store, and only those asked for.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DepositTakesTheBagButNotTheStoreAsync()
    {
        var inventory = new Storage(InventoryConstants.FirstStoreItemSlotIndex + InventoryConstants.StoreSize, InventoryConstants.EquippableSlotsCount, 0, new BasicModel.ItemStorage());
        var blessInBag = this.CreateItem(14, 13, 0);
        var packInBag = this.CreateItem(12, 30, 1);
        var soulInBag = this.CreateItem(14, 14, 0);
        var blessInStore = this.CreateItem(14, 13, 0);
        Assert.That(await inventory.AddItemAsync(20, blessInBag).ConfigureAwait(false), Is.True);
        Assert.That(await inventory.AddItemAsync(21, packInBag).ConfigureAwait(false), Is.True);
        Assert.That(await inventory.AddItemAsync(22, soulInBag).ConfigureAwait(false), Is.True);
        Assert.That(await inventory.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, blessInStore).ConfigureAwait(false), Is.True);

        var bless = JewelBanker.GetDepositItems(inventory, [BankJewel.All[0]]);
        var all = JewelBanker.GetDepositItems(inventory, BankJewel.All.ToList());

        Assert.That(bless.Select(i => i.Item), Is.EquivalentTo(new[] { blessInBag, packInBag }));
        Assert.That(bless.Sum(i => i.Amount), Is.EqualTo(21));
        Assert.That(all.Select(i => i.Item), Is.EquivalentTo(new[] { blessInBag, packInBag, soulInBag }));
    }

    /// <summary>
    /// The contents name each jewel by its item, in the order of the window, with its amount and the fees.
    /// </summary>
    [Test]
    public void ContentsHaveTheJewelsAndTheFees()
    {
        var buffer = Enumerable.Repeat((byte)0xFF, 200).ToArray();
        var jewels = new Dictionary<BankJewel, long> { [BankJewel.All[0]] = 25, [BankJewel.All[3]] = 3_000_000_000 };
        var configuration = new JewelBankConfiguration { DepositFeePerJewel = 1_000, WithdrawalFeePerJewel = 2_000 };

        var length = JewelBanker.WritePacket(buffer, jewels, configuration, JewelBankContentsRef.JewelBankFlags.Open);

        Assert.That(length, Is.EqualTo(96));
        Assert.That(buffer[..6], Is.EqualTo(new byte[] { 0xC1, 96, 0xFB, 0x02, 1, 10 }));
        Assert.That(buffer[length..], Is.All.EqualTo((byte)0xFF));
        var packet = new JewelBankContentsRef(buffer.AsSpan(0, length));
        Assert.That((packet.DepositFeePerJewel, packet.WithdrawalFeePerJewel), Is.EqualTo((1_000, 2_000)));
        Assert.That(packet.GetAmount(0), Is.EqualTo(25));
        Assert.That(packet.GetAmount(1), Is.Zero);
        Assert.That(packet.GetAmount(3), Is.EqualTo(int.MaxValue), "more than the packet holds shows its maximum");

        // The Jewel of Chaos: group 12, number 15.
        Assert.That(buffer[(JewelBankContentsRef.JewelsOffset + (3 * JewelBankContentsRef.JewelLength))..][..4], Is.EqualTo(new byte[] { 12, 0, 15, 0 }));
    }

    /// <summary>
    /// The request has the action, the jewel and the amount; another packet isn't one.
    /// </summary>
    [Test]
    public void RequestIsRead()
    {
        Assert.That(JewelBankRequest.Read(new byte[] { 0xC1, 8, 0xFB, 0x03, 3, 4, 30, 0 }), Is.EqualTo(new JewelBankRequest(JewelBankAction.Withdraw, 4, 30)));
        Assert.That(JewelBankRequest.Read(new byte[] { 0xC1, 8, 0xFB, 0x01, 3, 4, 30, 0 }), Is.Null);
        Assert.That(JewelBankRequest.Read(new byte[] { 0xC1, 6, 0xFB, 0x03, 3, 4 }), Is.Null);
    }

    /// <summary>
    /// An operation is settled by the outcome which the stay knows, or else by the saved data: an
    /// earlier stay's deposit is saved when its items are gone, its withdrawal when its item is there;
    /// a deposit of this stay waits until its items are gone.
    /// </summary>
    /// <param name="kind">The kind of the operation.</param>
    /// <param name="earlierStay">Whether an earlier stay made it.</param>
    /// <param name="savedItems">How many of its two items are in the saved data.</param>
    /// <param name="known">The outcome which the stay knows.</param>
    /// <param name="settlement">The name of the settlement (the enum is internal).</param>
    [TestCase(JewelBankStore.Deposit, true, 0, null, "Credit")]
    [TestCase(JewelBankStore.Deposit, true, 2, null, "Forget")]
    [TestCase(JewelBankStore.Deposit, true, 1, null, "Forget")]
    [TestCase(JewelBankStore.Withdrawal, true, 1, null, "Forget")]
    [TestCase(JewelBankStore.Withdrawal, true, 0, null, "Credit")]
    [TestCase(JewelBankStore.Deposit, false, 0, null, "Credit")]
    [TestCase(JewelBankStore.Deposit, false, 2, null, "Wait")]
    [TestCase(JewelBankStore.Withdrawal, false, 0, null, "Wait")]
    [TestCase(JewelBankStore.Withdrawal, false, 1, null, "Wait")]
    [TestCase(JewelBankStore.Deposit, false, 2, true, "Credit")]
    [TestCase(JewelBankStore.Withdrawal, false, 1, false, "Forget")]
    [TestCase(JewelBankStore.Withdrawal, false, 0, true, "Credit")]
    public void SettlesByTheKnownOutcomeOrTheSavedData(string kind, bool earlierStay, int savedItems, bool? known, string settlement)
    {
        var operation = new JewelBankOperation(Guid.NewGuid(), Guid.NewGuid(), earlierStay ? EarlierStay : CurrentStay, kind, "Bless", 2, 0, [Guid.NewGuid(), Guid.NewGuid()]);

        Assert.That(JewelBanker.Decide(operation, savedItems, CurrentStay, known), Is.EqualTo(Enum.Parse<JewelBanker.Settlement>(settlement)));
    }

    /// <summary>
    /// The jewels which left with the Banqueira count apart at the exit, net of those taken out.
    /// </summary>
    [Test]
    public void BankedJewelsCountApartAtTheExit()
    {
        var account = new BasicModel.Account();
        var character = new BasicModel.Character { Inventory = new BasicModel.ItemStorage() };
        var counters = new SessionCounters(account, character);

        counters.AddBankedJewels(new JewelCount(Bless: 30, Soul: 1, Chaos: 0, Life: 0, Creation: 0));
        counters.AddBankedJewels(-new JewelCount(Bless: 10, Soul: 0, Chaos: 0, Life: 0, Creation: 0));

        var data = System.Text.Json.JsonSerializer.SerializeToElement(counters.ToData(account, character));
        Assert.That(data.GetProperty("jewels_banked").GetProperty("bless").GetInt32(), Is.EqualTo(20));
        Assert.That(data.GetProperty("jewels_banked").GetProperty("soul").GetInt32(), Is.EqualTo(1));
    }

    private Item CreateItem(byte group, short number, byte level)
        => new BasicModel.Item { Definition = this._configuration.Items.Single(d => d.Group == group && d.Number == number), Level = level, Durability = 1 };
}
