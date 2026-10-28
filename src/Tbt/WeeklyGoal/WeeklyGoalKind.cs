// <copyright file="WeeklyGoalKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

/// <summary>
/// What counts for a weekly goal, from the registro (<c>tbt.event</c>), only for humans.
/// </summary>
/// <remarks>
/// The window of our client knows the kinds by these numbers (<see cref="WeeklyGoalRef.Kind"/>),
/// and names them in its language. The database keeps their names.
/// </remarks>
public enum WeeklyGoalKind
{
    /// <summary>
    /// Every human who is in a Blood Castle at its end counts one. The end doesn't tell a failed
    /// run, which counts, too.
    /// </summary>
    BloodCastle = 0,

    /// <summary>
    /// Every human who is in a Devil Square at its end counts one.
    /// </summary>
    DevilSquare = 1,

    /// <summary>
    /// Every human who enters a Chaos Castle counts one: only its survivor is in it at the end.
    /// </summary>
    ChaosCastle = 2,

    /// <summary>
    /// Every reset of a human counts one.
    /// </summary>
    Resets = 3,

    /// <summary>
    /// Every opened treasure chest counts one.
    /// </summary>
    TreasureChests = 4,
}
