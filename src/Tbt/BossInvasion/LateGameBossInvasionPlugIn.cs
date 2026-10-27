// <copyright file="LateGameBossInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The boss invasion of the late game: the bosses of the last maps of Season 6 and the elementals of
/// Season 8, for the group after the first resets, on the "Noite do MU" (PLANO-SERVIDOR.md, item 13
/// of 9.5).
/// </summary>
[PlugIn]
[Display(Name = "TBT: Invasão de chefes (fim do jogo)", Description = "Once a week, the bosses of a theme appear with their escort and more health, for a group of the late game; the party which kills a boss gets Boxes of Kundun.")]
[Guid("9A3F6D12-E847-4C5B-B2E9-1F68D7A0C453")]
public class LateGameBossInvasionPlugIn : BossInvasionPlugInBase
{
    /// <inheritdoc />
    protected override BossInvasionConfiguration CreateDefaultConfiguration() => BossInvasionConfiguration.CreateLateGameDefault();
}
