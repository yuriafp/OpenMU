// <copyright file="ScheduleTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// Tests the reset milestones and the turn of the week of the weekly snapshot.
/// </summary>
[TestFixture]
public class ScheduleTest
{
    /// <summary>
    /// The reset milestones are the 1st, the 5th, the 10th and every 10th reset.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <param name="expected">The expected milestones, comma separated.</param>
    [TestCase(0, "")]
    [TestCase(1, "1")]
    [TestCase(4, "1")]
    [TestCase(5, "1,5")]
    [TestCase(10, "1,5,10")]
    [TestCase(29, "1,5,10,20")]
    [TestCase(30, "1,5,10,20,30")]
    public void ResetMilestones(int resets, string expected)
    {
        var milestones = MilestoneCatalog.GetResetMilestones(resets).Select(m => m.Resets);

        Assert.That(string.Join(",", milestones), Is.EqualTo(expected));
    }

    /// <summary>
    /// The next reset milestone after the resets.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <param name="expected">The expected next milestone.</param>
    [TestCase(0, 1)]
    [TestCase(1, 5)]
    [TestCase(4, 5)]
    [TestCase(5, 10)]
    [TestCase(9, 10)]
    [TestCase(10, 20)]
    [TestCase(19, 20)]
    [TestCase(20, 30)]
    public void NextResetMilestone(int resets, int expected)
    {
        Assert.That(MilestoneCatalog.GetNextResetMilestone(resets).Resets, Is.EqualTo(expected));
    }

    /// <summary>
    /// The reset milestones give a Box of Kundun +1 at the 1st reset, +2 at the 5th and +3 from
    /// the 10th; the box +N is the item 14/11 at level 7 + N.
    /// </summary>
    /// <param name="reset">The reset.</param>
    /// <param name="expectedLevel">The expected level of the box.</param>
    [TestCase(1, 8)]
    [TestCase(5, 9)]
    [TestCase(10, 10)]
    [TestCase(40, 10)]
    public void ResetMilestoneRewards(int reset, int expectedLevel)
    {
        var reward = MilestoneCatalog.GetResetMilestones(reset).Last().Rewards.Single();

        Assert.That((reward.Group, reward.Number, (int)reward.Level), Is.EqualTo(((byte)14, (short)11, expectedLevel)));
    }

    /// <summary>
    /// The week turns on Monday at 02:00 UTC (Sunday at 23:00 in Brasília).
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="expected">The expected Monday which starts the week.</param>
    [TestCase("2026-10-19T01:59:59", "2026-10-12")]
    [TestCase("2026-10-19T02:00:00", "2026-10-19")]
    [TestCase("2026-10-21T12:00:00", "2026-10-19")]
    [TestCase("2026-10-25T23:59:59", "2026-10-19")]
    [TestCase("2026-10-26T01:00:00", "2026-10-19")]
    public void WeekStart(string utc, string expected)
    {
        var start = WeeklySnapshotPlugIn.GetWeekStart(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));

        Assert.That(start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }
}
