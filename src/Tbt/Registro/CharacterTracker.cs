// <copyright file="CharacterTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// Keeps the last known level and resets of the human characters in <c>tbt.character_state</c>.
/// OpenMU has no plugin point for the reset, and a reset logs the player out, so the reset shows
/// when the character enters the world again with more resets than at its previous entry.
/// </summary>
/// <remarks>
/// The comparison is with the resets at the previous entry (<c>resets_at_entry</c>), not with the
/// last known resets: the reset raises the resets before its logout, so the exit records them already.
/// </remarks>
public static class CharacterTracker
{
    private static readonly ConditionalWeakTable<Player, Session> Sessions = new();

    /// <summary>
    /// Gets what was known about the character before it entered the world, and records its
    /// current state. Every caller during the same stay in the world gets the same result, so
    /// more than one plugin can use it.
    /// </summary>
    /// <param name="player">The human player who entered the world.</param>
    /// <returns>
    /// The previous state, with the last known level and the resets at the previous entry, or
    /// <c>null</c> when the character was never seen before.
    /// </returns>
    public static Task<CharacterState?> EnteredWorldAsync(Player player)
    {
        if (player.Account is not { } account || player.SelectedCharacter is not { } character)
        {
            return Task.FromResult<CharacterState?>(null);
        }

        lock (Sessions)
        {
            if (Sessions.TryGetValue(player, out var session) && session.Exit is null && ReferenceEquals(session.Character, character))
            {
                return session.Previous;
            }

            session = new Session(character, RecordEntryAsync(Read(account, character)));
            Sessions.AddOrUpdate(player, session);
            return session.Previous;
        }
    }

    /// <summary>
    /// Gets the character with which the player is in the world. When the player leaves the world,
    /// OpenMU already took the character away from it before the plugins are told.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The character, or <c>null</c> when the entry wasn't tracked.</returns>
    public static Character? GetCharacter(Player player)
    {
        lock (Sessions)
        {
            return Sessions.TryGetValue(player, out var session) ? session.Character : null;
        }
    }

    /// <summary>
    /// Records the state of the character with which the player left the world, and ends the
    /// stay, so that the next entry is detected as such. Every caller for the same stay gets the
    /// same result, so more than one plugin can use it.
    /// </summary>
    /// <param name="player">The human player who left the world.</param>
    /// <returns>The task.</returns>
    public static Task LeftWorldAsync(Player player)
    {
        lock (Sessions)
        {
            if (!Sessions.TryGetValue(player, out var session) || player.Account is not { } account)
            {
                return Task.CompletedTask;
            }

            return session.Exit ??= RecordExitAsync(Read(account, session.Character));
        }
    }

    /// <summary>
    /// Reads the level and resets of a character.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="character">The character.</param>
    /// <returns>The state of the character.</returns>
    public static CharacterState Read(Account account, Character character)
    {
        int ValueOf(AttributeDefinition definition)
            => (int)(character.Attributes.FirstOrDefault(a => a.Definition == definition)?.Value ?? 0);

        return new CharacterState(
            character.GetId(),
            account.GetId(),
            character.Name,
            account.IsBot,
            ValueOf(Stats.Level),
            ValueOf(Stats.Resets));
    }

    private static async Task<CharacterState?> RecordEntryAsync(CharacterState state)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        CharacterState? previous = null;
        await using (var command = new NpgsqlCommand("select level, coalesce(resets_at_entry, resets) from tbt.character_state where character_id = $1", connection))
        {
            command.Parameters.AddWithValue(state.CharacterId);
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                previous = state with { Level = reader.GetInt32(0), Resets = reader.GetInt32(1) };
            }
        }

        await UpsertAsync(connection, state, true).ConfigureAwait(false);
        if (previous is null || previous.Resets != state.Resets)
        {
            HumanProgress.Invalidate();
        }

        return previous;
    }

    private static async Task RecordExitAsync(CharacterState state)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await UpsertAsync(connection, state, false).ConfigureAwait(false);
        HumanProgress.Invalidate();
    }

    private static async ValueTask UpsertAsync(NpgsqlConnection connection, CharacterState state, bool entering)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into tbt.character_state (character_id, account_id, name, is_bot, level, resets, resets_at_entry, updated_at)
            values ($1, $2, $3, $4, $5, $6, $7, now())
            on conflict (character_id) do update
            set name = excluded.name, level = excluded.level, resets = excluded.resets,
                resets_at_entry = coalesce(excluded.resets_at_entry, tbt.character_state.resets_at_entry), updated_at = now()
            """,
            connection);
        command.Parameters.AddWithValue(state.CharacterId);
        command.Parameters.AddWithValue(state.AccountId);
        command.Parameters.AddWithValue(state.Name);
        command.Parameters.AddWithValue(state.IsBot);
        command.Parameters.AddWithValue(state.Level);
        command.Parameters.AddWithValue(state.Resets);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = entering ? state.Resets : DBNull.Value });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private sealed class Session(Character character, Task<CharacterState?> previous)
    {
        public Character Character { get; } = character;

        public Task<CharacterState?> Previous { get; } = previous;

        /// <summary>
        /// Gets or sets the recording of the exit; set once the character left the world.
        /// </summary>
        public Task? Exit { get; set; }
    }
}
