// <copyright file="StarterKitStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

using System.Collections.Concurrent;
using MUnique.OpenMU.Persistence;
using Npgsql;

/// <summary>
/// The starter kits of the characters (<c>tbt.starter_kit</c>, with its items in <c>tbt.starter_kit_item</c>).
/// The kit of a character is cached when it enters the world, so the moves, drops, sales and jewels
/// know its items without the database.
/// </summary>
internal static class StarterKitStore
{
    private static readonly ConcurrentDictionary<Guid, ActiveStarterKit> ActiveKits = new();

    /// <summary>The characters of the kit items, by item identifier.</summary>
    private static readonly ConcurrentDictionary<Guid, Guid> KitItems = new();

    /// <summary>
    /// Gets a value indicating whether no kit is cached.
    /// </summary>
    public static bool IsEmpty => ActiveKits.IsEmpty;

    /// <summary>
    /// Determines whether the item belongs to a cached kit.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if the item is a kit item.</returns>
    public static bool IsKitItem(Item item) => !KitItems.IsEmpty && KitItems.ContainsKey(item.GetId());

    /// <summary>
    /// Gets the cached kit of the character.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <returns>The kit, or <c>null</c>.</returns>
    public static ActiveStarterKit? GetActive(Guid characterId)
        => ActiveKits.TryGetValue(characterId, out var kit) ? kit : null;

    /// <summary>
    /// Caches the kit of the character. A cached kit with the same expiry stays, so a removal which
    /// already started for it isn't started again.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <param name="expiresAt">The time of expiry, in UTC.</param>
    /// <param name="itemIds">The identifiers of the items.</param>
    /// <returns>The cached kit.</returns>
    public static ActiveStarterKit Activate(Guid characterId, DateTime expiresAt, IReadOnlyCollection<Guid> itemIds)
    {
        var kit = ActiveKits.AddOrUpdate(
            characterId,
            _ => new ActiveStarterKit(characterId, expiresAt, itemIds),
            (_, cached) => cached.ExpiresAt == expiresAt ? cached : new ActiveStarterKit(characterId, expiresAt, itemIds));
        foreach (var itemId in kit.ItemIds)
        {
            KitItems[itemId] = characterId;
        }

        return kit;
    }

    /// <summary>
    /// Removes the kit of the character from the cache.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    public static void Forget(Guid characterId)
    {
        if (!ActiveKits.TryRemove(characterId, out var kit))
        {
            return;
        }

        foreach (var itemId in kit.ItemIds)
        {
            KitItems.TryRemove(new KeyValuePair<Guid, Guid>(itemId, characterId));
        }
    }

    /// <summary>
    /// Removes the expired kit of the character from the cache, but its items stay bound: a trade
    /// or mix which was cancelled at the same time may bring them back from the backup of the
    /// inventory, and then they leave on the next entry (<see cref="StarterKitRecord.ItemIds"/>).
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    public static void Retire(Guid characterId) => ActiveKits.TryRemove(characterId, out _);

    /// <summary>
    /// Loads the kit of the character.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <returns>The kit, or <c>null</c> when the character never got one.</returns>
    public static async ValueTask<StarterKitRecord?> LoadAsync(Guid characterId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select k.expires_at, k.removed_at is not null, array_remove(array_agg(i.item_id), null)
            from tbt.starter_kit k
            left join tbt.starter_kit_item i on i.character_id = k.character_id
            where k.character_id = $1
            group by k.character_id
            """,
            connection);
        command.Parameters.AddWithValue(characterId);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        return new StarterKitRecord(
            await reader.GetFieldValueAsync<DateTime>(0).ConfigureAwait(false),
            reader.GetBoolean(1),
            await reader.GetFieldValueAsync<Guid[]>(2).ConfigureAwait(false));
    }

    /// <summary>
    /// Records the kit of the character with its items, once per character.
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="character">The character name.</param>
    /// <param name="givenAt">The time it was given, in UTC.</param>
    /// <returns><c>true</c>, if it was recorded now; <c>false</c>, if it was recorded before.</returns>
    public static async ValueTask<bool> TryRecordAsync(ActiveStarterKit kit, Guid accountId, string character, DateTime givenAt)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using (var command = new NpgsqlCommand(
            "insert into tbt.starter_kit (character_id, account_id, character, given_at, expires_at) values ($1, $2, $3, $4, $5) on conflict do nothing",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(kit.CharacterId);
            command.Parameters.AddWithValue(accountId);
            command.Parameters.AddWithValue(character);
            command.Parameters.AddWithValue(givenAt);
            command.Parameters.AddWithValue(kit.ExpiresAt);
            if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) != 1)
            {
                return false;
            }
        }

        await using (var command = new NpgsqlCommand(
            "insert into tbt.starter_kit_item (item_id, character_id) select unnest($1::uuid[]), $2",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(kit.ItemIds.ToArray());
            command.Parameters.AddWithValue(kit.CharacterId);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Records that the expired kit of the character left its inventory. The record stays, so the
    /// character doesn't get another kit.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <returns>The task.</returns>
    public static async ValueTask MarkRemovedAsync(Guid characterId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("update tbt.starter_kit set removed_at = now() where character_id = $1 and removed_at is null", connection);
        command.Parameters.AddWithValue(characterId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the kit of the character with its items, when it wasn't given, so it's given on the
    /// next entry. The kit of a deleted character stays: should the deletion not be saved, the
    /// character keeps its kit bound.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <returns>The task.</returns>
    public static async ValueTask DeleteAsync(Guid characterId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("delete from tbt.starter_kit where character_id = $1", connection);
        command.Parameters.AddWithValue(characterId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
