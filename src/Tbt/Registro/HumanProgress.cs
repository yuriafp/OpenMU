// <copyright file="HumanProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Threading;
using Npgsql;

/// <summary>
/// The resets of the most advanced human (PLANO-SERVIDOR.md, 5.2 and 5.7): the most resets among the
/// accounts which played in the last days, without bots, game masters and test accounts. It's read
/// from <c>tbt.character_state</c> and cached, because the bots ask for it all the time.
/// </summary>
public static class HumanProgress
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private static readonly Lock RefreshLock = new();

    private static int? _maximumResets;

    private static DateTime _refreshDueUtc = DateTime.MinValue;

    private static Task? _refresh;

    private static string _key = string.Empty;

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
            var key = activeDays + "|" + string.Join(',', excludedAccounts);
            if (key != _key)
            {
                // The configuration changed; the cached value is of the old one.
                _key = key;
                _refreshDueUtc = DateTime.MinValue;
            }

            if (DateTime.UtcNow >= _refreshDueUtc && _refresh is not { IsCompleted: false })
            {
                _refreshDueUtc = DateTime.UtcNow + RefreshInterval;
                _refresh = RefreshAsync(activeDays, excludedAccounts.ToArray(), logger);
            }

            return _maximumResets;
        }
    }

    /// <summary>
    /// Makes the next read refresh the cached value, e.g. after a human reset.
    /// </summary>
    public static void Invalidate()
    {
        lock (RefreshLock)
        {
            _refreshDueUtc = DateTime.MinValue;
        }
    }

    private static async Task RefreshAsync(int activeDays, string[] excludedAccounts, ILogger logger)
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
                _maximumResets = result;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't read the resets of the most advanced human.");
            lock (RefreshLock)
            {
                // Better no bot reset at all than bots getting ahead of the humans.
                _maximumResets = null;
            }
        }
    }
}
