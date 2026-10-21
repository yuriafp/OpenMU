// <copyright file="Milestone.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

/// <summary>
/// A milestone of an account, with its reward.
/// </summary>
/// <param name="Key">The key in <c>tbt.milestone</c>.</param>
/// <param name="Level">The level which reaches it, or 0 for a reset milestone.</param>
/// <param name="Resets">The resets which reach it, or 0 for a level milestone.</param>
/// <param name="Rewards">The reward items.</param>
/// <param name="RewardText">The reward, as shown to the player.</param>
/// <param name="Hint">The hint which leads to the next activity, or <c>null</c>.</param>
internal sealed record Milestone(string Key, int Level, int Resets, IReadOnlyList<RewardItem> Rewards, string RewardText, string? Hint);
