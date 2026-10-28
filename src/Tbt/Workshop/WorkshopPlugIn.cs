// <copyright file="WorkshopPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Workshop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Oficina (PLANO-SERVIDOR.md, 5.1, items 2 and 15 of 9.5): puts the Luck or one excellent or
/// wing option of the player's choice into an item or a wing, for jewels and zen. It holds the configuration of the
/// <see cref="WorkshopCraftingHandler"/>; while it's inactive, the Oficina is closed.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Oficina", Description = "Puts the Luck or one excellent or wing option of the player's choice into an item or a wing, for jewels and zen. While it's inactive, the Oficina is closed.")]
[Guid("C61E8F3A-2D47-4B95-8E1C-7A3F5B9D0E24")]
public class WorkshopPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<WorkshopConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public WorkshopConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new WorkshopConfiguration();
}
