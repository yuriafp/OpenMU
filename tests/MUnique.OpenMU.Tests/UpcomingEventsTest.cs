// <copyright file="UpcomingEventsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests for the <see cref="PeriodicTaskConfiguration.GetNextStartUtc"/> and <see cref="UpcomingEvents"/>.
/// </summary>
[TestFixture]
public class UpcomingEventsTest
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Tests that the next start is the next time of the timetable today.
    /// </summary>
    [Test]
    public void NextStartIsLaterToday()
    {
        var configuration = CreateConfiguration(new TimeOnly(8, 0), new TimeOnly(14, 0), new TimeOnly(20, 0));

        Assert.That(configuration.GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.EqualTo(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the next start is the first time of the timetable tomorrow, when all times of today passed.
    /// </summary>
    [Test]
    public void NextStartIsTomorrow()
    {
        var configuration = CreateConfiguration(new TimeOnly(8, 0), new TimeOnly(10, 0));

        Assert.That(configuration.GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.EqualTo(new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the times of the timetable are in the time zone of the server.
    /// </summary>
    [Test]
    public void NextStartRespectsTheServerTimeZone()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var configuration = CreateConfiguration(new TimeOnly(16, 0));

        Assert.That(configuration.GetNextStartUtc(Now, timeZone), Is.EqualTo(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that there is no next start without timetable.
    /// </summary>
    [Test]
    public void NoNextStartWithoutTimetable()
    {
        Assert.That(CreateConfiguration().GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.Null);
    }

    /// <summary>
    /// Tests that the next start skips the days on which the event doesn't run. The 8th is a Thursday.
    /// </summary>
    [Test]
    public void NextStartSkipsTheDaysWithoutTheEvent()
    {
        var configuration = CreateConfiguration(new TimeOnly(14, 0));
        configuration.Days = WeekDays.Saturday;

        Assert.That(configuration.GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.EqualTo(new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the next start is today when the event runs today.
    /// </summary>
    [Test]
    public void NextStartIsTodayOnADayOfTheEvent()
    {
        var configuration = CreateConfiguration(new TimeOnly(14, 0));
        configuration.Days = WeekDays.Thursday | WeekDays.Saturday;

        Assert.That(configuration.GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.EqualTo(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the next start is a week later when the only day of the event is today and its times passed.
    /// </summary>
    [Test]
    public void NextStartIsNextWeek()
    {
        var configuration = CreateConfiguration(new TimeOnly(10, 0));
        configuration.Days = WeekDays.Thursday;

        Assert.That(configuration.GetNextStartUtc(Now, TimeZoneInfo.Utc), Is.EqualTo(new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the day of the week is the one of the server time zone: at 02:00 UTC on Thursday,
    /// it's still Wednesday at UTC-5, so an event on Wednesdays at 22:00 starts at 03:00 UTC.
    /// </summary>
    [Test]
    public void NextStartTakesTheDayInTheServerTimeZone()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");
        var configuration = CreateConfiguration(new TimeOnly(22, 0));
        configuration.Days = WeekDays.Wednesday;

        Assert.That(configuration.GetNextStartUtc(new DateTime(2026, 10, 8, 2, 0, 0, DateTimeKind.Utc), timeZone), Is.EqualTo(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that the event runs on all days when no day is selected, and only on the selected ones otherwise.
    /// </summary>
    /// <param name="days">The selected days.</param>
    /// <param name="day">The day.</param>
    /// <param name="expected">Whether the event runs on the day.</param>
    [TestCase(WeekDays.None, DayOfWeek.Sunday, true)]
    [TestCase(WeekDays.None, DayOfWeek.Wednesday, true)]
    [TestCase(WeekDays.Sunday, DayOfWeek.Sunday, true)]
    [TestCase(WeekDays.Sunday, DayOfWeek.Monday, false)]
    [TestCase(WeekDays.Friday | WeekDays.Saturday, DayOfWeek.Saturday, true)]
    [TestCase(WeekDays.Friday | WeekDays.Saturday, DayOfWeek.Thursday, false)]
    public void RunsOnTheSelectedDays(WeekDays days, DayOfWeek day, bool expected)
    {
        Assert.That(new PeriodicTaskConfiguration { Days = days }.RunsOn(day), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that the upcoming events contain the active scheduled plugins with their names, ordered by their start.
    /// </summary>
    [Test]
    public void UpcomingEventsAreOrderedAndNamed()
    {
        var plugInManager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        plugInManager.RegisterPlugInAtPlugInPoint<IPeriodicTaskPlugIn>(new GoldenInvasionPlugIn { Configuration = CreateInvasionConfiguration(new TimeOnly(18, 0)) });
        plugInManager.RegisterPlugInAtPlugInPoint<IPeriodicTaskPlugIn>(new RedDragonInvasionPlugIn { Configuration = CreateInvasionConfiguration(new TimeOnly(13, 0)) });

        var events = UpcomingEvents.Get(plugInManager, Now, TimeZoneInfo.Utc, TimeSpan.FromHours(12), CultureInfo.InvariantCulture);

        Assert.That(events.Select(e => e.Name), Is.EqualTo(new[] { "Red Dragon Invasion", "Golden Invasion" }));
        Assert.That(events[0].StartsAtUtc, Is.EqualTo(new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// Tests that events which start later than the requested time span aren't included.
    /// </summary>
    [Test]
    public void EventsAfterTheTimeSpanAreExcluded()
    {
        var plugInManager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        plugInManager.RegisterPlugInAtPlugInPoint<IPeriodicTaskPlugIn>(new GoldenInvasionPlugIn { Configuration = CreateInvasionConfiguration(new TimeOnly(18, 0)) });

        var events = UpcomingEvents.Get(plugInManager, Now, TimeZoneInfo.Utc, TimeSpan.FromHours(1), CultureInfo.InvariantCulture);

        Assert.That(events, Is.Empty);
    }

    private static PeriodicTaskConfiguration CreateConfiguration(params TimeOnly[] times)
    {
        return new PeriodicTaskConfiguration { Timetable = times.ToList() };
    }

    private static PeriodicInvasionConfiguration CreateInvasionConfiguration(params TimeOnly[] times)
    {
        return new PeriodicInvasionConfiguration { Timetable = times.ToList() };
    }
}
