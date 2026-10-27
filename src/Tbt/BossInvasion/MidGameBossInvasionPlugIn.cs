// <copyright file="MidGameBossInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The boss invasion of the mid game: the bosses of the maps of the second wings, and Lord Silvester
/// and the Reaper of Season 8, for a group which has them (PLANO-SERVIDOR.md, item 13 of 9.5).
/// </summary>
[PlugIn]
[Display(Name = "TBT: Invasão de chefes (meio do jogo)", Description = "Once a week, the bosses of a theme appear with their escort and more health, for a group of the mid game; the party which kills a boss gets Boxes of Kundun.")]
[Guid("5C1E8A47-3B9D-4E26-A8F1-7D04C2B96E31")]
public class MidGameBossInvasionPlugIn : BossInvasionPlugInBase
{
    /// <inheritdoc />
    protected override BossInvasionConfiguration CreateDefaultConfiguration() => BossInvasionConfiguration.CreateMidGameDefault();
}
