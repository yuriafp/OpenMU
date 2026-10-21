// <copyright file="RewardDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Puts reward items into the inventory of a player, all or none.
/// </summary>
internal static class RewardDelivery
{
    /// <summary>
    /// Puts the reward items into the inventory of the player. When one doesn't fit, or adding one
    /// fails, the ones added before are taken back, so the reward can be given again later as a whole.
    /// Runs under the persistence lock of the player, like the packet handlers.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="rewards">The rewards.</param>
    /// <returns><c>true</c>, if all items were added.</returns>
    public static async ValueTask<bool> TryGiveAsync(Player player, IReadOnlyList<RewardItem> rewards)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        Item? pending = null;
        try
        {
            foreach (var reward in rewards)
            {
                if (player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == reward.Group && d.Number == reward.Number) is not { } definition)
                {
                    player.Logger.LogWarning("The reward item {Group}/{Number} doesn't exist.", reward.Group, reward.Number);
                    continue;
                }

                for (var i = 0; i < reward.Count; i++)
                {
                    pending = CreateItem(player, definition, reward.Level);
                    if (!await inventory.AddItemAsync(pending).ConfigureAwait(false))
                    {
                        await player.PersistenceContext.DeleteAsync(pending).ConfigureAwait(false);
                        pending = null;
                        await TakeBackAsync(player, inventory, added).ConfigureAwait(false);
                        return false;
                    }

                    var item = pending;
                    added.Add(item);
                    pending = null;
                    await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            try
            {
                if (pending is not null)
                {
                    await player.PersistenceContext.DeleteAsync(pending).ConfigureAwait(false);
                }

                await TakeBackAsync(player, inventory, added).ConfigureAwait(false);
            }
            catch (Exception takeBackException)
            {
                player.Logger.LogWarning(takeBackException, "Couldn't take back a half given reward of {Character}.", player.Name);
            }

            throw;
        }

        try
        {
            // Persist right away: the milestone is already recorded as given.
            await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't save {Character} right after a reward; the periodic save will retry.", player.Name);
        }

        return true;
    }

    private static Item CreateItem(Player player, ItemDefinition definition, byte level)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = level;

        // A stackable item, like a potion, comes as a full stack.
        item.Durability = definition.Durability;
        return item;
    }

    private static async ValueTask TakeBackAsync(Player player, IStorage inventory, List<Item> added)
    {
        foreach (var item in added)
        {
            var slot = item.ItemSlot;
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        }
    }
}
