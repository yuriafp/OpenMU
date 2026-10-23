// <copyright file="StarterKitRecord.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The recorded starter kit of a character.
/// </summary>
/// <param name="ExpiresAt">The time of expiry, in UTC.</param>
/// <param name="Removed">If set to <c>true</c>, the kit expired and left the inventory.</param>
/// <param name="ItemIds">The identifiers of the items.</param>
internal sealed record StarterKitRecord(DateTime ExpiresAt, bool Removed, IReadOnlyList<Guid> ItemIds);
