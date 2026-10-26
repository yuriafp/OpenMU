// <copyright file="CatchUpBonusTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.Tbt.CatchUp;

/// <summary>
/// Tests the catch-up bonus and its message.
/// </summary>
[TestFixture]
public class CatchUpBonusTest
{
    /// <summary>
    /// The bonus is 10% for each reset behind the most advanced human, up to 50%, and nothing for
    /// the most advanced human and who passed it.
    /// </summary>
    /// <param name="resets">The resets of the character.</param>
    /// <param name="leaderResets">The resets of the most advanced human.</param>
    /// <param name="expected">The expected bonus, in percent.</param>
    [TestCase(0, 0, 0)]
    [TestCase(5, 5, 0)]
    [TestCase(6, 5, 0)]
    [TestCase(4, 5, 10)]
    [TestCase(2, 5, 30)]
    [TestCase(0, 5, 50)]
    [TestCase(0, 30, 50)]
    public void BonusGrowsWithTheDistance(int resets, int leaderResets, int expected)
    {
        Assert.That(CatchUpBonusPlugIn.GetPercent(resets, leaderResets, new CatchUpBonusConfiguration()), Is.EqualTo(expected));
    }

    /// <summary>
    /// The percent per reset and the maximum come from the configuration.
    /// </summary>
    [Test]
    public void BonusFollowsTheConfiguration()
    {
        var configuration = new CatchUpBonusConfiguration { PercentPerReset = 15, MaximumPercent = 40 };

        Assert.That(CatchUpBonusPlugIn.GetPercent(9, 10, configuration), Is.EqualTo(15));
        Assert.That(CatchUpBonusPlugIn.GetPercent(8, 10, configuration), Is.EqualTo(30));
        Assert.That(CatchUpBonusPlugIn.GetPercent(7, 10, configuration), Is.EqualTo(40));
    }

    /// <summary>
    /// The message has the bonus and the resets of the most advanced human; a broken one falls back
    /// to the default, and an empty one shows nothing.
    /// </summary>
    [Test]
    public void MessageHasTheBonusAndFallsBack()
    {
        Assert.That(CatchUpBonusPlugIn.FormatMessage(new CatchUpBonusConfiguration(), 30, 12), Is.EqualTo("Bônus de alcance: +30% de exp. O mais avançado está no reset 12."));
        Assert.That(CatchUpBonusPlugIn.FormatMessage(new CatchUpBonusConfiguration { Message = "Bônus {0" }, 30, 12), Is.EqualTo("Bônus de alcance: +30% de exp. O mais avançado está no reset 12."));
        Assert.That(CatchUpBonusPlugIn.FormatMessage(new CatchUpBonusConfiguration { Message = " " }, 30, 12), Is.Null);
    }
}
