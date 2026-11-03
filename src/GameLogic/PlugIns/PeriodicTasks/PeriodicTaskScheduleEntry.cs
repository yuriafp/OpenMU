// <copyright file="PeriodicTaskScheduleEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// One entry of the <see cref="PeriodicTaskConfiguration.Schedule"/>: times of the day on some days of the week,
/// optionally with an own duration. It makes a week with different days possible, e.g. a denser weekend,
/// which a single timetable and a single set of days can't express.
/// </summary>
public class PeriodicTaskScheduleEntry
{
    /// <summary>
    /// Gets or sets the days of the week, in the time zone of the server, on which the times of this entry start the task.
    /// No day means every day.
    /// </summary>
    [Display(Name = "Days", Description = "The days of the week, in the time zone of the server, on which the times of this entry start the task. No day means every day.", Order = 1)]
    public WeekDays Days { get; set; }

    /// <summary>
    /// Gets or sets the times of the day, in the time zone of the server, at which the task starts.
    /// </summary>
    [Display(Name = "Timetable", Description = "The times of the day, in the time zone of the server, at which the task starts.", Order = 2)]
    public IList<TimeOnly> Timetable { get; set; } = new List<TimeOnly>();

    /// <summary>
    /// Gets or sets the duration of the task when it starts through this entry.
    /// Without a value, the task duration of the configuration applies.
    /// </summary>
    [Display(Name = "Duration", Description = "The duration of the task when it starts through this entry. Without a value, the task duration of the configuration applies.", Order = 3)]
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Determines whether the entry starts the task on the specified day of the week.
    /// </summary>
    /// <param name="day">The day of the week, in the time zone of the server.</param>
    /// <returns><c>true</c> if the entry starts the task on the day; otherwise, <c>false</c>.</returns>
    public bool RunsOn(DayOfWeek day)
    {
        return this.Days == WeekDays.None || this.Days.HasFlag((WeekDays)(1 << (int)day));
    }
}
