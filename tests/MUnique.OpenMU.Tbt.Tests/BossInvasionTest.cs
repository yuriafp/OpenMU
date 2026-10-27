// <copyright file="BossInvasionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.BossInvasion;

/// <summary>
/// Tests the boss invasion: its default configurations, the turns of the themes and the places of the escort.
/// </summary>
[TestFixture]
public class BossInvasionTest
{
    /// <summary>
    /// The mid game invasion is on Wednesdays and the late game one on Saturdays, both at 21:30 in
    /// Brasília (00:30 of the next day in UTC), announced five minutes before, for half an hour.
    /// </summary>
    [Test]
    public void DefaultConfigurationsFollowThePlan()
    {
        var mid = BossInvasionConfiguration.CreateMidGameDefault();
        var late = BossInvasionConfiguration.CreateLateGameDefault();

        foreach (var configuration in new[] { mid, late })
        {
            Assert.That(configuration.Timetable, Is.EqualTo(new[] { new TimeOnly(0, 30) }));
            Assert.That(configuration.PreStartMessageDelay, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(configuration.TaskDuration, Is.EqualTo(TimeSpan.FromMinutes(30)));
        }

        Assert.That(mid.Days, Is.EqualTo(WeekDays.Thursday));
        Assert.That(late.Days, Is.EqualTo(WeekDays.Sunday));
        Assert.That(mid.Themes, Has.Count.EqualTo(5));
        Assert.That(late.Themes, Has.Count.EqualTo(6));
    }

    /// <summary>
    /// Every default theme has a boss with a prize on each of its maps, with the health of its definition
    /// times the multiplier, 20; the late game bosses have better boxes than the mid game ones. Only the
    /// Season 8 bosses have skills, and only escorts come when called.
    /// </summary>
    [Test]
    public void DefaultThemesHaveABossOnEachMap()
    {
        var mid = BossInvasionConfiguration.CreateMidGameDefault();
        var late = BossInvasionConfiguration.CreateLateGameDefault();

        foreach (var theme in mid.Themes.Concat(late.Themes))
        {
            Assert.That(theme.Name, Is.Not.Empty);
            foreach (var spawnsOfMap in theme.Spawns.GroupBy(spawn => spawn.Map))
            {
                Assert.That(spawnsOfMap.Any(spawn => spawn.IsBoss), $"{theme.Name}, map {spawnsOfMap.Key}");
            }

            Assert.That(theme.Spawns.Where(spawn => spawn.IsBoss), Has.All.Matches<BossInvasionSpawn>(spawn => spawn.Health == 0 && spawn.PrizeBoxes > 0 && !spawn.OnlyCalled), theme.Name);
            Assert.That(theme.Spawns.Where(spawn => !spawn.IsBoss), Has.All.Matches<BossInvasionSpawn>(spawn => spawn.Health == 0 && spawn.PrizeBoxes == 0 && !spawn.HasSkills), theme.Name);
        }

        Assert.That((mid.BossHealthMultiplier, late.BossHealthMultiplier), Is.EqualTo((20, 20)));
        Assert.That(Spawns(mid, late).Where(spawn => spawn.HasSkills).Select(spawn => spawn.MonsterNumber), Has.All.InRange(1003, 1009));
        Assert.That(mid.Themes.SelectMany(t => t.Spawns).Where(s => s.IsBoss).Select(s => s.PrizeBoxPlus).Distinct(), Is.EqualTo(new[] { 4 }));
        Assert.That(late.Themes.SelectMany(t => t.Spawns).Where(s => s.IsBoss).Select(s => s.PrizeBoxPlus).Distinct(), Is.EqualTo(new[] { 5 }));
    }

    /// <summary>
    /// The Season 8 bosses are our monsters from 1003 on, all with skills; the five elementals are each
    /// alone on a map of their own, with a box for the party which kills them, and call two kinds of
    /// monsters of their map.
    /// </summary>
    [Test]
    public void Season8BossesAreOurMonsters()
    {
        var midBosses = BossInvasionConfiguration.CreateMidGameDefault().Themes.SelectMany(theme => theme.Spawns).Where(spawn => spawn.IsBoss);
        var elementalTheme = BossInvasionConfiguration.CreateLateGameDefault().Themes.Single(theme => theme.Name == "Invasão elemental").Spawns;
        var elementals = elementalTheme.Where(spawn => spawn.IsBoss).ToList();

        Assert.That(midBosses.Where(spawn => spawn.HasSkills).Select(spawn => spawn.MonsterNumber), Is.EquivalentTo(new short[] { 1003, 1009 }));
        Assert.That(elementals.Select(spawn => spawn.MonsterNumber), Is.EqualTo(new short[] { 1004, 1005, 1006, 1007, 1008 }));
        Assert.That(elementals.Select(spawn => spawn.Map).Distinct().Count(), Is.EqualTo(elementals.Count));
        Assert.That(elementals, Has.All.Matches<BossInvasionSpawn>(spawn => spawn.HasSkills && spawn.PrizeBoxes == 1));
        foreach (var elemental in elementals)
        {
            var calledEscorts = elementalTheme.Where(spawn => !spawn.IsBoss && spawn.Map == elemental.Map).ToList();
            Assert.That(calledEscorts, Has.Count.EqualTo(2).And.All.Matches<BossInvasionSpawn>(spawn => spawn.OnlyCalled), $"map {elemental.Map}");
        }
    }

    /// <summary>
    /// The health of its spawn wins; otherwise a boss gets the health of its definition times the
    /// multiplier, at least once and at most the highest integer, and an escort keeps the health of
    /// its definition.
    /// </summary>
    [Test]
    public void BossesGetTheHealthOfTheirDefinitionTimesTheMultiplier()
    {
        var boss = new BossInvasionSpawn { IsBoss = true };
        var escort = new BossInvasionSpawn();

        Assert.That(BossInvasionPlugInBase.GetHealth(boss, 95_000, 20), Is.EqualTo(1_900_000));
        Assert.That(BossInvasionPlugInBase.GetHealth(boss, 200_000_000, 20), Is.EqualTo(int.MaxValue));
        Assert.That(BossInvasionPlugInBase.GetHealth(boss, 0, 20), Is.Null);
        Assert.That(BossInvasionPlugInBase.GetHealth(boss, 95_000, 0), Is.EqualTo(95_000));
        Assert.That(BossInvasionPlugInBase.GetHealth(escort, 95_000, 20), Is.Null);
        Assert.That(BossInvasionPlugInBase.GetHealth(new BossInvasionSpawn { IsBoss = true, Health = 1_000 }, 95_000, 20), Is.EqualTo(1_000));
        Assert.That(BossInvasionPlugInBase.GetHealth(new BossInvasionSpawn { Health = 1_000 }, 95_000, 20), Is.EqualTo(1_000));
    }

    /// <summary>
    /// A boss with skills calls its escort once at half of its health and once at a quarter, in any
    /// order of the configuration; a health of 0 is no call. Both calls are due when a hit goes past both.
    /// </summary>
    [Test]
    public void BossCallsItsEscortAtItsCallHealths()
    {
        var configuration = new BossInvasionConfiguration();
        var healths = InvasionBossIntelligence.GetEscortCallHealths(configuration);

        Assert.That(healths, Is.EqualTo(new[] { 50, 25 }));
        Assert.That(new[] { 100, 51, 50, 30, 25, 1 }.Select(health => InvasionBossIntelligence.GetDueEscortCalls(healths, health)), Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2 }));
        Assert.That(InvasionBossIntelligence.GetEscortCallHealths(new BossInvasionConfiguration { EscortCallHealthPercentage = 20, LastEscortCallHealthPercentage = 60 }), Is.EqualTo(new[] { 60, 20 }));
        Assert.That(InvasionBossIntelligence.GetEscortCallHealths(new BossInvasionConfiguration { EscortCallHealthPercentage = 0 }), Is.EqualTo(new[] { 25 }));
    }

    /// <summary>
    /// A raging boss acts faster by its speed bonus: 50% faster is two thirds of the interval.
    /// </summary>
    [Test]
    public void FuryMakesTheBossFaster()
    {
        Assert.That(InvasionBossIntelligence.GetFuryInterval(TimeSpan.FromSeconds(9), 50), Is.EqualTo(TimeSpan.FromSeconds(6)));
        Assert.That(InvasionBossIntelligence.GetFuryInterval(TimeSpan.FromSeconds(9), 0), Is.EqualTo(TimeSpan.FromSeconds(9)));
    }

    /// <summary>
    /// The configuration, with its themes, survives a save/load round-trip through the plug-in
    /// configuration storage, with the JSON converters which the server registers at startup.
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
            stored.SetConfiguration(BossInvasionConfiguration.CreateLateGameDefault(), null);

            var configuration = stored.GetConfiguration<BossInvasionConfiguration>(null);

            Assert.That(configuration?.Days, Is.EqualTo(WeekDays.Sunday));
            Assert.That(configuration?.Themes.Select(theme => theme.Name), Is.EqualTo(BossInvasionConfiguration.CreateLateGameDefault().Themes.Select(theme => theme.Name)));
            var balgass = configuration?.Themes[1].Spawns[0];
            Assert.That((balgass?.MonsterNumber, balgass?.Map, balgass?.IsBoss, balgass?.Health, balgass?.PrizeBoxes, balgass?.PrizeBoxPlus), Is.EqualTo(((short?)349, (short?)34, (bool?)true, (int?)0, (int?)2, (int?)5)));
            var elementals = configuration?.Themes[5].Spawns;
            Assert.That((elementals?[0].HasSkills, elementals?[1].OnlyCalled, configuration?.BossHealthMultiplier, configuration?.SkillInterval), Is.EqualTo(((bool?)true, (bool?)true, (int?)20, (TimeSpan?)TimeSpan.FromSeconds(10))));
            Assert.That(configuration?.DefeatedMessage.ValueInNeutralLanguage, Is.EqualTo(BossInvasionConfiguration.CreateLateGameDefault().DefeatedMessage.ValueInNeutralLanguage));
            Assert.That(configuration?.NoPrize.ValueInNeutralLanguage, Is.EqualTo(BossInvasionConfiguration.CreateLateGameDefault().NoPrize.ValueInNeutralLanguage));
        }
        finally
        {
            if (registerConverter)
            {
                JsonConverterRegistry.ClearConverters();
            }
        }
    }

    /// <summary>
    /// The themes take turns week by week, from Monday on in UTC: the whole week has the same theme.
    /// </summary>
    [Test]
    public void ThemesTakeTurnsWeekByWeek()
    {
        var themes = new[] { "A", "B", "C" }.Select(name => new BossInvasionTheme { Name = name }).ToList();
        var monday = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

        string? Chosen(DateTime utcNow) => BossInvasionPlugInBase.ChooseTheme(themes, utcNow, _ => true)?.Name;

        var first = Chosen(monday);
        Assert.That(Chosen(monday.AddDays(6).AddHours(23)), Is.EqualTo(first));
        var turns = Enumerable.Range(0, 3).Select(week => Chosen(monday.AddDays(7 * week).AddHours(1))).ToList();
        Assert.That(turns, Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(Chosen(monday.AddDays(21)), Is.EqualTo(first));
        Assert.That(Chosen(monday.AddMinutes(-1)), Is.Not.EqualTo(first));
    }

    /// <summary>
    /// A theme which can't run gives its turn to the next one; without any, there's no theme.
    /// </summary>
    [Test]
    public void ThemeWhichCantRunGivesItsTurn()
    {
        var themes = new[] { "A", "B", "C" }.Select(name => new BossInvasionTheme { Name = name }).ToList();
        var monday = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        var first = BossInvasionPlugInBase.ChooseTheme(themes, monday, _ => true)!;
        var next = themes[(themes.IndexOf(first) + 1) % themes.Count];

        Assert.That(BossInvasionPlugInBase.ChooseTheme(themes, monday, theme => theme != first), Is.SameAs(next));
        Assert.That(BossInvasionPlugInBase.ChooseTheme(themes, monday, _ => false), Is.Null);
        Assert.That(BossInvasionPlugInBase.ChooseTheme([], monday, _ => true), Is.Null);
    }

    /// <summary>
    /// The escort appears within the radius around the boss, in both directions.
    /// </summary>
    [Test]
    public void EscortAppearsAroundTheBoss()
    {
        var places = new[] { new Point(100, 100), new Point(106, 94), new Point(107, 100), new Point(100, 93), new Point(150, 150) };

        var around = BossInvasionPlugInBase.GetPlacesAround(places, new Point(100, 100), 6);

        Assert.That(around, Is.EquivalentTo(new[] { new Point(100, 100), new Point(106, 94) }));
    }

    /// <summary>
    /// The boxes +1 to +5 are the levels 8 to 12 of the Box of Kundun.
    /// </summary>
    [Test]
    public void BoxesOfKundunHaveTheirLevels()
    {
        Assert.That(Enumerable.Range(1, 5).Select(BoxOfKundun.LevelOf), Is.EqualTo(new byte[] { 8, 9, 10, 11, 12 }));
        Assert.That(BoxOfKundun.PlusOf(12), Is.EqualTo(5));
        Assert.That(BoxOfKundun.NameOf(4), Is.EqualTo("Box of Kundun +4"));
    }

    private static IEnumerable<BossInvasionSpawn> Spawns(params BossInvasionConfiguration[] configurations)
        => configurations.SelectMany(configuration => configuration.Themes).SelectMany(theme => theme.Spawns);
}
