// <copyright file="StarterKitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.PeriodItems;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The starter kit (PLANO-SERVIDOR.md, 5.10): a new human character gets 500 extra points, and on
/// its first entry into the world the kit of its class (<see cref="StarterKitCatalog"/>) for 10 days.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The kit is bound to the character: it doesn't go into the vault, a trade, a personal store,
/// the Chaos Machine or the Oficina, and it can't be dropped. Sold to a merchant it's worth 0 zen
/// (<see cref="StarterKitSellHandlerPlugIn"/>), and the jewels which change an item refuse it, so none is wasted.</item>
/// <item>When the time is up, the kit leaves the inventory: right away while the character is in the
/// world, else on its next entry. Until then, its items keep their full durability: the Panda would be
/// destroyed at 0, and the client shows no durability on items with an expiry.</item>
/// <item>The game client gets the expiry of the kit items (<see cref="PeriodItemRef"/>) and shows it
/// on them, like on the timed items of the cash shop: at the entry, when the kit is given, when an item
/// of it changes its slot, since the client marks the slot, and again at every check, since the client
/// forgets it whenever it gets the whole inventory again, e.g. after a trade. The entry and <c>/kit</c>
/// show it, too.</item>
/// </list>
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Kit iniciante", Description = "Gives new human characters extra level-up points and, on their first entry, a temporary starter kit of their class with the Pet Panda.")]
[Guid("28E3DCFC-6E2A-4F6B-AFEF-21311F478234")]
public class StarterKitPlugIn : ICharacterCreatedPlugIn, IPlayerStateChangedPlugIn, IPeriodicTaskPlugIn, IItemMovingPlugIn, GameLogic.PlugIns.IItemMovedPlugIn, IItemDropPlugIn, IItemConsumingPlugIn, ICharacterDeletedPlugIn, ISupportCustomConfiguration<StarterKitConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string BoundMessage = "Os itens do kit iniciante não saem do inventário. Vendidos ao NPC, valem 0 zen.";

    /// <summary>
    /// The check of the expiry and of the durability of the kit items. The Panda loses durability with
    /// the damage the character takes, and the pet is destroyed at 0, so the durability is refilled often.
    /// It's also how long the client may show a kit item without its expiry.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan LastHour = TimeSpan.FromHours(1);

    /// <summary>The jewels and stones which change the item they are used on.</summary>
    private static readonly ItemIdentifier[] ItemChangingJewels =
    [
        ItemConstants.JewelOfBless,
        ItemConstants.JewelOfSoul,
        ItemConstants.JewelOfLife,
        ItemConstants.JewelOfHarmony,
        ItemConstants.LowerRefineStone,
        ItemConstants.HigherRefineStone,
    ];

    /// <summary>The periodic checks, by game server: they share this instance.</summary>
    private readonly ConcurrentDictionary<GameContext, PeriodicCheck> _checks = new();

    /// <summary>
    /// The players whose kit couldn't be checked at the entry, e.g. because the database didn't
    /// answer. Until it's checked, their kit isn't bound, so the periodic check tries again.
    /// </summary>
    private readonly ConcurrentDictionary<Player, StarterKitPendingEntry> _pendingEntries = new();

    /// <inheritdoc />
    public StarterKitConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new StarterKitConfiguration();

    /// <inheritdoc />
    public void CharacterCreated(Account account, Character createdCharacter, IContext persistenceContext, GameConfiguration gameConfiguration, ILogger logger)
    {
        try
        {
            // Also called at the first entry of a character which was created in the database
            // without an inventory, so the date of creation counts, like for the kit.
            var configuration = this.Configuration ??= new StarterKitConfiguration();
            if (account is { IsBot: false, IsTemplate: false } && createdCharacter.CreateDate >= configuration.CreatedFromUtc)
            {
                // Saved with the new character. The reset replaces all points, so they last until the first reset.
                createdCharacter.LevelUpPoints += configuration.LevelUpPoints;
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the creation.
            logger.LogWarning(ex, "Couldn't give the extra points to the new character {Character}.", createdCharacter.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState))
        {
            this._pendingEntries.TryRemove(player, out _);
            return;
        }

        if (!TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) || !player.IsHuman() || player.SelectedCharacter is not { } character)
        {
            return;
        }

        try
        {
            if (await EnteredWorldAsync(player, character, this.Configuration ??= new StarterKitConfiguration()).ConfigureAwait(false))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the entry.
            player.Logger.LogWarning(ex, "Couldn't check the starter kit of {Character}; tried again in a moment.", player.Name);
        }

        this._pendingEntries[player] = new StarterKitPendingEntry(character.Id, 1, DateTime.UtcNow + CheckInterval);
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (StarterKitStore.IsEmpty && this._pendingEntries.IsEmpty)
        {
            return;
        }

        var check = this._checks.GetOrAdd(gameContext, _ => new PeriodicCheck());
        if (!check.TryStart(now, CheckInterval))
        {
            return;
        }

        try
        {
            var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
            var retryFailed = false;
            foreach (var player in players)
            {
                // After a failed attempt, the database probably doesn't answer the next ones either,
                // and each would wait for its timeout, so they wait for the next check.
                if (!retryFailed && this._pendingEntries.TryGetValue(player, out var pending) && now >= pending.NextAttemptUtc)
                {
                    retryFailed = !await this.RetryEntryAsync(player, pending, now).ConfigureAwait(false);
                }

                // An open trade or mix keeps a backup of the inventory, which a cancel restores, so the kit waits for its end.
                if (player.SelectedCharacter is { } character
                    && StarterKitStore.GetActive(character.Id) is { } kit
                    && player.IsInWorld()
                    && player.PlayerState.CurrentState != PlayerState.ChangingMap
                    && player.BackupInventory is null)
                {
                    await CheckAsync(player, kit, player.Inventory, now).ConfigureAwait(false);
                }
            }

            this.DropLeftEntries(gameContext, players);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger(this.GetType().Name).LogWarning(ex, "Couldn't check the starter kits.");
        }
        finally
        {
            check.End();
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        foreach (var check in this._checks.Values)
        {
            check.ForceStart();
        }
    }

    /// <inheritdoc />
    public void ItemMoving(Player player, Item item, Storages targetStorage, int slot, CancelEventArgs eventArgs)
    {
        try
        {
            if (targetStorage != Storages.Inventory && StarterKitStore.IsKitItem(item))
            {
                eventArgs.Cancel = true;
                ShowLater(player, BoundMessage, consumptionFailed: false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the move of {Item} of {Character}.", item, player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask ItemMovedAsync(Player player, Item item)
    {
        try
        {
            if (StarterKitStore.IsKitItem(item)
                && player.IsHuman()
                && player.SelectedCharacter is { } character
                && StarterKitStore.GetActive(character.Id) is { } kit)
            {
                await SendExpiryOfMoveAsync(player, kit, item, player.Inventory).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't send the expiry of the moved {Item} of {Character}.", item, player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask HandleItemDropAsync(Player player, Item item, Point target, IItemDropPlugIn.ItemDropArguments dropArgs)
    {
        try
        {
            if (StarterKitStore.IsKitItem(item))
            {
                dropArgs.WasHandled = true;
                dropArgs.Success = false;
                await player.ShowMessageAsync(BoundMessage).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the drop of {Item} of {Character}.", item, player.Name);
        }
    }

    /// <inheritdoc />
    public void ItemConsuming(Player player, Item item, Item? targetItem, CancelEventArgs eventArgs)
    {
        try
        {
            // Only these jewels change their target: a potion names a target, too, the weapon in hand.
            if (targetItem is not null
                && item.Definition is { } definition
                && ItemChangingJewels.Contains(new ItemIdentifier(definition.Number, definition.Group))
                && StarterKitStore.IsKitItem(targetItem))
            {
                eventArgs.Cancel = true;
                ShowLater(player, "As joias não funcionam nos itens do kit iniciante, que expiram.", consumptionFailed: true);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the use of {Item} of {Character}.", item, player.Name);
        }
    }

    /// <inheritdoc />
    public void CharacterDeleted(Player player, Character deletedCharacter)
    {
        try
        {
            // Only the cache: the record stays (see StarterKitStore.DeleteAsync).
            StarterKitStore.Forget(deletedCharacter.Id);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't forget the starter kit of the deleted character {Character}.", deletedCharacter.Name);
        }
    }

    /// <summary>
    /// Formats the time left of the kit, e.g. "Kit iniciante: expira em 9 dias e 5 h, em 01/11 às 19:30 (horário de Brasília).".
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The message.</returns>
    internal static string FormatRemaining(ActiveStarterKit kit, DateTime utcNow)
    {
        var left = kit.ExpiresAt - utcNow;
        if (left <= TimeSpan.Zero)
        {
            return "Seu kit iniciante expirou e sai do inventário em instantes.";
        }

        var days = (int)left.TotalDays;
        var leftText = days switch
        {
            > 1 => $"{days} dias e {left.Hours} h",
            1 => $"1 dia e {left.Hours} h",
            _ when left.TotalHours >= 1 => $"{(int)left.TotalHours} h e {left.Minutes} min",
            _ => $"{Math.Max(1, left.Minutes)} min",
        };
        return $"Kit iniciante: expira em {leftText}, em {TbtTime.Format(kit.ExpiresAt)} (horário de Brasília).";
    }

    /// <summary>
    /// Sends the expiry of the kit items among the items to the game client, which shows it on them.
    /// Bots and offline levelings have no client. A failure is only logged: the kit stays as it is.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="kit">The kit.</param>
    /// <param name="items">The items, of which the items of the kit are sent, at their slots.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask SendExpiryAsync(Player player, ActiveStarterKit kit, IEnumerable<Item> items)
    {
        try
        {
            await player.GetConnection().SendPeriodItemsAsync(kit.GetPeriodItems(items)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't send the expiry of the starter kit to the client of {Character}.", player.Name);
        }
    }

    /// <summary>
    /// Sends the expiry of a moved kit item to the game client, for its new slot, since the client
    /// marks the slot. A move which failed leaves the item at its old slot, which is sent, too.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="kit">The kit of the player.</param>
    /// <param name="item">The moved item.</param>
    /// <param name="inventory">The inventory of the player; an item elsewhere isn't sent.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask SendExpiryOfMoveAsync(Player player, ActiveStarterKit kit, Item item, IStorage? inventory)
    {
        if (ReferenceEquals(inventory?.GetItem(item.ItemSlot), item))
        {
            await SendExpiryAsync(player, kit, [item]).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The periodic check of a kit: removes it when it's expired, else keeps its items at their full
    /// durability and sends their expiry to the client again.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="kit">The kit of the player.</param>
    /// <param name="inventory">The inventory of the player.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask CheckAsync(Player player, ActiveStarterKit kit, IStorage? inventory, DateTime now)
    {
        try
        {
            if (kit.IsExpired(now))
            {
                await ExpireAsync(player, kit).ConfigureAwait(false);
                return;
            }

            await RefillDurabilityAsync(player, kit, inventory).ConfigureAwait(false);

            // The client forgets the expiry whenever it gets the whole inventory again, e.g. after a
            // trade (OpenMU's TradeFinishedPlugIn). The packets are small, and the client only marks
            // the items again.
            await SendExpiryOfInventoryAsync(player, kit, inventory).ConfigureAwait(false);
            if (!kit.LastHourWarned && kit.ExpiresAt - now <= LastHour)
            {
                kit.LastHourWarned = true;
                await player.ShowMessageAsync(FormatRemaining(kit, now)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the starter kit of {Character}.", player.Name);
        }
    }

    /// <summary>
    /// Makes the item the given item of a kit, like the server gives it.
    /// </summary>
    /// <param name="configuration">The game configuration, with the item definitions.</param>
    /// <param name="context">The persistence context of the player, which creates the options.</param>
    /// <param name="item">The new item.</param>
    /// <param name="kitItem">The item of the kit.</param>
    /// <exception cref="InvalidOperationException">The item, or one of its options, doesn't exist in the configuration.</exception>
    internal static void Configure(GameConfiguration configuration, IContext context, Item item, StarterKitItem kitItem)
    {
        var definition = configuration.Items.FirstOrDefault(d => d.Group == kitItem.Group && d.Number == kitItem.Number)
            ?? throw new InvalidOperationException($"The item {kitItem.Group}/{kitItem.Number} of the starter kit doesn't exist.");
        item.Definition = definition;
        item.Level = kitItem.Level;
        item.HasSkill = definition.Skill is not null;
        var possibleOptions = definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).ToList();
        foreach (var number in kitItem.ExcellentOptions)
        {
            var option = possibleOptions.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Excellent && o.Number == number)
                ?? throw new InvalidOperationException($"The item {kitItem.Group}/{kitItem.Number} of the starter kit has no excellent option {number}.");
            AddOption(context, item, option);
        }

        if (kitItem.Luck)
        {
            var luck = possibleOptions.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Luck)
                ?? throw new InvalidOperationException($"The item {kitItem.Group}/{kitItem.Number} of the starter kit can't have the Luck.");
            AddOption(context, item, luck);
        }

        // After the level and the options, which add to it.
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
    }

    /// <summary>
    /// Checks the kit of a character which entered the world: gives it, shows its time left, or removes it.
    /// </summary>
    /// <returns><c>false</c>, if a trade or mix is open, so the check is tried again later.</returns>
    private static async ValueTask<bool> EnteredWorldAsync(Player player, Character character, StarterKitConfiguration configuration)
    {
        var record = await StarterKitStore.LoadAsync(character.Id).ConfigureAwait(false);
        if (record is null)
        {
            // An offline leveling enters the world, too, but never first.
            if (character.CreateDate < configuration.CreatedFromUtc || player is OfflinePlayer)
            {
                return true;
            }

            return await GiveAsync(player, character, configuration).ConfigureAwait(false);
        }

        if (record.Removed)
        {
            StarterKitStore.Forget(character.Id);
            return await RemoveLeftoversAsync(player, record.ItemIds).ConfigureAwait(false);
        }

        var kit = StarterKitStore.Activate(character.Id, record.ExpiresAt, record.ItemIds);
        var now = DateTime.UtcNow;
        if (kit.IsExpired(now))
        {
            // When it can't be removed now, the periodic check tries again: the kit is cached.
            await ExpireAsync(player, kit).ConfigureAwait(false);
            return true;
        }

        kit.LastHourWarned |= kit.ExpiresAt - now <= LastHour;
        await player.ShowMessageAsync(FormatRemaining(kit, now)).ConfigureAwait(false);

        // The inventory went to the client with the selection of the character, before this entry.
        await SendExpiryOfInventoryAsync(player, kit, player.Inventory).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gives the kit of its class to the character, all or none.
    /// </summary>
    /// <returns><c>false</c>, if a trade or mix is open, so it's given later.</returns>
    private static async ValueTask<bool> GiveAsync(Player player, Character character, StarterKitConfiguration configuration)
    {
        if (StarterKitCatalog.Get(character.CharacterClass) is not { } kitDefinition || player.Account is not { } account)
        {
            return true;
        }

        // The kit changes the inventory, so it takes the persistence lock of the player, like its packet handlers.
        return await player.RunPersistenceExclusiveAsync(async () =>
        {
            // Checked under the lock, which the packet handlers hold when they open a mix or accept
            // a trade: a cancel restores the backup of the inventory, without the kit.
            if (player.BackupInventory is not null)
            {
                return false;
            }

            var items = new List<Item>();
            try
            {
                foreach (var kitItem in kitDefinition.Items)
                {
                    var item = player.PersistenceContext.CreateNew<Item>();
                    items.Add(item);
                    Configure(player.GameContext.Configuration, player.PersistenceContext, item, kitItem);
                }
            }
            catch
            {
                await DeleteAsync(player, items).ConfigureAwait(false);
                throw;
            }

            // Whole seconds, so the expiry stays the same through the database.
            var givenAt = DateTime.UtcNow;
            givenAt = new DateTime(givenAt.Ticks - (givenAt.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
            var kit = new ActiveStarterKit(character.Id, givenAt.AddDays(configuration.Days), items.Select(i => i.GetId()));
            bool recorded;
            try
            {
                recorded = await StarterKitStore.TryRecordAsync(kit, account.GetId(), character.Name, givenAt).ConfigureAwait(false);
            }
            catch
            {
                await DeleteAsync(player, items).ConfigureAwait(false);
                throw;
            }

            if (!recorded)
            {
                await DeleteAsync(player, items).ConfigureAwait(false);
                return true;
            }

            // Cached before the items appear, so they are bound from the start.
            kit = StarterKitStore.Activate(kit.CharacterId, kit.ExpiresAt, kit.ItemIds);
            bool given;
            try
            {
                given = await RewardDelivery.TryGiveAsync(player, items).ConfigureAwait(false);
            }
            catch
            {
                // E.g. the save failed: the items are taken back, and the periodic check tries again.
                StarterKitStore.Forget(character.Id);
                await UndoAsync(player, character).ConfigureAwait(false);
                throw;
            }

            if (!given)
            {
                StarterKitStore.Forget(character.Id);
                await UndoAsync(player, character).ConfigureAwait(false);
                await player.ShowMessageAsync("Abra espaço no inventário e entre de novo para receber o kit iniciante.").ConfigureAwait(false);
                return true;
            }

            // After the items, which appeared on the client one by one.
            await SendExpiryAsync(player, kit, items).ConfigureAwait(false);
            ActivityLog.Write("starter_kit", player, new { kit = kitDefinition.Description, expires_at = kit.ExpiresAt });
            await player.ShowMessageAsync("Você ganhou o kit iniciante!", MessageType.GoldenCenter).ConfigureAwait(false);
            await player.ShowMessageAsync($"Kit iniciante: {kitDefinition.Description} +9 excelentes com Luck.").ConfigureAwait(false);
            await player.ShowMessageAsync($"Mais {kitDefinition.Wings} +9 e Pet Panda, por {configuration.Days} dias, no inventário.").ConfigureAwait(false);
            await player.ShowMessageAsync("Distribua seus pontos (tecla C) para equipar o kit. Use /kit para ver o prazo.").ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private static void AddOption(IContext context, Item item, IncreasableItemOption option)
    {
        var optionLink = context.CreateNew<ItemOptionLink>();
        optionLink.ItemOption = option;
        item.ItemOptions.Add(optionLink);
    }

    private static async ValueTask DeleteAsync(Player player, IEnumerable<Item> items)
    {
        foreach (var item in items)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes the record of a kit which wasn't given, so it's given on the next entry. When that
    /// fails too, the kit stays recorded without its items, which a GM has to fix.
    /// </summary>
    private static async ValueTask UndoAsync(Player player, Character character)
    {
        try
        {
            await StarterKitStore.DeleteAsync(character.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The starter kit of {Character} is recorded in tbt.starter_kit, but wasn't given.", character.Name);
        }
    }

    /// <summary>
    /// Removes the expired kit from the inventory. It's recorded as removed only after the save,
    /// otherwise a crash would bring the items back without the record.
    /// </summary>
    private static async ValueTask ExpireAsync(Player player, ActiveStarterKit kit)
    {
        if (!kit.TryStartExpiry())
        {
            return;
        }

        try
        {
            var removed = await player.RunPersistenceExclusiveAsync(async () =>
            {
                // The backup is checked under the lock, which the packet handlers hold when they
                // open a mix or accept a trade: a cancel would bring the kit back from it. A trade
                // which the partner accepts meanwhile isn't covered; StarterKitStore.Retire is.
                if (player.Inventory is not { } inventory
                    || player.SelectedCharacter?.Id != kit.CharacterId
                    || player.BackupInventory is not null)
                {
                    return -1;
                }

                var items = inventory.Items.Where(i => kit.ItemIds.Contains(i.GetId())).ToList();
                foreach (var item in items)
                {
                    await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
                }

                return items.Count;
            }).ConfigureAwait(false);

            if (removed < 0 || player.SelectedCharacter?.Id != kit.CharacterId || !await player.SaveProgressAsync().ConfigureAwait(false))
            {
                // Left the world, or opened a mix or trade, in the meantime, or not saved: tried again later.
                kit.CancelExpiry();
                return;
            }

            await StarterKitStore.MarkRemovedAsync(kit.CharacterId).ConfigureAwait(false);
            StarterKitStore.Retire(kit.CharacterId);
            ActivityLog.Write("starter_kit_expired", player, new { items = removed });
            await player.ShowMessageAsync("Seu kit iniciante expirou e saiu do inventário. Boa jornada!", MessageType.GoldenCenter).ConfigureAwait(false);
        }
        catch
        {
            kit.CancelExpiry();
            throw;
        }
    }

    /// <summary>
    /// Removes items of an expired kit which came back, e.g. from the backup of the inventory of a
    /// trade which was cancelled while the kit expired (see <see cref="StarterKitStore.Retire"/>).
    /// </summary>
    /// <returns><c>false</c>, if a trade or mix is open, so they are removed later.</returns>
    private static async ValueTask<bool> RemoveLeftoversAsync(Player player, IReadOnlyList<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return true;
        }

        var removed = await player.RunPersistenceExclusiveAsync(async () =>
        {
            // Under the lock, like the expiry: a cancel would bring them back from the backup again.
            if (player.BackupInventory is not null)
            {
                return -1;
            }

            var leftovers = player.Inventory?.Items.Where(i => itemIds.Contains(i.GetId())).ToList() ?? [];
            foreach (var item in leftovers)
            {
                await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
            }

            return leftovers.Count;
        }).ConfigureAwait(false);

        if (removed > 0)
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
            ActivityLog.Write("starter_kit_leftovers", player, new { items = removed });
        }

        return removed >= 0;
    }

    /// <summary>
    /// Refills the durability of the kit items in the inventory, equipped or not, which lost some.
    /// </summary>
    /// <remarks>
    /// Under the lock of the packet handlers, which change the inventory. Like the loss of durability
    /// (<see cref="Player.DecreaseItemDurabilityAsync"/>), an equipped item whose durability factor
    /// changes, e.g. a broken one, updates the attributes, so it gives its full power again.
    /// </remarks>
    private static ValueTask RefillDurabilityAsync(Player player, ActiveStarterKit kit, IStorage? inventory)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            var wornItems = inventory?.Items
                .Where(item => kit.ItemIds.Contains(item.GetId()) && item.Durability < item.GetMaximumDurabilityOfOnePiece())
                .ToList() ?? [];
            foreach (var item in wornItems)
            {
                var previousFactor = item.GetCurrentDurabilityFactor();
                item.Durability = item.GetMaximumDurabilityOfOnePiece();
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
                if (item.ItemSlot <= InventoryConstants.LastEquippableItemSlotIndex
                    && previousFactor != item.GetCurrentDurabilityFactor()
                    && inventory is InventoryStorage inventoryStorage)
                {
                    await inventoryStorage.RaiseEquippedItemsChangedAsync(item, true, true).ConfigureAwait(false);
                }
            }
        });

    /// <summary>
    /// Sends the expiry of the kit items in the inventory to the game client. Under the lock of the
    /// packet handlers, which change the inventory, since the entry may be retried and the check runs
    /// on its own.
    /// </summary>
    private static ValueTask SendExpiryOfInventoryAsync(Player player, ActiveStarterKit kit, IStorage? inventory)
        => player.RunPersistenceExclusiveAsync(() => SendExpiryAsync(player, kit, inventory?.Items ?? []));

    /// <summary>
    /// Shows a message from a synchronous plugin point.
    /// </summary>
    private static void ShowLater(Player player, string message, bool consumptionFailed)
    {
        // The task must not inherit the flow, which carries the persistence lock state of the packet
        // handler (see MilestonePlugIn.CharacterLeveledUp).
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (consumptionFailed)
                    {
                        // The consumption ends without an answer when a plugin cancels it, so the client gets it from here.
                        await player.InvokeViewPlugInAsync<IRequestedItemConsumptionFailedPlugIn>(p => p.RequestedItemConsumptionFailedAsync()).ConfigureAwait(false);
                    }

                    await player.ShowMessageAsync(message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogWarning(ex, "Couldn't answer {Character} about the starter kit.", player.Name);
                }
            });
        }
    }

    /// <summary>
    /// Checks the kit of a player again, whose check at the entry failed or had to wait.
    /// </summary>
    /// <returns><c>false</c>, if the attempt failed.</returns>
    private async ValueTask<bool> RetryEntryAsync(Player player, StarterKitPendingEntry pending, DateTime utcNow)
    {
        if (player.SelectedCharacter is not { } character || character.Id != pending.CharacterId || !player.IsInWorld())
        {
            this._pendingEntries.TryRemove(new KeyValuePair<Player, StarterKitPendingEntry>(player, pending));
            return true;
        }

        if (player.PlayerState.CurrentState == PlayerState.ChangingMap)
        {
            return true;
        }

        try
        {
            if (await EnteredWorldAsync(player, character, this.Configuration ??= new StarterKitConfiguration()).ConfigureAwait(false))
            {
                this._pendingEntries.TryRemove(new KeyValuePair<Player, StarterKitPendingEntry>(player, pending));
            }

            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the starter kit of {Character} again (attempt {Attempt}).", player.Name, pending.Attempts + 1);
            this._pendingEntries.TryUpdate(player, pending.Failed(utcNow, CheckInterval), pending);
            return false;
        }
    }

    /// <summary>
    /// Drops the pending entries of the players who left this game server: the exit may have come
    /// before the failed entry was noted.
    /// </summary>
    private void DropLeftEntries(GameContext gameContext, IList<Player> players)
    {
        foreach (var player in this._pendingEntries.Keys)
        {
            if (ReferenceEquals(player.GameContext, gameContext) && !players.Contains(player))
            {
                this._pendingEntries.TryRemove(player, out _);
            }
        }
    }
}
