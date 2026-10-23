// <copyright file="ActivityLog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// The registro (PLANO-SERVIDOR.md, section 8): the events of the game which OpenMU doesn't keep,
/// written to <c>tbt.event</c>. Writing never blocks the game: the entries are queued and written
/// in batches in the background, and when the database fails, they are dropped with a warning.
/// </summary>
public static class ActivityLog
{
    private const int MaximumBatchSize = 500;

    private static readonly Channel<Entry> Entries = Channel.CreateBounded<Entry>(
        new BoundedChannelOptions(50_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private static int _writerStarted;

    private static ILogger? _logger;

    /// <summary>
    /// Writes an event of a player.
    /// </summary>
    /// <param name="kind">The kind of the event, e.g. "login".</param>
    /// <param name="player">The player.</param>
    /// <param name="data">The additional data, serialized as JSON.</param>
    /// <param name="character">The character, when the player doesn't have it anymore, e.g. after leaving the world.</param>
    public static void Write(string kind, Player player, object? data = null, Character? character = null)
    {
        try
        {
            EnsureWriter(player.GameContext);
            var account = player.Account;
            character ??= player.SelectedCharacter;
            var state = account is not null && character is not null ? CharacterTracker.Read(account, character) : null;
            Entries.Writer.TryWrite(new Entry(
                DateTime.UtcNow,
                kind,
                account?.GetId(),
                account?.LoginName,
                character?.GetId(),
                character?.Name,
                account?.IsBot is true,
                state?.Level,
                state?.Resets,
                player.CurrentMap?.MapId,
                data is null ? null : JsonSerializer.Serialize(data)));
        }
        catch (Exception ex)
        {
            // It's called from the plugin points, whose proxies don't catch: the registro must never break the game.
            _logger?.LogWarning(ex, "Couldn't queue the {Kind} event of the registro.", kind);
        }
    }

    /// <summary>
    /// Writes an event which isn't about a single player, e.g. the start of a mini game.
    /// </summary>
    /// <param name="kind">The kind of the event.</param>
    /// <param name="gameContext">The game context.</param>
    /// <param name="data">The additional data, serialized as JSON.</param>
    public static void Write(string kind, IGameContext gameContext, object? data)
    {
        try
        {
            EnsureWriter(gameContext);
            Entries.Writer.TryWrite(new Entry(DateTime.UtcNow, kind, null, null, null, null, false, null, null, null, data is null ? null : JsonSerializer.Serialize(data)));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Couldn't queue the {Kind} event of the registro.", kind);
        }
    }

    private static void EnsureWriter(IGameContext gameContext)
    {
        if (Interlocked.Exchange(ref _writerStarted, 1) == 1)
        {
            return;
        }

        _logger = gameContext.LoggerFactory.CreateLogger(typeof(ActivityLog).FullName!);

        // The loop runs for good, so it doesn't keep the flow of the player whose event started it.
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(WriteLoopAsync);
        }
    }

    private static async Task WriteLoopAsync()
    {
        var batch = new List<Entry>(MaximumBatchSize);
        while (await Entries.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            batch.Clear();
            while (batch.Count < MaximumBatchSize && Entries.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            try
            {
                await InsertAsync(batch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Couldn't write {Count} entries of the registro; they are lost.", batch.Count);

                // Don't hammer a database which is down.
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask InsertAsync(List<Entry> batch)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var importer = await connection.BeginBinaryImportAsync(
            "copy tbt.event (at, kind, account_id, account, character_id, character, is_bot, level, resets, map, data) from stdin (format binary)")
            .ConfigureAwait(false);
        foreach (var entry in batch)
        {
            await importer.StartRowAsync().ConfigureAwait(false);
            await importer.WriteAsync(entry.At, NpgsqlDbType.TimestampTz).ConfigureAwait(false);
            await importer.WriteAsync(entry.Kind, NpgsqlDbType.Text).ConfigureAwait(false);
            await WriteAsync(importer, entry.AccountId).ConfigureAwait(false);
            await WriteAsync(importer, entry.Account, NpgsqlDbType.Text).ConfigureAwait(false);
            await WriteAsync(importer, entry.CharacterId).ConfigureAwait(false);
            await WriteAsync(importer, entry.Character, NpgsqlDbType.Text).ConfigureAwait(false);
            await importer.WriteAsync(entry.IsBot, NpgsqlDbType.Boolean).ConfigureAwait(false);
            await WriteAsync(importer, entry.Level).ConfigureAwait(false);
            await WriteAsync(importer, entry.Resets).ConfigureAwait(false);
            await WriteAsync(importer, entry.Map).ConfigureAwait(false);
            await WriteAsync(importer, entry.Data, NpgsqlDbType.Jsonb).ConfigureAwait(false);
        }

        await importer.CompleteAsync().ConfigureAwait(false);
    }

    private static Task WriteAsync(NpgsqlBinaryImporter importer, Guid? value)
        => value is { } guid ? importer.WriteAsync(guid, NpgsqlDbType.Uuid) : importer.WriteNullAsync();

    private static Task WriteAsync(NpgsqlBinaryImporter importer, int? value)
        => value is { } number ? importer.WriteAsync(number, NpgsqlDbType.Integer) : importer.WriteNullAsync();

    private static Task WriteAsync(NpgsqlBinaryImporter importer, string? value, NpgsqlDbType type)
        => value is not null ? importer.WriteAsync(value, type) : importer.WriteNullAsync();

    private sealed record Entry(
        DateTime At,
        string Kind,
        Guid? AccountId,
        string? Account,
        Guid? CharacterId,
        string? Character,
        bool IsBot,
        int? Level,
        int? Resets,
        int? Map,
        string? Data);
}
