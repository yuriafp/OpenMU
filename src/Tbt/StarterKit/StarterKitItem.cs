// <copyright file="StarterKitItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// An item of a starter kit. An item with a skill, like a weapon or a shield, gets it.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number.</param>
/// <param name="Level">The item level.</param>
/// <param name="ExcellentOptions">The numbers of the excellent options.</param>
/// <param name="Luck">If set to <c>true</c>, the item gets the Luck, which its definition must allow.</param>
internal sealed record StarterKitItem(byte Group, short Number, byte Level, IReadOnlyList<int> ExcellentOptions, bool Luck);
