// <copyright file="EventSchedule.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Welcome;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The agenda of the events, read from the timetables of the active event plugins, so it never
/// differs from what really happens. The timetables are in the time zone of the server, which is
/// UTC on our server (PLANO-SERVIDOR.md, 9.3).
/// </summary>
internal static class EventSchedule
{
    /// <summary>
    /// The name of the Blood Castle in the agenda.
    /// </summary>
    public const string BloodCastle = "Blood Castle";

    /// <summary>
    /// Gets the next events of the coming 24 hours, starting now, on the days on which they run.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="count">The maximum number of events.</param>
    /// <returns>The next events, with their start time in UTC.</returns>
    public static IEnumerable<(TimeOnly Start, string Name)> GetNextEvents(IGameContext gameContext, int count)
        => GetNextEvents(gameContext, count, DateTime.UtcNow);

    /// <summary>
    /// Gets the times of an event in the day, in UTC.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="name">The name of the event, e.g. <see cref="BloodCastle"/>.</param>
    /// <returns>The times of the event, ordered as shown to the players.</returns>
    public static IReadOnlyList<TimeOnly> GetTimes(IGameContext gameContext, string name)
        => GetTimetables(gameContext)
            .Where(e => e.Name == name)
            .SelectMany(e => e.Configuration.Timetable)
            .Distinct()
            .OrderBy(time => time.Add(TbtTime.ShownOffset))
            .ToList();

    /// <summary>
    /// Gets the next events of the 24 hours after the specified time, on the days on which they run.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="count">The maximum number of events.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The next events, with their start time in UTC.</returns>
    internal static IEnumerable<(TimeOnly Start, string Name)> GetNextEvents(IGameContext gameContext, int count, DateTime utcNow)
    {
        var timeZone = gameContext.ServerTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));
        return GetTimetables(gameContext)
            .SelectMany(e => new[] { today, today.AddDays(1) }
                .SelectMany(date => e.Configuration.GetStartsUtc(date, timeZone))
                .Select(startsAt => (StartsAt: startsAt, e.Name)))
            .Where(e => e.StartsAt >= utcNow && e.StartsAt < utcNow.AddDays(1))
            .OrderBy(e => e.StartsAt)
            .Take(count)
            .Select(e => (TimeOnly.FromDateTime(e.StartsAt), e.Name));
    }

    private static IEnumerable<(string Name, PeriodicTaskConfiguration Configuration)> GetTimetables(IGameContext gameContext)
    {
        foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>())
        {
            (string Name, PeriodicTaskConfiguration? Configuration) entry = plugIn switch
            {
                BloodCastleStartPlugIn p => (BloodCastle, p.Configuration),
                DevilSquareStartPlugIn p => ("Devil Square", p.Configuration),
                ChaosCastleStartPlugIn p => ("Chaos Castle", p.Configuration),
                KanturuStartPlugIn p => ("Kanturu", p.Configuration),
                GoldenInvasionPlugIn p => ("Golden Invasion", p.Configuration),
                RedDragonInvasionPlugIn p => ("Red Dragon", p.Configuration),
                WhiteWizardInvasionPlugIn p => ("White Wizard", p.Configuration),
                HappyHourPlugIn p => ("Happy Hour", p.Configuration),
                _ => (string.Empty, null),
            };

            if (entry.Configuration is { Timetable.Count: > 0 } configuration)
            {
                yield return (entry.Name, configuration);
            }
        }
    }
}
