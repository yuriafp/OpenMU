// <copyright file="DropAnnouncementPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Drops;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The announced drop (PLANO-SERVIDOR.md, 5.1, item 3 of 9.5): "Fulano dropou Excellent Dragon Armor
/// em Lost Tower!" in a golden message to everyone, for excellent and ancient items and the Box of
/// Kundun. Drops of bots don't count. Every announced drop is also written to the registro.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Drop anunciado", Description = "Announces the excellent and ancient items and the Box of Kundun dropped for humans to everyone.")]
[Guid("B27F4C83-5D1E-4A6B-8C39-2E7D5A1F9C64")]
public class DropAnnouncementPlugIn : IMonsterItemDroppedPlugIn, ISupportCustomConfiguration<DropAnnouncementConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public DropAnnouncementConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new DropAnnouncementConfiguration();

    /// <inheritdoc />
    public async ValueTask MonsterItemDroppedAsync(AttackableNpcBase monster, Player killer, DroppedItem droppedItem)
    {
        if (!killer.IsHuman())
        {
            return;
        }

        try
        {
            var configuration = this.Configuration ??= new DropAnnouncementConfiguration();
            if (GetAnnouncedName(droppedItem.Item, configuration) is not { } itemName)
            {
                return;
            }

            var mapName = droppedItem.CurrentMap.Definition.Name.ValueInNeutralLanguage;
            ActivityLog.Write("drop", killer, new { item = itemName, map = mapName });
            var message = string.Format(configuration.Message, killer.Name, itemName, mapName);
            await killer.GameContext.SendGlobalMessageAsync(message, MessageType.GoldenCenter).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the drop.
            killer.Logger.LogWarning(ex, "Couldn't announce the drop {Item}.", droppedItem.Item);
        }
    }

    private static string? GetAnnouncedName(Item item, DropAnnouncementConfiguration configuration)
    {
        if (item.Definition is not { } definition)
        {
            return null;
        }

        if (definition.Group == BoxOfKundun.Group && definition.Number == BoxOfKundun.Number)
        {
            var plus = BoxOfKundun.PlusOf(item.Level);
            return configuration.MinimumBoxOfKundun > 0 && plus >= configuration.MinimumBoxOfKundun ? BoxOfKundun.NameOf(plus) : null;
        }

        var name = definition.Name.ValueInNeutralLanguage;

        var level = item.Level > 0 ? $" +{item.Level}" : string.Empty;
        if (configuration.AnnounceExcellentItems && item.IsExcellent())
        {
            return $"Excellent {name}{level}";
        }

        if (configuration.AnnounceAncientItems && item.IsAncient())
        {
            return $"Ancient {name}{level}";
        }

        return null;
    }
}
