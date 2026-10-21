// <copyright file="TbtDatabase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Threading;
using MUnique.OpenMU.Persistence.EntityFramework;
using Npgsql;

/// <summary>
/// The connection to our own schema <c>tbt</c> in the OpenMU database. It's outside of the data model
/// of OpenMU, so it survives the updates of OpenMU, and OpenMU never touches it.
/// </summary>
internal static class TbtDatabase
{
    private const string SchemaSql = """
        create schema if not exists tbt;

        create table if not exists tbt.event (
            id bigint generated always as identity primary key,
            at timestamptz not null default now(),
            kind text not null,
            account_id uuid,
            account text,
            character_id uuid,
            character text,
            is_bot boolean not null default false,
            level integer,
            resets integer,
            map integer,
            data jsonb);
        create index if not exists event_kind_at_idx on tbt.event (kind, at);
        create index if not exists event_account_at_idx on tbt.event (account_id, at);

        create table if not exists tbt.character_state (
            character_id uuid primary key,
            account_id uuid not null,
            name text not null,
            is_bot boolean not null,
            level integer not null,
            resets integer not null,
            updated_at timestamptz not null default now());
        create index if not exists character_state_account_idx on tbt.character_state (account_id);
        alter table tbt.character_state add column if not exists resets_at_entry integer;

        create table if not exists tbt.milestone (
            account_id uuid not null,
            milestone text not null,
            character text not null,
            at timestamptz not null default now(),
            primary key (account_id, milestone));

        create table if not exists tbt.server_first (
            first text primary key,
            account text not null,
            character text not null,
            at timestamptz not null default now());

        create table if not exists tbt.weekly_snapshot (
            week date not null,
            taken_at timestamptz not null default now(),
            account_id uuid not null,
            account text not null,
            character_id uuid,
            character text,
            level integer,
            resets integer,
            bless integer not null,
            soul integer not null,
            chaos integer not null,
            life integer not null,
            creation integer not null,
            plus10 integer not null,
            plus11 integer not null,
            plus12 integer not null,
            plus13 integer not null,
            plus14_15 integer not null);
        create index if not exists weekly_snapshot_week_idx on tbt.weekly_snapshot (week);
        """;

    /// <summary>
    /// The data source; a failed creation, e.g. without the connection settings, is tried again on the next use.
    /// </summary>
    private static readonly Lazy<NpgsqlDataSource> DataSource = new(CreateDataSource, LazyThreadSafetyMode.PublicationOnly);

    private static readonly Lock SchemaLock = new();

    private static Task? _schemaCreation;

    /// <summary>
    /// Opens a connection, after making sure that the schema exists.
    /// </summary>
    /// <returns>The opened connection.</returns>
    public static async ValueTask<NpgsqlConnection> OpenAsync()
    {
        await EnsureSchemaAsync().ConfigureAwait(false);
        return await DataSource.Value.OpenConnectionAsync().ConfigureAwait(false);
    }

    private static Task EnsureSchemaAsync()
    {
        lock (SchemaLock)
        {
            // A failed creation, e.g. while the database is still starting, is tried again on the next use.
            if (_schemaCreation is null || _schemaCreation.IsFaulted || _schemaCreation.IsCanceled)
            {
                _schemaCreation = CreateSchemaAsync();
            }

            return _schemaCreation;
        }
    }

    private static async Task CreateSchemaAsync()
    {
        await using var connection = await DataSource.Value.OpenConnectionAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(SchemaSql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static NpgsqlDataSource CreateDataSource()
    {
        // The admin connection of OpenMU's ConnectionSettings.xml, with the environment variables
        // (DB_HOST, DB_ADMIN_USER, DB_ADMIN_PW) applied, like OpenMU itself does.
        var setting = new ConfigFileDatabaseConnectionStringProvider().GetConnectionSetting(typeof(EntityDataContext));
        var builder = new NpgsqlConnectionStringBuilder(setting.ConnectionString ?? throw new InvalidOperationException("No connection string for the admin connection."))
        {
            // Short, because the entry into the world waits for the registro and the milestones:
            // a stalled database delays it only briefly. The weekly snapshot sets its own timeout.
            Timeout = 5,
            CommandTimeout = 5,
        };

        return NpgsqlDataSource.Create(builder.ConnectionString);
    }
}
