// <copyright file="JewelBankOperation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// An operation of the Banqueira, in <c>tbt.jewel_bank_operation</c> while its items aren't settled.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="AccountId">The account identifier.</param>
/// <param name="Session">The stay in the world in which it was made (<see cref="JewelBanker"/>).</param>
/// <param name="Kind"><see cref="JewelBankStore.Deposit"/> or <see cref="JewelBankStore.Withdrawal"/>.</param>
/// <param name="Jewel">The name of the jewel (<see cref="BankJewel.Name"/>).</param>
/// <param name="Amount">The jewels.</param>
/// <param name="Fee">The fee, in zen.</param>
/// <param name="ItemIds">The items: those which left the inventory, or the one which entered it.</param>
internal sealed record JewelBankOperation(Guid Id, Guid AccountId, Guid Session, string Kind, string Jewel, int Amount, long Fee, Guid[] ItemIds);
