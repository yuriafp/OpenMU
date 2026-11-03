// <copyright file="PeriodicTaskConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// Configuration for the <see cref="PeriodicTaskBasePlugIn{TConfiguration,TState}"/>.
/// </summary>
/// <remarks>
/// Display order values are spaced by inheritance level, so adding a property never
/// requires renumbering another level: this class uses 1–99, direct subclasses
/// 100–199, their subclasses 200–299, and so on.
/// </remarks>
public class PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets or sets a time delay.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_PreStartMessageDelay_Name), Order = 1)]
    public TimeSpan PreStartMessageDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the minimum time of the task duration.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_TaskDuration_Name), Order = 2)]
    public TimeSpan TaskDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the text which prints as a golden message in the game when task starts.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_StartMessage_Name), Order = 3)]
    public LocalizedString StartMessage { get; set; }

    /// <summary>
    /// Gets or sets the text which prints as a golden message in the game when task ends.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_EndMessage_Name), Order = 4)]
    public LocalizedString EndMessage { get; set; }

    /// <summary>
    /// Gets or sets a timetable for the event.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_Timetable_Name), Order = 5)]
    public IList<TimeOnly> Timetable { get; set; } = new List<TimeOnly>();

    /// <summary>
    /// Gets or sets the identifiers of the game servers on which the event runs.
    /// An empty list means the event runs on all game servers.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_AllowedServerIds_Name), Description = nameof(PlugInResources.PeriodicTaskConfiguration_AllowedServerIds_Description), Order = 6)]
    public IList<byte> AllowedServerIds { get; set; } = new List<byte>();

    /// <summary>
    /// Gets or sets the days of the week on which the event runs, in the time zone of the server.
    /// No day means every day, which is also what a configuration without this setting gets.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_Days_Name), Description = nameof(PlugInResources.PeriodicTaskConfiguration_Days_Description), Order = 7)]
    public WeekDays Days { get; set; }

    /// <summary>
    /// Gets or sets additional entries of the schedule, each with its own days, times and duration.
    /// They add to the <see cref="Timetable"/> (with its <see cref="Days"/>), which stays valid as it is.
    /// </summary>
    [Display(Name = "Schedule", Description = "Additional entries of the schedule, each with its own days, times and duration. They add to the timetable.", Order = 8)]
    public IList<PeriodicTaskScheduleEntry> Schedule { get; set; } = new List<PeriodicTaskScheduleEntry>();

    /// <summary>
    /// Gets a value indicating whether the configuration has at least one time at which the task starts.
    /// </summary>
    public bool HasSchedule => this.GetEntries().Any();

    /// <summary>
    /// Generate a sequence of time points like [00:00, 00:01, ...].
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <param name="startLimit">The start limit, inclusive.</param>
    /// <param name="endLimit">The end limit, exclusive.</param>
    public static IEnumerable<TimeOnly> GenerateTimeSequence(TimeSpan duration, TimeOnly? startLimit = null, TimeOnly? endLimit = null)
    {
        var limit = endLimit?.ToTimeSpan() ?? TimeSpan.FromDays(1);
        var current = startLimit?.ToTimeSpan() ?? TimeSpan.Zero;

        while (current < limit)
        {
            yield return TimeOnly.FromTimeSpan(current);
            current = current.Add(duration);
        }
    }

    /// <summary>
    /// Determines whether the event runs on the game server with the specified identifier.
    /// A missing (<c>null</c>) or empty list means the event runs on all servers.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <returns><c>true</c> if the event runs on the server; otherwise, <c>false</c>.</returns>
    public bool IsEnabledOnServer(byte serverId)
    {
        return this.AllowedServerIds is null or { Count: 0 } || this.AllowedServerIds.Contains(serverId);
    }

    /// <summary>
    /// Determines whether the event runs on the specified day of the week.
    /// </summary>
    /// <param name="day">The day of the week, in the time zone of the server.</param>
    /// <returns><c>true</c> if the event runs on the day; otherwise, <c>false</c>.</returns>
    public bool RunsOn(DayOfWeek day)
    {
        return this.Days == WeekDays.None || this.Days.HasFlag((WeekDays)(1 << (int)day));
    }

    /// <summary>
    /// Check if current time is OK for starting an invasion.
    /// </summary>
    /// <param name="serverTimeZone">
    /// The server time zone used to interpret the <see cref="Timetable"/> times of day.
    /// Pass <see cref="TimeZoneInfo.Utc"/> to keep the previous UTC-based behavior.
    /// </param>
    /// <returns>Returns true if the invasion can be started.</returns>
    public virtual bool IsItTimeToStart(TimeZoneInfo serverTimeZone)
    {
        return this.IsItTimeToStart(DateTime.UtcNow, serverTimeZone);
    }

    /// <summary>
    /// Check if the specified time is OK for starting the task: a time of the <see cref="Timetable"/> or of an entry
    /// of the <see cref="Schedule"/>, on a day on which that entry runs, has just come.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>Returns true if the task can be started.</returns>
    public bool IsItTimeToStart(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        return this.GetStartingEntry(utcNow, serverTimeZone) is not null;
    }

    /// <summary>
    /// Gets the duration of a run which starts at the specified time: the duration of the entry
    /// which starts it, or the <see cref="TaskDuration"/> when the entry has none.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>The duration; or <see langword="null"/>, if no time has just come.</returns>
    public TimeSpan? GetStartingRunDuration(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        return this.GetStartingEntry(utcNow, serverTimeZone) is { } entry
            ? entry.Duration ?? this.TaskDuration
            : null;
    }

    /// <summary>
    /// Gets how long a run which should have started earlier would still last, if the current time is
    /// inside a window of the schedule: a start time plus the duration of its entry.
    /// It makes it possible to continue a long window, e.g. after a restart of the server.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>The remaining time of the longest window; or <see langword="null"/>, if no window is open.</returns>
    public TimeSpan? GetRemainingRunTime(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone));
        TimeSpan? longest = null;
        foreach (var entry in this.GetEntries())
        {
            var duration = entry.Duration ?? this.TaskDuration;
            var daysBack = (int)Math.Ceiling(duration.TotalDays) + 1;
            for (var date = today.AddDays(-daysBack); date <= today; date = date.AddDays(1))
            {
                foreach (var start in GetStartsUtc(entry, date, serverTimeZone))
                {
                    var remaining = start + duration - utcNow;
                    if (start <= utcNow && remaining > TimeSpan.Zero && (longest is null || remaining > longest))
                    {
                        longest = remaining;
                    }
                }
            }
        }

        return longest;
    }

    /// <summary>
    /// Gets the number of the run which starts at the specified time, counted in the starts of the week
    /// and the weeks since the year 1: consecutive runs have consecutive numbers, whatever the schedule of
    /// the days is. It lets things take turns per run, e.g. the themes of an invasion.
    /// </summary>
    /// <param name="utcNow">The time of the start, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>The number of the run, starting with 0.</returns>
    public long GetRunNumber(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone));

        // Day number 0 is the 1st of January of the year 1, a Monday.
        var monday = today.AddDays(-(today.DayNumber % 7));
        var weekStarts = Enumerable.Range(0, 7)
            .SelectMany(offset => this.GetStartsUtc(monday.AddDays(offset), serverTimeZone))
            .OrderBy(start => start)
            .ToList();
        var startsBefore = weekStarts.Count(start => start <= utcNow);
        return ((long)(monday.DayNumber / 7) * weekStarts.Count) + Math.Max(0, startsBefore - 1);
    }

    /// <summary>
    /// Gets the next start time of the task, according to the <see cref="Timetable"/>, the <see cref="Days"/> and the <see cref="Schedule"/>.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>The next start time, in UTC; or <see langword="null"/>, if there is no time.</returns>
    public DateTime? GetNextStartUtc(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone));

        // Today, the remaining times of the timetable can still come; otherwise the first one of the
        // next day on which the event runs, which is at most a week away.
        for (var date = today; date <= today.AddDays(7); date = date.AddDays(1))
        {
            var nextStartUtc = this.GetStartsUtc(date, serverTimeZone).Where(start => start > utcNow).Select(start => (DateTime?)start).Min();
            if (nextStartUtc is not null)
            {
                return nextStartUtc;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the start times of the task on a day, if it runs on that day.
    /// </summary>
    /// <param name="date">The day, in the time zone of the server.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times are defined.</param>
    /// <returns>The start times on the day, in UTC.</returns>
    public IEnumerable<DateTime> GetStartsUtc(DateOnly date, TimeZoneInfo serverTimeZone)
    {
        return this.GetEntries().SelectMany(entry => GetStartsUtc(entry, date, serverTimeZone));
    }

    private static IEnumerable<DateTime> GetStartsUtc(PeriodicTaskScheduleEntry entry, DateOnly date, TimeZoneInfo serverTimeZone)
    {
        if (!entry.RunsOn(date.DayOfWeek))
        {
            yield break;
        }

        foreach (var time in entry.Timetable)
        {
            // A time which the clock skips when it moves forward never comes on that day.
            var startLocal = date.ToDateTime(time, DateTimeKind.Unspecified);
            if (!serverTimeZone.IsInvalidTime(startLocal))
            {
                yield return TimeZoneInfo.ConvertTimeToUtc(startLocal, serverTimeZone);
            }
        }
    }

    /// <summary>
    /// Gets the entries with at least one time: the <see cref="Timetable"/> with the <see cref="Days"/> as the
    /// first one, then the entries of the <see cref="Schedule"/>.
    /// </summary>
    private IEnumerable<PeriodicTaskScheduleEntry> GetEntries()
    {
        if (this.Timetable is { Count: > 0 })
        {
            yield return new PeriodicTaskScheduleEntry { Days = this.Days, Timetable = this.Timetable };
        }

        foreach (var entry in this.Schedule ?? [])
        {
            if (entry?.Timetable is { Count: > 0 })
            {
                yield return entry;
            }
        }
    }

    private PeriodicTaskScheduleEntry? GetStartingEntry(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        if (!this.HasSchedule)
        {
            return null;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone);
        var nowTime = TimeOnly.FromDateTime(nowLocal);
        var earlier = nowTime.Add(TimeSpan.FromSeconds(-5));

        // For example, p = 00:00. Check that time between 00:00:00 and 00:00:05
        return this.GetEntries().FirstOrDefault(entry => entry.RunsOn(nowLocal.DayOfWeek) && entry.Timetable.Any(p => p.IsBetween(earlier, nowTime)));
    }
}
