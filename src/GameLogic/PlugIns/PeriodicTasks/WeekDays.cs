// <copyright file="WeekDays.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The days of the week on which a periodic task runs. Each flag is one shifted by the
/// <see cref="DayOfWeek"/> value of its day.
/// </summary>
[Flags]
public enum WeekDays
{
    /// <summary>
    /// No day is selected, which means every day.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sunday.
    /// </summary>
    Sunday = 1 << (int)DayOfWeek.Sunday,

    /// <summary>
    /// Monday.
    /// </summary>
    Monday = 1 << (int)DayOfWeek.Monday,

    /// <summary>
    /// Tuesday.
    /// </summary>
    Tuesday = 1 << (int)DayOfWeek.Tuesday,

    /// <summary>
    /// Wednesday.
    /// </summary>
    Wednesday = 1 << (int)DayOfWeek.Wednesday,

    /// <summary>
    /// Thursday.
    /// </summary>
    Thursday = 1 << (int)DayOfWeek.Thursday,

    /// <summary>
    /// Friday.
    /// </summary>
    Friday = 1 << (int)DayOfWeek.Friday,

    /// <summary>
    /// Saturday.
    /// </summary>
    Saturday = 1 << (int)DayOfWeek.Saturday,
}
