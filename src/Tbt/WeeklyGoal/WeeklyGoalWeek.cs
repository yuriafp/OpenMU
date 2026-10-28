// <copyright file="WeeklyGoalWeek.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// A week of the goals, which turns on Monday at 02:00 UTC, like the weekly snapshot.
/// </summary>
/// <param name="Date">The Monday which starts the week, its key in the database.</param>
internal readonly record struct WeeklyGoalWeek(DateOnly Date)
{
    private const int DaysPerWeek = 7;

    /// <summary>
    /// Gets the start of the week, in UTC.
    /// </summary>
    public DateTime Start => this.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) + WeeklySnapshotPlugIn.WeekTurnOffset;

    /// <summary>
    /// Gets the end of the week, in UTC: the start of the next one.
    /// </summary>
    public DateTime End => this.Start.AddDays(DaysPerWeek);

    /// <summary>
    /// Gets the number of the week since the 1st of January of the year 1, a Monday, for the rotation of the goals.
    /// </summary>
    public int Number => this.Date.DayNumber / DaysPerWeek;

    /// <summary>
    /// Gets the week of the specified time.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The week.</returns>
    public static WeeklyGoalWeek Of(DateTime utc) => new(DateOnly.FromDateTime(WeeklySnapshotPlugIn.GetWeekStart(utc)));

    /// <summary>
    /// Gets the week which is the specified number of weeks before this one.
    /// </summary>
    /// <param name="weeks">The number of weeks.</param>
    /// <returns>The earlier week.</returns>
    public WeeklyGoalWeek Before(int weeks) => new(this.Date.AddDays(-weeks * DaysPerWeek));
}
