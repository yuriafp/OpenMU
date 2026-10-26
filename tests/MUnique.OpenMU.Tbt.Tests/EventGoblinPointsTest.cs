// <copyright file="EventGoblinPointsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.Tbt.GoblinPoints;

/// <summary>
/// Tests who gets the Goblin Points of a completed event, and how many.
/// </summary>
[TestFixture]
public class EventGoblinPointsTest
{
    private readonly Player _alice = new BotPlayer(TestGameContext.Create());

    private readonly Player _bob = new BotPlayer(TestGameContext.Create());

    private readonly Player _bot = new BotPlayer(TestGameContext.Create());

    /// <summary>
    /// The humans still in the event at its end get the points, its winner the winner points on
    /// top, and a bot nothing.
    /// </summary>
    [Test]
    public void FinishersGetThePointsAndTheWinnerMore()
    {
        var grants = this.GetGrants([this._alice, this._bob, this._bot], this._bob, new EventGoblinPointsConfiguration());

        Assert.That(grants, Is.EqualTo(new[] { (this._alice, 10), (this._bob, 20) }));
    }

    /// <summary>
    /// The winner gets its points also when it isn't in the event anymore at its end, while it's
    /// still a human in the world.
    /// </summary>
    [Test]
    public void TheWinnerOutsideTheEventGetsThePoints()
    {
        var grants = this.GetGrants([this._alice], this._bob, new EventGoblinPointsConfiguration());

        Assert.That(grants, Is.EqualTo(new[] { (this._alice, 10), (this._bob, 20) }));
    }

    /// <summary>
    /// Without points for finishing, only the winner gets something, and a bot which wins nothing.
    /// </summary>
    [Test]
    public void NoPointsNoGrant()
    {
        var configuration = new EventGoblinPointsConfiguration { Points = 0, WinnerPoints = 5 };

        Assert.That(this.GetGrants([this._alice, this._bob], this._bob, configuration), Is.EqualTo(new[] { (this._bob, 5) }));
        Assert.That(this.GetGrants([this._alice, this._bot], this._bot, configuration), Is.Empty);
    }

    /// <summary>
    /// The message has the points and the event; a broken one falls back to the default, and an
    /// empty one means no message.
    /// </summary>
    [Test]
    public void TheMessageFallsBackToTheDefault()
    {
        Assert.That(EventGoblinPointsPlugIn.FormatMessage(new EventGoblinPointsConfiguration(), 20, "Blood Castle 3"), Is.EqualTo("Você ganhou 20 Goblin Points no Blood Castle 3."));
        Assert.That(EventGoblinPointsPlugIn.FormatMessage(new EventGoblinPointsConfiguration { Message = "+{0} GP ({2})" }, 10, "Devil Square 1"), Is.EqualTo("Você ganhou 10 Goblin Points no Devil Square 1."));
        Assert.That(EventGoblinPointsPlugIn.FormatMessage(new EventGoblinPointsConfiguration { Message = " " }, 10, "Devil Square 1"), Is.Null);
    }

    private List<(Player Player, int Points)> GetGrants(IReadOnlyCollection<Player> finishers, Player? winner, EventGoblinPointsConfiguration configuration)
        => EventGoblinPointsPlugIn.GetGrants(finishers, winner, configuration, player => player != this._bot).ToList();
}
