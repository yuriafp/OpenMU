// <copyright file="RewardItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

/// <summary>
/// An item of a reward.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number.</param>
/// <param name="Level">The item level.</param>
/// <param name="Count">The number of items; a stackable item comes as full stacks.</param>
internal sealed record RewardItem(byte Group, short Number, byte Level = 0, int Count = 1);
