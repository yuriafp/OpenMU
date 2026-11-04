// <copyright file="JewelBankAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// The action of a <see cref="JewelBankRequest"/>.
/// </summary>
internal enum JewelBankAction : byte
{
    /// <summary>Keeps all jewels and packs of one jewel from the inventory.</summary>
    Deposit = 1,

    /// <summary>Keeps all jewels and packs which the Banqueira keeps from the inventory.</summary>
    DepositAll = 2,

    /// <summary>Takes out an amount of one jewel: a jewel, or a pack of 10 or 30.</summary>
    Withdraw = 3,
}
