// <copyright file="WeeklyGoalProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

/// <summary>
/// The progress of the goal of a week.
/// </summary>
/// <param name="Week">The week.</param>
/// <param name="Goal">The goal.</param>
/// <param name="Contributions">What each account counted, by its identifier.</param>
/// <param name="ReachedAt">When the goal was reached, or <c>null</c> while it isn't.</param>
internal sealed record WeeklyGoalProgress(WeeklyGoalWeek Week, WeeklyGoalDefinition Goal, IReadOnlyDictionary<Guid, int> Contributions, DateTime? ReachedAt)
{
    /// <summary>
    /// Gets the count of the week.
    /// </summary>
    public int Count { get; } = (int)Math.Min(Contributions.Values.Sum(value => (long)value), int.MaxValue);

    /// <summary>
    /// Gets a value indicating whether the goal is reached.
    /// </summary>
    public bool IsReached => this.ReachedAt is not null;

    /// <summary>
    /// Gets what the account counted this week.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The count of the account.</returns>
    public int Of(Guid accountId) => this.Contributions.TryGetValue(accountId, out var count) ? count : 0;

    /// <summary>
    /// Gets the step of the progress which the count reached, e.g. 50 for 12 of 20 with steps of 25.
    /// It's 100 only once the goal is reached, which is recorded, so the steps below stay below 100.
    /// </summary>
    /// <param name="step">The step, in percent; 0 for none.</param>
    /// <returns>The reached step, in percent.</returns>
    public int GetStep(int step)
    {
        if (this.IsReached)
        {
            return WeeklyGoalPlugIn.FullPercent;
        }

        if (step <= 0 || step >= WeeklyGoalPlugIn.FullPercent || this.Goal.Target <= 0)
        {
            return 0;
        }

        var percent = (int)Math.Min((long)this.Count * WeeklyGoalPlugIn.FullPercent / this.Goal.Target, WeeklyGoalPlugIn.FullPercent - 1);
        return percent / step * step;
    }
}
