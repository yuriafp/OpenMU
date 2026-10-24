// <copyright file="StarterKitDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The starter kit of a class.
/// </summary>
/// <param name="Description">The weapon, the set and the shield, as shown to the player.</param>
/// <param name="Wings">The name of the wings, as shown to the player.</param>
/// <param name="Items">The items.</param>
internal sealed record StarterKitDefinition(string Description, string Wings, IReadOnlyList<StarterKitItem> Items);
