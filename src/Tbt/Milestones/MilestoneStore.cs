// <copyright file="MilestoneStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

using System.Collections.Concurrent;
using Npgsql;

/// <summary>
/// The milestones of the accounts (<c>tbt.milestone</c>, once per account) and the firsts of the
/// server (<c>tbt.server_first</c>, once per server). The milestones of an account are cached when
/// it enters the world, so the level-up can check them without the database.
/// </summary>
internal static class MilestoneStore
{
    private static readonly ConcurrentDictionary<Guid, Task<AccountMilestones>> Loading = new();

    private static readonly ConcurrentDictionary<Guid, AccountMilestones> Loaded = new();

    /// <summary>
    /// Gets the milestones of the account, loading them on the first use.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The milestones of the account.</returns>
    public static async ValueTask<AccountMilestones> GetAsync(Guid accountId)
    {
        if (Loaded.TryGetValue(accountId, out var loaded))
        {
            return loaded;
        }

        var loading = Loading.GetOrAdd(accountId, LoadAsync);
        try
        {
            return Loaded.GetOrAdd(accountId, await loading.ConfigureAwait(false));
        }
        catch
        {
            // Load again on the next use, e.g. when the database is back.
            Loading.TryRemove(new KeyValuePair<Guid, Task<AccountMilestones>>(accountId, loading));
            throw;
        }
    }

    /// <summary>
    /// Gets the milestones of the account, if they are loaded already.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The milestones of the account, or <c>null</c> when they aren't loaded yet.</returns>
    public static AccountMilestones? GetLoaded(Guid accountId)
        => Loaded.TryGetValue(accountId, out var loaded) ? loaded : null;

    /// <summary>
    /// Records the milestone of the account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="milestone">The milestone.</param>
    /// <param name="character">The character which reached it.</param>
    /// <returns><c>true</c>, if it was recorded now; <c>false</c>, if it was recorded before.</returns>
    public static async ValueTask<bool> TryRecordAsync(Guid accountId, string milestone, string character)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "insert into tbt.milestone (account_id, milestone, character) values ($1, $2, $3) on conflict do nothing",
            connection);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(milestone);
        command.Parameters.AddWithValue(character);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Removes the milestone of the account, e.g. when its reward didn't fit into the inventory.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="milestone">The milestone.</param>
    /// <returns>The task.</returns>
    public static async ValueTask RemoveAsync(Guid accountId, string milestone)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("delete from tbt.milestone where account_id = $1 and milestone = $2", connection);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(milestone);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Records a first of the server, like the first reset.
    /// </summary>
    /// <param name="first">The first.</param>
    /// <param name="account">The login name of the account.</param>
    /// <param name="character">The character name.</param>
    /// <returns><c>true</c>, if it was recorded now; <c>false</c>, if someone else was first.</returns>
    public static async ValueTask<bool> TryRecordServerFirstAsync(string first, string account, string character)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "insert into tbt.server_first (first, account, character) values ($1, $2, $3) on conflict do nothing",
            connection);
        command.Parameters.AddWithValue(first);
        command.Parameters.AddWithValue(account);
        command.Parameters.AddWithValue(character);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    private static async Task<AccountMilestones> LoadAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select milestone from tbt.milestone where account_id = $1", connection);
        command.Parameters.AddWithValue(accountId);
        var milestones = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            milestones.Add(reader.GetString(0));
        }

        return new AccountMilestones(milestones);
    }
}
