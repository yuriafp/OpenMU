// <copyright file="StarterKitTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// Tests the starter kits per class, the time left shown to the players and the single removal of an expired kit.
/// </summary>
[TestFixture]
public class StarterKitTest
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Every class of a line gets the kit of its line: the weapon, the set (the Magic Gladiator's
    /// without the helm), the shield (none for the Fairy Elf), the mini wings and the Pet Panda.
    /// </summary>
    /// <param name="classNumber">The number of the class.</param>
    /// <param name="weapon">The expected weapon, as group/number.</param>
    /// <param name="set">The expected number of the set.</param>
    /// <param name="withHelm">If set to <c>true</c>, the set has a helm.</param>
    /// <param name="shield">The expected shield, as group/number, or <c>null</c>.</param>
    /// <param name="wings">The expected mini wings, as group/number.</param>
    [TestCase(0, "5/0", 2, true, "6/4", "12/133")]
    [TestCase(1, "5/0", 2, true, "6/4", "12/133")]
    [TestCase(4, "0/3", 5, true, "6/6", "12/134")]
    [TestCase(7, "0/3", 5, true, "6/6", "12/134")]
    [TestCase(8, "4/2", 10, true, null, "12/132")]
    [TestCase(12, "0/4", 4, false, "6/2", "12/134")]
    [TestCase(16, "2/1", 0, true, "6/10", "12/130")]
    public void KitOfClass(int classNumber, string weapon, int set, bool withHelm, string? shield, string wings)
    {
        var kit = StarterKitCatalog.Get(new CharacterClass { Number = (byte)classNumber });
        var firstGroup = withHelm ? 7 : 8;
        var expected = new[] { weapon }
            .Concat(Enumerable.Range(firstGroup, 12 - firstGroup).Select(group => $"{group}/{set}"))
            .Concat(shield is null ? [] : [shield])
            .Append(wings)
            .Append("13/80");

        Assert.That(kit?.Items.Select(i => $"{i.Group}/{i.Number}"), Is.EqualTo(expected));
    }

    /// <summary>
    /// The weapon, the set and the shield are +9 with three different excellent options and the
    /// Luck; the mini wings are +9 without options, and the Panda +0 without options.
    /// </summary>
    /// <param name="classNumber">The number of the class.</param>
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    public void KitItemsArePlusNineWithLuck(int classNumber)
    {
        var items = StarterKitCatalog.Get(new CharacterClass { Number = (byte)classNumber })!.Items;
        var panda = items.Single(i => (i.Group, i.Number) == (13, 80));
        var wings = items.Single(i => i.Group == 12);

        Assert.That(items.Where(i => i != panda && i != wings).Select(i => (i.Level, i.ExcellentOptions.Distinct().Count(), i.Luck)), Is.All.EqualTo(((byte)9, 3, true)));
        Assert.That((wings.Level, wings.ExcellentOptions.Count, wings.Luck), Is.EqualTo(((byte)9, 0, false)));
        Assert.That((panda.Level, panda.ExcellentOptions.Count, panda.Luck), Is.EqualTo(((byte)0, 0, false)));
    }

    /// <summary>
    /// The Summoner and the Rage Fighter, which this server doesn't have, and a missing class get no kit.
    /// </summary>
    /// <param name="classNumber">The number of the class, or -1 for none.</param>
    [TestCase(20)]
    [TestCase(24)]
    [TestCase(-1)]
    public void NoKit(int classNumber)
    {
        var characterClass = classNumber < 0 ? null : new CharacterClass { Number = (byte)classNumber };

        Assert.That(StarterKitCatalog.Get(characterClass), Is.Null);
    }

    /// <summary>
    /// The time left is shown in days and hours, then hours and minutes, with the day and time of
    /// the expiry in Brasília.
    /// </summary>
    /// <param name="minutesLeft">The minutes left.</param>
    /// <param name="expected">The expected message.</param>
    [TestCase(13290, "Kit iniciante: expira em 9 dias e 5 h, em 31/10 às 14:30 (horário de Brasília).")]
    [TestCase(1560, "Kit iniciante: expira em 1 dia e 2 h, em 23/10 às 11:00 (horário de Brasília).")]
    [TestCase(195, "Kit iniciante: expira em 3 h e 15 min, em 22/10 às 12:15 (horário de Brasília).")]
    [TestCase(20, "Kit iniciante: expira em 20 min, em 22/10 às 09:20 (horário de Brasília).")]
    [TestCase(0.5, "Kit iniciante: expira em 1 min, em 22/10 às 09:00 (horário de Brasília).")]
    [TestCase(0, "Seu kit iniciante expirou e sai do inventário em instantes.")]
    public void RemainingTime(double minutesLeft, string expected)
    {
        var kit = new ActiveStarterKit(Guid.NewGuid(), Now.AddMinutes(minutesLeft), []);

        Assert.That(StarterKitPlugIn.FormatRemaining(kit, Now), Is.EqualTo(expected));
    }

    /// <summary>
    /// The periodic check and the entry may both find an expired kit, but only one removes it; a
    /// failed removal allows the next one.
    /// </summary>
    [Test]
    public void ExpiryStartsOnce()
    {
        var kit = new ActiveStarterKit(Guid.NewGuid(), Now, []);

        Assert.That(kit.TryStartExpiry(), Is.True);
        Assert.That(kit.TryStartExpiry(), Is.False);
        kit.CancelExpiry();
        Assert.That(kit.TryStartExpiry(), Is.True);
    }

    /// <summary>
    /// A failed check at the entry is tried again after 5 seconds, then the wait doubles, up to 5 minutes.
    /// </summary>
    [Test]
    public void RetryWaitDoublesUpToFiveMinutes()
    {
        var firstDelay = TimeSpan.FromSeconds(5);
        var entry = new StarterKitPendingEntry(Guid.NewGuid(), 1, Now + firstDelay);
        var waits = new List<TimeSpan>();
        for (var i = 0; i < 8; i++)
        {
            entry = entry.Failed(Now, firstDelay);
            waits.Add(entry.NextAttemptUtc - Now);
        }

        Assert.That(waits.Select(w => w.TotalSeconds), Is.EqualTo(new double[] { 10, 20, 40, 80, 160, 300, 300, 300 }));
        Assert.That(entry.Attempts, Is.EqualTo(9));
    }

    /// <summary>
    /// The periodic check of a game server runs once per interval and never twice at the same time.
    /// </summary>
    [Test]
    public void CheckRunsOncePerInterval()
    {
        var interval = TimeSpan.FromSeconds(5);
        var check = new PeriodicCheck();
        var now = DateTime.UtcNow;

        Assert.That(check.TryStart(now, interval), Is.True);
        Assert.That(check.TryStart(now + interval, interval), Is.False, "still running");
        check.End();
        Assert.That(check.TryStart(now + TimeSpan.FromSeconds(1), interval), Is.False, "not due yet");
        Assert.That(check.TryStart(now + interval, interval), Is.True);
        check.End();
        check.ForceStart();
        Assert.That(check.TryStart(DateTime.UtcNow, interval), Is.True, "forced");
    }
}
