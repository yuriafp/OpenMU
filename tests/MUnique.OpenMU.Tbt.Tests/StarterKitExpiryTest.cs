// <copyright file="StarterKitExpiryTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using System.IO;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.PeriodItems;
using MUnique.OpenMU.Tbt.StarterKit;
using Nito.AsyncEx;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the expiry of the starter kit which the game client gets: the classic period item list,
/// C1 05 D2 11 with the count, then C1 0C D2 12 per item, with the item code, the slot and the
/// expiry as Unix time, all little endian. Also the durability of its items, which the client
/// doesn't show on items with an expiry.
/// </summary>
[TestFixture]
public class StarterKitExpiryTest
{
    /// <summary>The expiry of the kit, 2026-11-01 12:00:00 UTC, as Unix time 1793534400 in little endian.</summary>
    private static readonly byte[] ExpiryBytes = [0xC0, 0x29, 0xE7, 0x6A];

    private static readonly DateTime ExpiresAt = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The first slot of the bag, after the 12 equipped ones.</summary>
    private static readonly byte FirstBagSlot = InventoryConstants.EquippableSlotsCount;

    private readonly BasicModel.ItemDefinition _helmDefinition = new() { Group = 7, Number = 2, Width = 2, Height = 2 };

    private readonly BasicModel.ItemDefinition _pandaDefinition = new() { Group = 13, Number = 80, Width = 1, Height = 1 };

    private readonly BasicModel.ItemDefinition _staffDefinition = new() { Group = 5, Number = 1, Width = 1, Height = 4 };

    private readonly BasicModel.ItemDefinition _jewelDefinition = new() { Group = 14, Number = 13, Width = 1, Height = 1 };

    /// <summary>
    /// At the entry and when the kit is given, the client gets the kit items which the character
    /// still has, at their slots, equipped or in the bag; not its other items, nor a sold kit item.
    /// </summary>
    [Test]
    public async Task EntrySendsTheKitItemsAtTheirSlotsAsync()
    {
        var helm = Item(this._helmDefinition, InventoryConstants.HelmSlot);
        var panda = Item(this._pandaDefinition, InventoryConstants.PetSlot);
        var staff = Item(this._staffDefinition, FirstBagSlot);
        var jewel = Item(this._jewelDefinition, 20);
        var soldItemId = Guid.NewGuid();
        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [helm.Id, panda.Id, staff.Id, soldItemId]);
        var (player, output) = CreateRemotePlayer();

        await StarterKitPlugIn.SendExpiryAsync(player, kit, [helm, jewel, panda, staff]).ConfigureAwait(false);

        byte[] expected =
        [
            0xC1, 0x05, 0xD2, 0x11, 3,
            0xC1, 0x0C, 0xD2, 0x12, 0x02, 0x0E, 2, 0, .. ExpiryBytes,
            0xC1, 0x0C, 0xD2, 0x12, 0x50, 0x1A, 8, 0, .. ExpiryBytes,
            0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 12, 0, .. ExpiryBytes,
        ];
        Assert.That(output.ToArray(), Is.EqualTo(expected));
    }

    /// <summary>
    /// A kit item which is equipped, moved in the bag and unequipped gets its expiry for each new slot.
    /// </summary>
    [Test]
    public async Task MoveSendsTheNewSlotAsync()
    {
        var inventory = CreateInventory();
        var staff = Item(this._staffDefinition, 0);
        Assert.That(await inventory.AddItemAsync(FirstBagSlot, staff).ConfigureAwait(false), Is.True);
        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [staff.Id]);
        var (player, output) = CreateRemotePlayer();

        // Equipped, unequipped, moved in the bag.
        foreach (var slot in new[] { InventoryConstants.LeftHandSlot, FirstBagSlot, (byte)13, (byte)30 })
        {
            await inventory.RemoveItemAsync(staff).ConfigureAwait(false);
            Assert.That(await inventory.AddItemAsync(slot, staff).ConfigureAwait(false), Is.True);
            await StarterKitPlugIn.SendExpiryOfMoveAsync(player, kit, staff, inventory).ConfigureAwait(false);
        }

        byte[] expected =
        [
            0xC1, 0x05, 0xD2, 0x11, 1, 0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 0, 0, .. ExpiryBytes,
            0xC1, 0x05, 0xD2, 0x11, 1, 0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 12, 0, .. ExpiryBytes,
            0xC1, 0x05, 0xD2, 0x11, 1, 0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 13, 0, .. ExpiryBytes,
            0xC1, 0x05, 0xD2, 0x11, 1, 0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 30, 0, .. ExpiryBytes,
        ];
        Assert.That(output.ToArray(), Is.EqualTo(expected));
    }

    /// <summary>
    /// The move of an item which isn't of the kit, or of a kit item which isn't in the inventory, sends nothing.
    /// </summary>
    [Test]
    public async Task MoveOfOtherItemsSendsNothingAsync()
    {
        var inventory = CreateInventory();
        var jewel = Item(this._jewelDefinition, 0);
        Assert.That(await inventory.AddItemAsync(20, jewel).ConfigureAwait(false), Is.True);
        var staffElsewhere = Item(this._staffDefinition, 20);
        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [staffElsewhere.Id]);
        var (player, output) = CreateRemotePlayer();

        await StarterKitPlugIn.SendExpiryOfMoveAsync(player, kit, jewel, inventory).ConfigureAwait(false);
        await StarterKitPlugIn.SendExpiryOfMoveAsync(player, kit, staffElsewhere, inventory).ConfigureAwait(false);
        await StarterKitPlugIn.SendExpiryOfMoveAsync(player, kit, staffElsewhere, null).ConfigureAwait(false);

        Assert.That(output.ToArray(), Is.Empty);
    }

    /// <summary>
    /// Without kit items among the items, nothing is sent, not even the count.
    /// </summary>
    [Test]
    public async Task NoKitItemsSendNothingAsync()
    {
        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [Guid.NewGuid()]);
        var (player, output) = CreateRemotePlayer();

        await StarterKitPlugIn.SendExpiryAsync(player, kit, [Item(this._jewelDefinition, 20)]).ConfigureAwait(false);

        Assert.That(output.ToArray(), Is.Empty);
    }

    /// <summary>
    /// The move of a kit item of a player which isn't a human with a character in the world sends
    /// nothing, and a bot has no game client at all.
    /// </summary>
    [Test]
    public async Task NothingForPlayersWhichAreNoHumansAsync()
    {
        var staff = Item(this._staffDefinition, FirstBagSlot);
        var characterId = Guid.NewGuid();
        StarterKitStore.Activate(characterId, ExpiresAt, [staff.Id]);
        try
        {
            var (player, output) = CreateRemotePlayer();
            await new StarterKitPlugIn().ItemMovedAsync(player, staff).ConfigureAwait(false);
            Assert.That(output.ToArray(), Is.Empty);

            Assert.That(new BotPlayer(CreateGameContext()).GetConnection(), Is.Null);
        }
        finally
        {
            StarterKitStore.Forget(characterId);
        }
    }

    /// <summary>
    /// The periodic check refills the durability of the kit items, equipped or in the bag, and
    /// leaves the other items as they are.
    /// </summary>
    [Test]
    public async Task CheckRefillsTheDurabilityOfTheKitItemsAsync()
    {
        var inventory = CreateInventory();
        var helm = Item(Wearable(7, 2, InventoryConstants.HelmSlot), InventoryConstants.HelmSlot);
        var staff = Item(Wearable(5, 1, InventoryConstants.LeftHandSlot), FirstBagSlot);
        var otherHelm = Item(Wearable(7, 3, InventoryConstants.HelmSlot), 30);
        foreach (var item in new[] { helm, staff, otherHelm })
        {
            item.Durability = 1;
            Assert.That(await inventory.AddItemAsync(item.ItemSlot, item).ConfigureAwait(false), Is.True);
        }

        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [helm.Id, staff.Id]);
        var (player, _) = CreateRemotePlayer();

        await StarterKitPlugIn.CheckAsync(player, kit, inventory, ExpiresAt.AddDays(-5)).ConfigureAwait(false);

        Assert.That(helm.Durability, Is.EqualTo(helm.GetMaximumDurabilityOfOnePiece()).And.GreaterThan(1));
        Assert.That(staff.Durability, Is.EqualTo(staff.GetMaximumDurabilityOfOnePiece()).And.GreaterThan(1));
        Assert.That(otherHelm.Durability, Is.EqualTo(1));
    }

    /// <summary>
    /// The periodic check sends the expiry of the kit items in the inventory again, at their slots,
    /// since the client forgets it whenever it gets the whole inventory, e.g. after a trade.
    /// </summary>
    [Test]
    public async Task CheckSendsTheExpiryAgainAsync()
    {
        var inventory = CreateInventory();
        var helm = Item(this._helmDefinition, InventoryConstants.HelmSlot);
        var staff = Item(this._staffDefinition, FirstBagSlot);
        var jewel = Item(this._jewelDefinition, 30);
        foreach (var item in new[] { helm, staff, jewel })
        {
            Assert.That(await inventory.AddItemAsync(item.ItemSlot, item).ConfigureAwait(false), Is.True);
        }

        var kit = new ActiveStarterKit(Guid.NewGuid(), ExpiresAt, [helm.Id, staff.Id]);
        var (player, output) = CreateRemotePlayer();

        await StarterKitPlugIn.CheckAsync(player, kit, inventory, ExpiresAt.AddDays(-5)).ConfigureAwait(false);
        await StarterKitPlugIn.CheckAsync(player, kit, inventory, ExpiresAt.AddDays(-5)).ConfigureAwait(false);

        byte[] list =
        [
            0xC1, 0x05, 0xD2, 0x11, 2,
            0xC1, 0x0C, 0xD2, 0x12, 0x02, 0x0E, 2, 0, .. ExpiryBytes,
            0xC1, 0x0C, 0xD2, 0x12, 0x01, 0x0A, 12, 0, .. ExpiryBytes,
        ];
        Assert.That(output.ToArray(), Is.EqualTo(list.Concat(list)));
    }

    /// <summary>
    /// The item code is the group times 512 plus the number, and the expiry is the same Unix time
    /// whether the time says it's UTC or not.
    /// </summary>
    /// <param name="group">The group of the item.</param>
    /// <param name="number">The number of the item.</param>
    /// <param name="expectedCode">The expected item code.</param>
    [TestCase(0, 0, 0)]
    [TestCase(7, 2, 3586)]
    [TestCase(13, 80, 6736)]
    [TestCase(15, 511, 8191)]
    public void ItemCodeAndUnixTime(int group, int number, int expectedCode)
    {
        var definition = new BasicModel.ItemDefinition { Group = (byte)group, Number = (short)number };

        var utc = PeriodItem.Of(definition, 12, ExpiresAt);
        var unspecified = PeriodItem.Of(definition, 12, DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Unspecified));

        Assert.That(utc, Is.EqualTo(new PeriodItem((ushort)expectedCode, 12, 1793534400)));
        Assert.That(unspecified, Is.EqualTo(utc));
    }

    private static Storage CreateInventory()
        => new(FirstBagSlot + (InventoryConstants.InventoryRows * InventoryConstants.RowSize), FirstBagSlot, 0, new BasicModel.ItemStorage());

    private static IGameServerContext CreateGameContext()
    {
        var manager = new PlugInManager(null, new NullLoggerFactory(), null, null);
        var gameContext = new Mock<IGameServerContext>();
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new Mock<IPersistenceContextProvider>().Object);
        gameContext.Setup(c => c.Configuration).Returns(new GameConfiguration());
        gameContext.Setup(c => c.PlugInManager).Returns(manager);
        gameContext.Setup(c => c.FeaturePlugIns).Returns(new FeaturePlugInContainer(manager));
        gameContext.Setup(c => c.LoggerFactory).Returns(new NullLoggerFactory());
        return gameContext.Object;
    }

    /// <summary>
    /// Creates a player of a game client, whose connection writes into the returned stream, like
    /// the remote view tests of OpenMU. Its connection is reached like on the server.
    /// </summary>
    private static (RemotePlayer Player, MemoryStream Output) CreateRemotePlayer()
    {
        var output = new MemoryStream();
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.Connected).Returns(true);
        connection.SetupGet(c => c.Output).Returns(PipeWriter.Create(output, new StreamPipeWriterOptions(leaveOpen: true)));
        connection.SetupGet(c => c.OutputLock).Returns(new AsyncLock());
        return (new RemotePlayer(CreateGameContext(), connection.Object, default), output);
    }

    private static BasicModel.Item Item(BasicModel.ItemDefinition definition, byte slot)
        => new() { Id = Guid.NewGuid(), Definition = definition, ItemSlot = slot };

    private static BasicModel.ItemDefinition Wearable(byte group, short number, int equipmentSlot)
    {
        var slotType = new BasicModel.ItemSlotType();
        slotType.ItemSlots.Add(equipmentSlot);
        return new() { Group = group, Number = number, Width = 1, Height = 1, Durability = 30, ItemSlot = slotType };
    }
}
