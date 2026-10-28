// <copyright file="WeeklyGoalTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using System.Globalization;
using System.ComponentModel.DataAnnotations;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.WeeklyGoal;

/// <summary>
/// Tests the weekly goal: its weeks and turns, the steps of the progress, the reward, the messages
/// and the packet of its window.
/// </summary>
[TestFixture]
public class WeeklyGoalTest
{
    /// <summary>
    /// The week turns on Monday at 02:00 UTC, which is Sunday at 23:00 in Brasília, and lasts seven days.
    /// </summary>
    [Test]
    public void WeekTurnsOnMondayAtTwo()
    {
        var beforeTurn = WeeklyGoalWeek.Of(new DateTime(2026, 11, 2, 1, 59, 59, DateTimeKind.Utc));
        var atTurn = WeeklyGoalWeek.Of(new DateTime(2026, 11, 2, 2, 0, 0, DateTimeKind.Utc));

        Assert.That(beforeTurn.Date, Is.EqualTo(new DateOnly(2026, 10, 26)));
        Assert.That(atTurn.Date, Is.EqualTo(new DateOnly(2026, 11, 2)));
        Assert.That(atTurn.Start, Is.EqualTo(new DateTime(2026, 11, 2, 2, 0, 0, DateTimeKind.Utc)));
        Assert.That(atTurn.End, Is.EqualTo(new DateTime(2026, 11, 9, 2, 0, 0, DateTimeKind.Utc)));
        Assert.That(atTurn.Start.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(beforeTurn.End, Is.EqualTo(atTurn.Start));
        Assert.That(atTurn.Before(1), Is.EqualTo(beforeTurn));
        Assert.That(atTurn.Number, Is.EqualTo(beforeTurn.Number + 1));
    }

    /// <summary>
    /// The goals take turns, one per week, and come back after all of them; a goal without a target
    /// is skipped, and without goals there is none.
    /// </summary>
    [Test]
    public void GoalsTakeTurns()
    {
        var goals = WeeklyGoalConfiguration.CreateDefault().Goals;
        var week = WeeklyGoalWeek.Of(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
        var chosen = Enumerable.Range(0, goals.Count).Select(i => WeeklyGoalPlugIn.ChooseGoal(goals, week.Before(-i))).ToList();

        Assert.That(chosen, Is.EquivalentTo(goals));
        Assert.That(WeeklyGoalPlugIn.ChooseGoal(goals, week.Before(-goals.Count)), Is.SameAs(chosen[0]));

        var withoutTarget = new List<WeeklyGoalDefinition> { new() { Kind = WeeklyGoalKind.Resets, Target = 0 }, goals[0] };
        Assert.That(WeeklyGoalPlugIn.ChooseGoal(withoutTarget, week), Is.SameAs(goals[0]));
        Assert.That(WeeklyGoalPlugIn.ChooseGoal(new List<WeeklyGoalDefinition>(), week), Is.Null);
    }

    /// <summary>
    /// The default goals are the first guess of the plan for 4 to 6 friends, and every kind has its
    /// events in the registro.
    /// </summary>
    [Test]
    public void DefaultGoalsAreThePlannedOnes()
    {
        var goals = WeeklyGoalConfiguration.CreateDefault().Goals;

        Assert.That(goals.Select(goal => (goal.Kind, goal.Target)), Is.EqualTo(new[]
        {
            (WeeklyGoalKind.BloodCastle, 20),
            (WeeklyGoalKind.DevilSquare, 20),
            (WeeklyGoalKind.ChaosCastle, 10),
            (WeeklyGoalKind.Resets, 15),
            (WeeklyGoalKind.TreasureChests, 15),
        }));
        Assert.That(WeeklyGoalStore.GetFilter(WeeklyGoalKind.BloodCastle), Is.EqualTo(("minigame_end", (MiniGameType?)MiniGameType.BloodCastle)));
        Assert.That(WeeklyGoalStore.GetFilter(WeeklyGoalKind.ChaosCastle), Is.EqualTo(("minigame_start", (MiniGameType?)MiniGameType.ChaosCastle)));
        Assert.That(WeeklyGoalStore.GetFilter(WeeklyGoalKind.TreasureChests), Is.EqualTo(("chest", (MiniGameType?)null)));
        Assert.That(Enum.GetValues<WeeklyGoalKind>().Select(kind => WeeklyGoalStore.GetFilter(kind).EventKind), Is.All.Not.Empty);
    }

    /// <summary>
    /// The progress reaches its steps of 25%, and 100% only once the goal is recorded as reached.
    /// </summary>
    /// <param name="count">The count of the week.</param>
    /// <param name="step">The step of the messages, in percent.</param>
    /// <param name="isReached">Whether the goal is recorded as reached.</param>
    /// <param name="expected">The expected step.</param>
    [TestCase(0, 25, false, 0)]
    [TestCase(4, 25, false, 0)]
    [TestCase(5, 25, false, 25)]
    [TestCase(9, 25, false, 25)]
    [TestCase(10, 25, false, 50)]
    [TestCase(19, 25, false, 75)]
    [TestCase(25, 25, false, 75)]
    [TestCase(20, 25, true, 100)]
    [TestCase(15, 0, false, 0)]
    [TestCase(3, 0, true, 100)]
    public void ProgressReachesItsSteps(int count, int step, bool isReached, int expected)
    {
        var accounts = Enumerable.Range(0, count).ToDictionary(_ => Guid.NewGuid(), _ => 1);
        var progress = new WeeklyGoalProgress(
            WeeklyGoalWeek.Of(DateTime.UtcNow),
            new WeeklyGoalDefinition { Kind = WeeklyGoalKind.BloodCastle, Target = 20 },
            accounts,
            isReached ? DateTime.UtcNow : null);

        Assert.That(progress.Count, Is.EqualTo(count));
        Assert.That(progress.GetStep(step), Is.EqualTo(expected));
    }

    /// <summary>
    /// What an account counted comes from its contributions, and an account which counted nothing has 0.
    /// </summary>
    [Test]
    public void AccountCountsItsContribution()
    {
        var account = Guid.NewGuid();
        var progress = new WeeklyGoalProgress(
            WeeklyGoalWeek.Of(DateTime.UtcNow),
            new WeeklyGoalDefinition { Kind = WeeklyGoalKind.Resets, Target = 15 },
            new Dictionary<Guid, int> { [account] = 4, [Guid.NewGuid()] = 3 },
            null);

        Assert.That(progress.Count, Is.EqualTo(7));
        Assert.That(progress.Of(account), Is.EqualTo(4));
        Assert.That(progress.Of(Guid.NewGuid()), Is.Zero);
    }

    /// <summary>
    /// The default reward is a Jewel of Bless, a Jewel of Soul, a Box of Kundun +3 and 100 Goblin
    /// Points; what's 0 is left out of the items and the text.
    /// </summary>
    [Test]
    public void RewardHasTheConfiguredItems()
    {
        var configuration = WeeklyGoalConfiguration.CreateDefault();

        Assert.That(WeeklyGoalPlugIn.GetRewardItems(configuration), Is.EqualTo(new[]
        {
            new RewardItem(14, 13, 0, 1),
            new RewardItem(14, 14, 0, 1),
            new RewardItem(BoxOfKundun.Group, BoxOfKundun.Number, BoxOfKundun.LevelOf(3)),
        }));
        Assert.That(WeeklyGoalPlugIn.GetRewardText(configuration), Is.EqualTo("1 Jewel of Bless, 1 Jewel of Soul, 1 Box of Kundun +3 e 100 Goblin Points"));

        var onlyPoints = new WeeklyGoalConfiguration { GoblinPoints = 50 };
        Assert.That(WeeklyGoalPlugIn.GetRewardItems(onlyPoints), Is.Empty);
        Assert.That(WeeklyGoalPlugIn.GetRewardText(onlyPoints), Is.EqualTo("50 Goblin Points"));
        Assert.That(WeeklyGoalPlugIn.GetRewardText(new WeeklyGoalConfiguration { JewelsOfSoul = 2, BoxOfKundunPlus = 9 }), Is.EqualTo("2 Jewel of Soul"));
    }

    /// <summary>
    /// The default messages fit their arguments; a broken one falls back to the default, and an
    /// empty one shows nothing.
    /// </summary>
    [Test]
    public void MessagesFitTheirArguments()
    {
        var configuration = WeeklyGoalConfiguration.CreateDefault();

        Assert.That(string.Format(CultureInfo.InvariantCulture, configuration.NewGoalMessage.ValueInNeutralLanguage!, 20, "Blood Castles completos", 20, "08/11 às 23:00"), Is.EqualTo("Meta da semana: 20 Blood Castles completos! Quem somar ganha prêmio, e ao bater, todos ganham +20% de exp até 08/11 às 23:00. Veja com /meta."));
        Assert.That(string.Format(CultureInfo.InvariantCulture, configuration.ProgressMessage.ValueInNeutralLanguage!, 50, 10, 20, "Blood Castles completos"), Is.EqualTo("Meta da semana em 50%: 10 de 20 Blood Castles completos."));
        Assert.That(string.Format(CultureInfo.InvariantCulture, configuration.ReachedMessage.ValueInNeutralLanguage!, 20, "Blood Castles completos", 20, "08/11 às 23:00"), Does.StartWith("Meta da semana batida: 20 Blood Castles completos!"));
        Assert.That(WeeklyGoalPlugIn.Format(configuration.EntryMessage, configuration.EntryMessage, 12, 20, "Blood Castles completos", 3), Is.EqualTo("Meta da semana: 12 de 20 Blood Castles completos, e você somou 3. Veja com /meta."));
        Assert.That(WeeklyGoalPlugIn.Format(configuration.EntryReachedMessage, configuration.EntryReachedMessage, 20, "08/11 às 23:00"), Is.EqualTo("Meta da semana batida: +20% de exp até 08/11 às 23:00."));
        Assert.That(WeeklyGoalPlugIn.Format("Meta {9}", configuration.RewardMessage, "1 Jewel of Bless"), Is.EqualTo("Prêmio da meta da semana: 1 Jewel of Bless!"));
        Assert.That(WeeklyGoalPlugIn.Format(" ", configuration.RewardMessage, "1 Jewel of Bless"), Is.Null);
    }

    /// <summary>
    /// The packet of the window has the header of our client, the progress, the flags, the kind, the
    /// reward and the name, and doesn't keep old bytes of the buffer.
    /// </summary>
    [Test]
    public void PacketHasTheProgress()
    {
        var progress = new WeeklyGoalProgress(
            WeeklyGoalWeek.Of(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)),
            new WeeklyGoalDefinition { Kind = WeeklyGoalKind.BloodCastle, Target = 20, Name = "Blood Castles completos" },
            new Dictionary<Guid, int> { [Guid.NewGuid()] = 21 },
            DateTime.UtcNow);
        var buffer = Enumerable.Repeat((byte)0xFF, 300).ToArray();

        var length = WeeklyGoalPlugIn.WritePacket(buffer, progress, 7, true, WeeklyGoalConfiguration.CreateDefault());

        Assert.That(length, Is.EqualTo(100));
        Assert.That(buffer[..4], Is.EqualTo(new byte[] { 0xC1, 100, 0xFB, 0x01 }));
        Assert.That(buffer[length..], Is.All.EqualTo((byte)0xFF));
        var packet = new WeeklyGoalRef(buffer.AsSpan(0, length));
        Assert.That((packet.Kind, packet.JewelsOfBless, packet.JewelsOfSoul, packet.BoxOfKundunPlus, packet.GoblinPoints), Is.EqualTo((WeeklyGoalKind.BloodCastle, 1, 1, (byte)3, 100)));
        Assert.That(packet.Flags, Is.EqualTo(WeeklyGoalRef.WeeklyGoalFlags.HasGoal | WeeklyGoalRef.WeeklyGoalFlags.Reached | WeeklyGoalRef.WeeklyGoalFlags.Rewarded));
        Assert.That((packet.ExperienceBonusPercent, packet.Count, packet.Target, packet.Contribution), Is.EqualTo(((byte)20, 21, 20, 7)));
        Assert.That(packet.WeekEnd, Is.EqualTo((int)new DateTimeOffset(2026, 11, 9, 2, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()));
        Assert.That(packet.Name, Is.EqualTo("Blood Castles completos"));
    }

    /// <summary>
    /// The window of the client knows the kinds by their numbers, in the byte after the bonus.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="number">Its number in the packet.</param>
    [TestCase(WeeklyGoalKind.BloodCastle, 0)]
    [TestCase(WeeklyGoalKind.DevilSquare, 1)]
    [TestCase(WeeklyGoalKind.ChaosCastle, 2)]
    [TestCase(WeeklyGoalKind.Resets, 3)]
    [TestCase(WeeklyGoalKind.TreasureChests, 4)]
    public void KindKeepsItsNumberForTheClient(WeeklyGoalKind kind, int number)
    {
        var buffer = new byte[WeeklyGoalRef.Length];

        new WeeklyGoalRef(buffer) { Kind = kind };

        Assert.That(buffer[6], Is.EqualTo(number));
    }

    /// <summary>
    /// A text longer than its field is cut at a whole character, and keeps its ending zero.
    /// </summary>
    [Test]
    public void LongTextIsCutAtAWholeCharacter()
    {
        var buffer = new byte[WeeklyGoalRef.Length];
        var packet = new WeeklyGoalRef(buffer);

        // Two bytes per character, so 63 free bytes hold 31 of them.
        packet.Name = new string('ã', WeeklyGoalRef.NameLength);

        Assert.That(packet.Name, Is.EqualTo(new string('ã', 31)));
        Assert.That(buffer[WeeklyGoalRef.NameOffset + WeeklyGoalRef.NameLength - 1], Is.Zero);
    }

    /// <summary>
    /// A goal whose name the admin panel left empty still writes its packet, with an empty name.
    /// </summary>
    [Test]
    public void GoalWithoutNameHasAnEmptyName()
    {
        var progress = new WeeklyGoalProgress(
            WeeklyGoalWeek.Of(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)),
            new WeeklyGoalDefinition { Kind = WeeklyGoalKind.Resets, Target = 15, Name = null! },
            new Dictionary<Guid, int>(),
            null);
        var buffer = new byte[WeeklyGoalRef.Length];

        WeeklyGoalPlugIn.WritePacket(buffer, progress, 0, false, WeeklyGoalConfiguration.CreateDefault());

        Assert.That(new WeeklyGoalRef(buffer).Name, Is.Empty);
    }

    /// <summary>
    /// The experience bonus can't be configured above what the byte of the packet holds.
    /// </summary>
    [Test]
    public void BonusFitsItsByte()
    {
        var range = typeof(WeeklyGoalConfiguration).GetProperty(nameof(WeeklyGoalConfiguration.ExperienceBonusPercent))!
            .GetCustomAttributes(typeof(RangeAttribute), false).Cast<RangeAttribute>().Single();

        Assert.That(range.Maximum, Is.EqualTo((int)byte.MaxValue));
    }

    /// <summary>
    /// The configuration, with its goals and messages, survives a save/load round-trip through the
    /// plug-in configuration storage, with the JSON converters which the server registers at startup.
    /// </summary>
    [Test]
    public void ConfigurationSurvivesRoundTrip()
    {
        var registerConverter = !JsonConverterRegistry.Converters.OfType<LocalizedStringJsonConverter>().Any();
        if (registerConverter)
        {
            JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        }

        try
        {
            var stored = new PlugInConfiguration();
            stored.SetConfiguration(WeeklyGoalConfiguration.CreateDefault(), null);

            var configuration = stored.GetConfiguration<WeeklyGoalConfiguration>(null);
            var defaults = WeeklyGoalConfiguration.CreateDefault();

            Assert.That(configuration?.Goals.Select(goal => (goal.Kind, goal.Target, goal.Name)), Is.EqualTo(defaults.Goals.Select(goal => (goal.Kind, goal.Target, goal.Name))));
            Assert.That((configuration?.ExperienceBonusPercent, configuration?.BoxOfKundunPlus, configuration?.GoblinPoints, configuration?.AnnouncementStep), Is.EqualTo(((int?)20, (int?)3, (int?)100, (int?)25)));
            Assert.That(configuration?.ReachedMessage.ValueInNeutralLanguage, Is.EqualTo(defaults.ReachedMessage.ValueInNeutralLanguage));
            Assert.That(configuration?.EntryMessage, Is.EqualTo(defaults.EntryMessage));
        }
        finally
        {
            if (registerConverter)
            {
                JsonConverterRegistry.ClearConverters();
            }
        }
    }
}
