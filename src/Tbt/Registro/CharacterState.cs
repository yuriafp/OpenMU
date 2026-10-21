// <copyright file="CharacterState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The known state of a character.
/// </summary>
/// <param name="CharacterId">The character identifier.</param>
/// <param name="AccountId">The account identifier.</param>
/// <param name="Name">The character name.</param>
/// <param name="IsBot">If set to <c>true</c>, the account is a bot account.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The resets.</param>
public sealed record CharacterState(Guid CharacterId, Guid AccountId, string Name, bool IsBot, int Level, int Resets);
