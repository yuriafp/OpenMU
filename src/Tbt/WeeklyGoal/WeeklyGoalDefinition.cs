// <copyright file="WeeklyGoalDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

/// <summary>
/// A weekly goal of the rotation, e.g. 20 Blood Castles.
/// </summary>
public class WeeklyGoalDefinition
{
    /// <summary>
    /// Gets or sets what counts.
    /// </summary>
    [Display(Name = "Kind", Description = "What counts.", Order = 1)]
    public WeeklyGoalKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the count which reaches the goal.
    /// </summary>
    [Display(Name = "Target", Description = "The count which reaches the goal.", Order = 2)]
    [Range(1, 1_000_000)]
    public int Target { get; set; } = 1;

    /// <summary>
    /// Gets or sets the name of what counts in the messages, after the target, e.g. "Blood Castles completos".
    /// </summary>
    [Display(Name = "Name", Description = "The name of what counts in the messages, after the target, e.g. \"Blood Castles completos\".", Order = 3)]
    public string Name { get; set; } = string.Empty;
}
