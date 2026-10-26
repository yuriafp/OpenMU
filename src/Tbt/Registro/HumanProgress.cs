// <copyright file="HumanProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Threading;
using Npgsql;

/// <summary>
/// The resets of the most advanced human (PLANO-SERVIDOR.md, 5.2 and 5.7): the most resets among the
/// accounts which played in the last days, without bots, game masters and test accounts. It's read
/// from <c>tbt.character_state</c> and cached, because the bots ask for it all the time. Each plugin
/// which asks has its own settings, so there is a cached value for each of them.
/// </summary>
public static class HumanProgress
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    private static readonly Lock RefreshLock = new();

    private static readonly Dictionary<string, CachedValue> CachedValues = new();

    /// <summary>
    /// Gets the resets of the most advanced human, or <c>null</c> while they are unknown, e.g.
    /// before the first read or when the database fails. Starts a refresh in the background when
    /// the cached value is old.
    /// </summary>
    /// <param name="activeDays">The days in which an account must have played to count.</param>
    /// <param name="excludedAccounts">The login names of the accounts which don't count, e.g. test accounts.</param>
    /// <param name="logger">The logger for failed reads.</param>
    /// <returns>The resets of the most advanced human.</returns>
    public static int? GetMaximumResets(int activeDays, IReadOnlyCollection<string> excludedAccounts, ILogger logger)
    {
        lock (RefreshLock)
        {
            return GetCachedValue(activeDays, excludedAccounts, logger).MaximumResets;
        }
    }

    /// <summary>
    /// Gets the resets of the most advanced human like <see cref="GetMaximumResets"/>, but waits for
    /// the refresh when the cached value is old, e.g. for a character which just entered the world.
    /// </summary>
    /// <param name="activeDays">The days in which an account must have played to count.</param>
    /// <param name="excludedAccounts">The login names of the accounts which don't count, e.g. test accounts.</param>
    /// <param name="logger">The logger for failed reads.</param>
    /// <returns>The resets of the most advanced human, or <c>null</c> when the database fails.</returns>
    public static async Task<int?> GetMaximumResetsAsync(int activeDays, IReadOnlyCollection<string> excludedAccounts, ILogger logger)
    {
        CachedValue cachedValue;
        Task? refresh;
        lock (RefreshLock)
        {
            cachedValue = GetCachedValue(activeDays, excludedAccounts, logger);
            refresh = cachedValue.Refresh;
        }

        if (refresh is not null)
        {
            // The refresh catches its exceptions.
            await refresh.ConfigureAwait(false);
        }

        lock (RefreshLock)
        {
            return cachedValue.MaximumResets;
        }
    }

    /// <summary>
    /// Makes the next read refresh the cached values, e.g. after a human reset.
    /// </summary>
    public static void Invalidate()
    {
        lock (RefreshLock)
        {
            foreach (var cachedValue in CachedValues.Values)
            {
                cachedValue.RefreshDueUtc = DateTime.MinValue;
            }
        }
    }

    /// <summary>
    /// Gets the cached value of the settings, and starts its refresh when it's old. Must be called
    /// in the <see cref="RefreshLock"/>.
    /// </summary>
    /// <param name="activeDays">The days in which an account must have played to count.</param>
    /// <param name="excludedAccounts">The login names of the accounts which don't count, e.g. test accounts.</param>
    /// <param name="logger">The logger for failed reads.</param>
    /// <returns>The cached value.</returns>
    private static CachedValue GetCachedValue(int activeDays, IReadOnlyCollection<string> excludedAccounts, ILogger logger)
    {
        var key = activeDays + "|" + string.Join(',', excludedAccounts);
        if (!CachedValues.TryGetValue(key, out var cachedValue))
        {
            cachedValue = new CachedValue();
            CachedValues.Add(key, cachedValue);
        }

        if (DateTime.UtcNow >= cachedValue.RefreshDueUtc && cachedValue.Refresh is not { IsCompleted: false })
        {
            cachedValue.RefreshDueUtc = DateTime.UtcNow + RefreshInterval;
            cachedValue.Refresh = RefreshAsync(cachedValue, activeDays, excludedAccounts.ToArray(), logger);
        }

        return cachedValue;
    }

    private static async Task RefreshAsync(CachedValue cachedValue, int activeDays, string[] excludedAccounts, ILogger logger)
    {
        try
        {
            await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand(
                """
                select coalesce(max(cs.resets), 0)
                from tbt.character_state cs
                join data."Account" a on a."Id" = cs.account_id
                where not a."IsBot" and not a."IsTemplate"
                  and a."State" = 0
                  and not (lower(a."LoginName") = any($2))
                  and cs.account_id in (
                    select account_id from tbt.character_state where updated_at > now() - make_interval(days => $1))
                """,
                connection);
            command.Parameters.AddWithValue(activeDays);
            command.Parameters.AddWithValue(excludedAccounts.Select(a => a.ToLowerInvariant()).ToArray());
            var result = Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
            lock (RefreshLock)
            {
                cachedValue.MaximumResets = result;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't read the resets of the most advanced human.");
            lock (RefreshLock)
            {
                // Better no bot reset at all than bots getting ahead of the humans. The next read tries again soon.
                cachedValue.MaximumResets = null;
                cachedValue.RefreshDueUtc = DateTime.UtcNow + RetryInterval;
            }
        }
    }

    /// <summary>
    /// The cached resets of the most advanced human for one set of settings.
    /// </summary>
    private sealed class CachedValue
    {
        /// <summary>
        /// Gets or sets the resets of the most advanced human, or <c>null</c> while they are unknown.
        /// </summary>
        public int? MaximumResets { get; set; }

        /// <summary>
        /// Gets or sets when the next read refreshes the value.
        /// </summary>
        public DateTime RefreshDueUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the last refresh.
        /// </summary>
        public Task? Refresh { get; set; }
    }
}
