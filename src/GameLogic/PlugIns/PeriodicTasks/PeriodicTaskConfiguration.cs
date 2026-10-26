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
        if (this.Timetable.Count == 0)
        {
            return false;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, serverTimeZone);
        if (!this.RunsOn(nowLocal.DayOfWeek))
        {
            return false;
        }

        var nowTime = TimeOnly.FromDateTime(nowLocal);
        var earlier = nowTime.Add(TimeSpan.FromSeconds(-5));

        // For example, p = 00:00. Check that time between 00:00:00 and 00:00:05
        return this.Timetable.Any(p => p.IsBetween(earlier, nowTime));
    }

    /// <summary>
    /// Gets the next start time of the task, according to the <see cref="Timetable"/> and the <see cref="Days"/>.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times of the <see cref="Timetable"/> are defined.</param>
    /// <returns>The next start time, in UTC; or <see langword="null"/>, if the timetable is empty.</returns>
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
    /// <param name="serverTimeZone">The time zone of the server, in which the times of the <see cref="Timetable"/> are defined.</param>
    /// <returns>The start times on the day, in UTC.</returns>
    public IEnumerable<DateTime> GetStartsUtc(DateOnly date, TimeZoneInfo serverTimeZone)
    {
        if (!this.RunsOn(date.DayOfWeek))
        {
            yield break;
        }

        foreach (var time in this.Timetable)
        {
            // A time which the clock skips when it moves forward never comes on that day.
            var startLocal = date.ToDateTime(time, DateTimeKind.Unspecified);
            if (!serverTimeZone.IsInvalidTime(startLocal))
            {
                yield return TimeZoneInfo.ConvertTimeToUtc(startLocal, serverTimeZone);
            }
        }
    }
}