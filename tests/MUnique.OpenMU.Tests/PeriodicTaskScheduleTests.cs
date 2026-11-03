// <copyright file="PeriodicTaskScheduleTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// Tests the <see cref="PeriodicTaskConfiguration.Schedule"/>: entries with their own days, times and duration.
/// </summary>
[TestFixture]
public class PeriodicTaskScheduleTests
{
    // A Saturday.
    private static readonly DateTime Saturday = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The starts of a day are the ones of the entries which run on that day: the weekdays and the weekend differ.
    /// </summary>
    [Test]
    public void StartsFollowTheDaysOfTheEntries()
    {
        var configuration = CreateWeekAndWeekendConfiguration();

        Assert.That(Starts(configuration, Saturday.AddDays(-1)), Is.EqualTo(new[] { "22:00" }));
        Assert.That(Starts(configuration, Saturday), Is.EqualTo(new[] { "03:00" }));
        Assert.That(Starts(configuration, Saturday.AddDays(1)), Is.Empty);
        Assert.That(Starts(configuration, Saturday.AddDays(2)), Is.EqualTo(new[] { "22:00" }));
    }

    /// <summary>
    /// A configuration with a timetable only keeps working as before; the timetable adds to the schedule.
    /// </summary>
    [Test]
    public void TimetableAndScheduleAdd()
    {
        var configuration = new PeriodicTaskConfiguration
        {
            Timetable = [new TimeOnly(8, 0)],
            Days = WeekDays.Saturday,
            Schedule = [new PeriodicTaskScheduleEntry { Timetable = [new TimeOnly(9, 0)] }],
        };

        Assert.That(configuration.HasSchedule, Is.True);
        Assert.That(Starts(configuration, Saturday), Is.EqualTo(new[] { "08:00", "09:00" }));
        Assert.That(Starts(configuration, Saturday.AddDays(1)), Is.EqualTo(new[] { "09:00" }));
        Assert.That(new PeriodicTaskConfiguration().HasSchedule, Is.False);
    }

    /// <summary>
    /// The task starts at the times of the entry, on its days only, and runs for the duration of the entry.
    /// </summary>
    [Test]
    public void StartAndDurationComeFromTheEntry()
    {
        var configuration = CreateWeekAndWeekendConfiguration();
        var weekendStart = Saturday.AddHours(3).AddSeconds(2);
        var weekdayStart = Saturday.AddDays(-1).AddHours(22).AddSeconds(2);

        Assert.That(configuration.IsItTimeToStart(weekendStart, TimeZoneInfo.Utc), Is.True);
        Assert.That(configuration.GetStartingRunDuration(weekendStart, TimeZoneInfo.Utc), Is.EqualTo(TimeSpan.FromHours(48)));
        Assert.That(configuration.IsItTimeToStart(weekdayStart, TimeZoneInfo.Utc), Is.True);
        Assert.That(configuration.GetStartingRunDuration(weekdayStart, TimeZoneInfo.Utc), Is.EqualTo(TimeSpan.FromHours(3)));

        // The time of the weekend entry on a weekday, and the time of the weekday entry on the weekend.
        Assert.That(configuration.IsItTimeToStart(Saturday.AddDays(-2).AddHours(3).AddSeconds(2), TimeZoneInfo.Utc), Is.False);
        Assert.That(configuration.IsItTimeToStart(Saturday.AddHours(22).AddSeconds(2), TimeZoneInfo.Utc), Is.False);
        Assert.That(configuration.GetStartingRunDuration(Saturday.AddHours(22).AddSeconds(2), TimeZoneInfo.Utc), Is.Null);
    }

    /// <summary>
    /// The next start looks into all entries.
    /// </summary>
    [Test]
    public void NextStartConsidersAllEntries()
    {
        var configuration = CreateWeekAndWeekendConfiguration();

        // Friday evening: the next one is the weekend window of Saturday 03:00 UTC.
        Assert.That(configuration.GetNextStartUtc(Saturday.AddDays(-1).AddHours(23), TimeZoneInfo.Utc), Is.EqualTo(Saturday.AddHours(3)));

        // Sunday: the weekday window of Monday.
        Assert.That(configuration.GetNextStartUtc(Saturday.AddDays(1).AddHours(12), TimeZoneInfo.Utc), Is.EqualTo(Saturday.AddDays(2).AddHours(22)));
    }

    /// <summary>
    /// A window which is open is found with its remaining time, also a long one which started days ago.
    /// </summary>
    [Test]
    public void RemainingRunTimeIsFoundInsideAWindow()
    {
        var configuration = CreateWeekAndWeekendConfiguration();

        Assert.That(configuration.GetRemainingRunTime(Saturday.AddDays(1).AddHours(12), TimeZoneInfo.Utc), Is.EqualTo(TimeSpan.FromHours(15)));
        Assert.That(configuration.GetRemainingRunTime(Saturday.AddDays(2).AddHours(2).AddMinutes(59), TimeZoneInfo.Utc), Is.EqualTo(TimeSpan.FromMinutes(1)));
        Assert.That(configuration.GetRemainingRunTime(Saturday.AddDays(2).AddHours(3), TimeZoneInfo.Utc), Is.Null);
        Assert.That(configuration.GetRemainingRunTime(Saturday.AddDays(2).AddHours(23), TimeZoneInfo.Utc), Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(configuration.GetRemainingRunTime(Saturday.AddDays(2).AddHours(12), TimeZoneInfo.Utc), Is.Null);
    }

    /// <summary>
    /// Consecutive runs have consecutive numbers, over the days and the weeks, whatever the schedule of the days is.
    /// </summary>
    [Test]
    public void RunNumbersAreConsecutive()
    {
        var configuration = new PeriodicTaskConfiguration
        {
            Schedule =
            [
                new PeriodicTaskScheduleEntry { Days = WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday | WeekDays.Friday, Timetable = [new TimeOnly(10, 0), new TimeOnly(20, 0)] },
                new PeriodicTaskScheduleEntry { Days = WeekDays.Saturday | WeekDays.Sunday, Timetable = [new TimeOnly(9, 0), new TimeOnly(15, 0), new TimeOnly(21, 0)] },
            ],
        };
        var monday = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        var starts = Enumerable.Range(0, 28)
            .SelectMany(day => configuration.GetStartsUtc(DateOnly.FromDateTime(monday.AddDays(day)), TimeZoneInfo.Utc))
            .OrderBy(start => start)
            .ToList();

        var numbers = starts.Select(start => configuration.GetRunNumber(start.AddSeconds(1), TimeZoneInfo.Utc)).ToList();

        Assert.That(numbers.Zip(numbers.Skip(1), (a, b) => b - a), Is.All.EqualTo(1));
        Assert.That(starts, Has.Count.EqualTo(64));
    }

    private static PeriodicTaskConfiguration CreateWeekAndWeekendConfiguration()
    {
        // Monday to Friday 19:00 (BRT) for 3 hours, and the whole weekend (Saturday 00:00 BRT) for 48 hours.
        return new PeriodicTaskConfiguration
        {
            TaskDuration = TimeSpan.FromHours(1),
            Schedule =
            [
                new PeriodicTaskScheduleEntry
                {
                    Days = WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday | WeekDays.Friday,
                    Timetable = [new TimeOnly(22, 0)],
                    Duration = TimeSpan.FromHours(3),
                },
                new PeriodicTaskScheduleEntry
                {
                    Days = WeekDays.Saturday,
                    Timetable = [new TimeOnly(3, 0)],
                    Duration = TimeSpan.FromHours(48),
                },
            ],
        };
    }

    private static IEnumerable<string> Starts(PeriodicTaskConfiguration configuration, DateTime day)
    {
        return configuration.GetStartsUtc(DateOnly.FromDateTime(day), TimeZoneInfo.Utc)
            .OrderBy(start => start)
            .Select(start => start.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
    }
}
