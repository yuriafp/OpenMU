// <copyright file="JewelBankConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// The configuration of the Banqueira (<see cref="JewelBankPlugIn"/>). The fees are values for the
/// calibration (PLANO-SERVIDOR.md, 5.2).
/// </summary>
public class JewelBankConfiguration
{
    /// <summary>
    /// Gets or sets the number of the NPC of the Banqueira.
    /// </summary>
    /// <remarks>
    /// Our client shows the NPC 669, Ellin of the later seasons, with her model (<c>Data/NPC/NpcElin.bmd</c>); another
    /// number needs the same change in its <c>Monsters.json</c>.
    /// </remarks>
    [Display(Name = "NPC Number", Description = "The number of the NPC of the Banqueira. Our client shows the NPC 669 as the Banqueira.")]
    public short NpcNumber { get; set; } = 669;

    /// <summary>
    /// Gets or sets the fee to keep one jewel, in zen. A pack counts its jewels.
    /// </summary>
    [Display(Name = "Deposit Fee per Jewel", Description = "The fee to keep one jewel, in zen. A pack counts its jewels: a pack of 10 costs ten times as much.")]
    [Range(0, 10_000_000)]
    public int DepositFeePerJewel { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the fee to take out one jewel, in zen. A pack counts its jewels.
    /// </summary>
    [Display(Name = "Withdrawal Fee per Jewel", Description = "The fee to take out one jewel, in zen. A pack counts its jewels: a pack of 10 costs ten times as much.")]
    [Range(0, 10_000_000)]
    public int WithdrawalFeePerJewel { get; set; } = 100_000;
}
