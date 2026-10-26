// <copyright file="EventScheduleTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Tbt.Welcome;

/// <summary>
/// Tests the agenda of the welcome message.
/// </summary>
[TestFixture]
public class EventScheduleTest
{
    /// <summary>
    /// The agenda shows the events of the next 24 hours only on the days on which they run. On Thursday
    /// at 12:30, the Golden Invasion of Fridays at 08:00 and 18:00 shows only at 08:00 tomorrow, since its
    /// time at 18:00 today is on a Thursday and the one tomorrow is more than 24 hours away.
    /// </summary>
    [Test]
    public void NextEventsSkipTheDaysWithoutTheEvent()
    {
        var gameContext = TestGameContext.Create();
        var golden = new PeriodicInvasionConfiguration { Timetable = [new TimeOnly(8, 0), new TimeOnly(18, 0)], Days = WeekDays.Friday };
        var redDragon = new PeriodicInvasionConfiguration { Timetable = [new TimeOnly(13, 0)] };
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPeriodicTaskPlugIn>(new GoldenInvasionPlugIn { Configuration = golden });
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPeriodicTaskPlugIn>(new RedDragonInvasionPlugIn { Configuration = redDragon });

        var events = EventSchedule.GetNextEvents(gameContext, 10, new DateTime(2026, 10, 8, 12, 30, 0, DateTimeKind.Utc)).ToList();

        Assert.That(events, Is.EqualTo(new[] { (new TimeOnly(13, 0), "Red Dragon"), (new TimeOnly(8, 0), "Golden Invasion") }));
    }
}
