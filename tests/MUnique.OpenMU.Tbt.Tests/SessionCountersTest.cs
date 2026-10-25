// <copyright file="SessionCountersTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the counters of a stay in the world, which the exit of a human carries in the registro
/// for the calibration: the kills, the experience, the zen and the jewels.
/// </summary>
[TestFixture]
public class SessionCountersTest
{
    private static readonly BasicModel.ItemDefinition Bless = new() { Group = 14, Number = 13 };

    private static readonly BasicModel.ItemDefinition Soul = new() { Group = 14, Number = 14 };

    private static readonly BasicModel.ItemDefinition Chaos = new() { Group = 12, Number = 15 };

    private static readonly BasicModel.ItemDefinition Life = new() { Group = 14, Number = 16 };

    private static readonly BasicModel.ItemDefinition Creation = new() { Group = 14, Number = 22 };

    private static readonly BasicModel.ItemDefinition PackedBless = new() { Group = 12, Number = 30 };

    private static readonly BasicModel.ItemDefinition PackedSoul = new() { Group = 12, Number = 31 };

    private static readonly BasicModel.ItemDefinition PackedChaos = new() { Group = 12, Number = 141 };

    private static readonly BasicModel.ItemDefinition PackedLife = new() { Group = 12, Number = 136 };

    private static readonly BasicModel.ItemDefinition PackedCreation = new() { Group = 12, Number = 137 };

    private static readonly BasicModel.ItemDefinition Sword = new() { Group = 0, Number = 0 };

    /// <summary>
    /// A jewel counts one, whatever its level, a packed jewel ten per level, and other items, or
    /// an item without a definition, nothing.
    /// </summary>
    [Test]
    public void JewelsCountOneEachAndTenPerLevelOfAPack()
    {
        var singles = new[] { Item(Bless), Item(Bless, 3), Item(Soul), Item(Chaos), Item(Life), Item(Creation) };
        var packs = new[] { Item(PackedBless), Item(PackedSoul, 1), Item(PackedChaos, 2), Item(PackedLife, 1), Item(PackedCreation) };
        var others = new[] { Item(Sword, 13), new BasicModel.Item { Id = Guid.NewGuid() } };

        Assert.That(JewelCount.Of(singles), Is.EqualTo(new JewelCount(Bless: 2, Soul: 1, Chaos: 1, Life: 1, Creation: 1)));
        Assert.That(JewelCount.Of(packs), Is.EqualTo(new JewelCount(Bless: 10, Soul: 20, Chaos: 30, Life: 20, Creation: 10)));
        Assert.That(JewelCount.Of(others), Is.EqualTo(default(JewelCount)));
    }

    /// <summary>
    /// A character without an inventory, on an account without a vault, has no zen and no jewels.
    /// </summary>
    [Test]
    public void MissingStoragesCountNothing()
    {
        var account = new BasicModel.Account();
        var character = new BasicModel.Character();

        var json = JsonSerializer.Serialize(new SessionCounters(account, character).ToData(account, character));

        Assert.That(json, Does.Contain("\"zen_in\":0,\"zen_out\":0"));
        Assert.That(json, Does.Contain("\"jewels_out\":{\"bless\":0,\"soul\":0,\"chaos\":0,\"life\":0,\"creation\":0}"));
    }

    /// <summary>
    /// A kill counts for the player who killed, or for the owner of the Dark Raven which killed,
    /// and for nobody when no player killed.
    /// </summary>
    [Test]
    public void TheKillCountsForTheHunter()
    {
        var player = new BotPlayer(CreateGameContext());
        var raven = new Mock<IPlayerSurrogate>();
        raven.SetupGet(r => r.Owner).Returns(player);
        var otherAttacker = new Mock<IAttacker>();

        Assert.That(ActivityLogPlugIn.GetHunter(player), Is.SameAs(player));
        Assert.That(ActivityLogPlugIn.GetHunter(raven.As<IAttacker>().Object), Is.SameAs(player));
        Assert.That(ActivityLogPlugIn.GetHunter(otherAttacker.Object), Is.Null);
        Assert.That(ActivityLogPlugIn.GetHunter(null), Is.Null);
    }

    /// <summary>
    /// The data of a stay has the counted kills, experience and drops, and the experience, zen and
    /// jewels at the entry and at the exit, those in the inventory and the vault together.
    /// </summary>
    [Test]
    public void TheDataHasTheCountersAndTheEntryAndExit()
    {
        var (account, character) = CreateCharacter();
        var counters = new SessionCounters(account, character);

        counters.AddKill();
        counters.AddKill();
        counters.AddKill();
        counters.AddExperience(100);
        counters.AddExperience(250);
        counters.AddDroppedZen(70);
        counters.AddDroppedItem(Item(Bless));
        counters.AddDroppedItem(Item(Sword));
        character.Experience += 600;
        character.Inventory!.Money = 2000;
        character.Inventory.Items.Add(Item(Creation));

        var json = JsonSerializer.Serialize(counters.ToData(account, character));

        const string expected = """
            {"kills":3,"exp":350,"exp_in":1000,"exp_out":1600,"zen_drops":70,"zen_in":1500,"zen_out":3000,"jewel_drops":{"bless":1,"soul":0,"chaos":0,"life":0,"creation":0},"jewels_in":{"bless":1,"soul":1,"chaos":0,"life":0,"creation":0},"jewels_out":{"bless":1,"soul":1,"chaos":0,"life":0,"creation":1}}
            """;
        Assert.That(json, Is.EqualTo(expected));
    }

    /// <summary>
    /// The counters of a player end once: a second exit, or one without an entry, has no data.
    /// </summary>
    [Test]
    public void TheCountersEndOnce()
    {
        var (account, character) = CreateCharacter();
        var player = new BotPlayer(CreateGameContext());
        Assert.That(SessionCounters.Finish(player, account, character), Is.Null);

        SessionCounters.Start(player, account, character);
        SessionCounters.Of(player)!.AddKill();

        Assert.That(SessionCounters.Finish(player, account, character), Is.Not.Null);
        Assert.That(SessionCounters.Of(player), Is.Null);
        Assert.That(SessionCounters.Finish(player, account, character), Is.Null);
    }

    private static (BasicModel.Account Account, BasicModel.Character Character) CreateCharacter()
    {
        var vault = new BasicModel.ItemStorage { Money = 1000 };
        vault.Items.Add(Item(Bless));
        var inventory = new BasicModel.ItemStorage { Money = 500 };
        inventory.Items.Add(Item(Soul));
        inventory.Items.Add(Item(Sword));
        var account = new BasicModel.Account { Vault = vault };
        var character = new BasicModel.Character { Experience = 1000, Inventory = inventory };
        return (account, character);
    }

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

    private static BasicModel.Item Item(BasicModel.ItemDefinition definition, byte level = 0)
        => new() { Id = Guid.NewGuid(), Definition = definition, Level = level };
}
