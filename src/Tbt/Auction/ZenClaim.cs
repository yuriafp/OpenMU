// <copyright file="ZenClaim.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// A taking of the zen of the sales.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="AccountId">The account.</param>
/// <param name="Session">The stay in the world which wrote it.</param>
/// <param name="Zen">The zen.</param>
/// <param name="InventoryId">The item storage of the inventory of the character which takes it.</param>
/// <param name="MoneyAfter">The zen of that inventory with the taking.</param>
internal sealed record ZenClaim(Guid Id, Guid AccountId, Guid Session, long Zen, Guid InventoryId, long MoneyAfter);
