// <copyright file="WeeklyGoalStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using Npgsql;
using NpgsqlTypes;

/// <summary>
/// The counts of the weekly goals, from the registro (<c>tbt.event</c>), the reached goals
/// (<c>tbt.weekly_goal</c>, once per week) and the given rewards (<c>tbt.weekly_goal_reward</c>,
/// once per account and week).
/// </summary>
internal static class WeeklyGoalStore
{
    /// <summary>
    /// What each human account counted in a time range, for the event kind and, for a mini game, its
    /// type. A reset counts the resets it made, which is one unless a reset was missed in between.
    /// </summary>
    private const string ContributionsSql = """
        select account_id, sum(case when kind = 'reset' then greatest(coalesce(resets - (data->>'previous')::integer, 1), 1) else 1 end)::bigint
        from tbt.event
        where kind = $1 and at >= $2 and at < $3 and not is_bot and account_id is not null
            and ($4::text is null or data->>'type' = $4)
            and ($5::uuid is null or account_id = $5)
        group by account_id
        """;

    /// <summary>
    /// Gets what each human account counted for the goal in the week.
    /// </summary>
    /// <param name="kind">What counts.</param>
    /// <param name="week">The week.</param>
    /// <param name="accountId">The only account to count, or <c>null</c> for all.</param>
    /// <returns>The counts, by account identifier; an account which counted nothing is missing.</returns>
    public static async ValueTask<Dictionary<Guid, int>> GetContributionsAsync(WeeklyGoalKind kind, WeeklyGoalWeek week, Guid? accountId = null)
    {
        var (eventKind, miniGame) = GetFilter(kind);
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(ContributionsSql, connection);
        command.Parameters.AddWithValue(eventKind);
        command.Parameters.AddWithValue(week.Start);
        command.Parameters.AddWithValue(week.End);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = miniGame?.ToString() ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = accountId ?? (object)DBNull.Value });
        var contributions = new Dictionary<Guid, int>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            contributions[reader.GetGuid(0)] = (int)Math.Min(reader.GetInt64(1), int.MaxValue);
        }

        return contributions;
    }

    /// <summary>
    /// Gets when the goal of the week was reached.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <returns>The goal as it was reached, and when; <c>null</c> while it isn't, or when a later version renamed its kind.</returns>
    public static async ValueTask<(WeeklyGoalDefinition Goal, DateTime ReachedAt)?> GetReachedAsync(WeeklyGoalWeek week)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select kind, target, name, reached_at from tbt.weekly_goal where week = $1", connection);
        command.Parameters.AddWithValue(week.Date);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false) || !Enum.TryParse<WeeklyGoalKind>(reader.GetString(0), out var kind))
        {
            return null;
        }

        var goal = new WeeklyGoalDefinition { Kind = kind, Target = reader.GetInt32(1), Name = reader.GetString(2) };
        return (goal, reader.GetDateTime(3));
    }

    /// <summary>
    /// Records that the goal of the week is reached, once per week.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <param name="goal">The goal.</param>
    /// <returns>The goal as it was reached, and when: now, or earlier when another game server recorded it first.</returns>
    public static async ValueTask<(WeeklyGoalDefinition Goal, DateTime ReachedAt)?> RecordReachedAsync(WeeklyGoalWeek week, WeeklyGoalDefinition goal)
    {
        await using (var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false))
        {
            await using var command = new NpgsqlCommand(
                "insert into tbt.weekly_goal (week, kind, target, name) values ($1, $2, $3, $4) on conflict do nothing",
                connection);
            command.Parameters.AddWithValue(week.Date);
            command.Parameters.AddWithValue(goal.Kind.ToString());
            command.Parameters.AddWithValue(goal.Target);
            command.Parameters.AddWithValue(goal.Name ?? string.Empty);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        return await GetReachedAsync(week).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the accounts which got the reward of the week.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <returns>The account identifiers.</returns>
    public static async ValueTask<HashSet<Guid>> GetRewardedAccountsAsync(WeeklyGoalWeek week)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select account_id from tbt.weekly_goal_reward where week = $1", connection);
        command.Parameters.AddWithValue(week.Date);
        var accounts = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            accounts.Add(reader.GetGuid(0));
        }

        return accounts;
    }

    /// <summary>
    /// Gets the reached goals since the week, whose reward the account didn't get yet.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="since">The first week.</param>
    /// <returns>The weeks and what counted in them.</returns>
    public static async ValueTask<List<(WeeklyGoalWeek Week, WeeklyGoalKind Kind)>> GetUnrewardedWeeksAsync(Guid accountId, WeeklyGoalWeek since)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select g.week, g.kind from tbt.weekly_goal g
            where g.week >= $2 and not exists (select 1 from tbt.weekly_goal_reward r where r.week = g.week and r.account_id = $1)
            order by g.week
            """,
            connection);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(since.Date);
        var weeks = new List<(WeeklyGoalWeek Week, WeeklyGoalKind Kind)>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            // A kind which a later version renamed can't be counted anymore, so its reward is skipped.
            if (Enum.TryParse<WeeklyGoalKind>(reader.GetString(1), out var kind))
            {
                weeks.Add((new WeeklyGoalWeek(await reader.GetFieldValueAsync<DateOnly>(0).ConfigureAwait(false)), kind));
            }
        }

        return weeks;
    }

    /// <summary>
    /// Records the reward of the week for the account, once.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="character">The character which gets it.</param>
    /// <returns><c>true</c>, if it was recorded now; <c>false</c>, if the account got it before.</returns>
    public static async ValueTask<bool> TryRecordRewardAsync(WeeklyGoalWeek week, Guid accountId, string character)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "insert into tbt.weekly_goal_reward (week, account_id, character) values ($1, $2, $3) on conflict do nothing",
            connection);
        command.Parameters.AddWithValue(week.Date);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(character);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Removes the reward of the week for the account, e.g. when it didn't fit into the inventory.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The task.</returns>
    public static async ValueTask RemoveRewardAsync(WeeklyGoalWeek week, Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("delete from tbt.weekly_goal_reward where week = $1 and account_id = $2", connection);
        command.Parameters.AddWithValue(week.Date);
        command.Parameters.AddWithValue(accountId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the events of the registro which count for the goal.
    /// </summary>
    /// <param name="kind">What counts.</param>
    /// <returns>The kind of the events and, for a mini game, its type.</returns>
    internal static (string EventKind, MiniGameType? MiniGame) GetFilter(WeeklyGoalKind kind) => kind switch
    {
        WeeklyGoalKind.BloodCastle => ("minigame_end", MiniGameType.BloodCastle),
        WeeklyGoalKind.DevilSquare => ("minigame_end", MiniGameType.DevilSquare),
        WeeklyGoalKind.ChaosCastle => ("minigame_start", MiniGameType.ChaosCastle),
        WeeklyGoalKind.Resets => ("reset", null),
        WeeklyGoalKind.TreasureChests => ("chest", null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of weekly goal."),
    };
}
