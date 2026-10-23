// <copyright file="StarterKitDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The starter kit of a class.
/// </summary>
/// <param name="Description">The weapon and the set, as shown to the player.</param>
/// <param name="Items">The items.</param>
internal sealed record StarterKitDefinition(string Description, IReadOnlyList<StarterKitItem> Items);
