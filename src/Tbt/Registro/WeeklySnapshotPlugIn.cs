// <copyright file="WeeklySnapshotPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// The weekly snapshot of the registro (PLANO-SERVIDOR.md, section 8): level, resets, the stored
/// jewels and the items +10 and above of every human character, and the vault of every human
/// account, in <c>tbt.weekly_snapshot</c>. The Chaos Machine has no plugin point, so what happens
/// there shows in the difference between two snapshots.
/// </summary>
/// <remarks>
/// The week turns on Monday at 02:00 UTC, which is Sunday at 23:00 in Brasília, when the weekly ranking
/// closes (5.4). The snapshot of a week is taken at the first check of the week, so a server which
/// was down at the turn takes it when it's back.
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Foto semanal", Description = "Takes a weekly snapshot of the level, resets, jewels and +10 items of the human characters and vaults into tbt.weekly_snapshot.")]
[Guid("E5A3C918-4F62-4D7B-9B1E-8C2A6F0D3E75")]
public class WeeklySnapshotPlugIn : IPeriodicTaskPlugIn
{
    // Jewels count one each, and the packed jewels ten per level (+0 is 10, +1 is 20 and +2 is 30).
    private const string SnapshotSql = """
        with storages as (
            select c."InventoryId" as storage_id from data."Character" c join data."Account" a on a."Id" = c."AccountId" where not a."IsBot" and not a."IsTemplate"
            union all
            select a."VaultId" from data."Account" a where not a."IsBot" and not a."IsTemplate" and a."VaultId" is not null),
        counts as (
            select i."ItemStorageId" as storage_id,
                sum(case when (d."Group", d."Number") = (14, 13) then 1 when (d."Group", d."Number") = (12, 30) then 10 * (i."Level" + 1) else 0 end) as bless,
                sum(case when (d."Group", d."Number") = (14, 14) then 1 when (d."Group", d."Number") = (12, 31) then 10 * (i."Level" + 1) else 0 end) as soul,
                sum(case when (d."Group", d."Number") = (12, 15) then 1 when (d."Group", d."Number") = (12, 141) then 10 * (i."Level" + 1) else 0 end) as chaos,
                sum(case when (d."Group", d."Number") = (14, 16) then 1 when (d."Group", d."Number") = (12, 136) then 10 * (i."Level" + 1) else 0 end) as life,
                sum(case when (d."Group", d."Number") = (14, 22) then 1 when (d."Group", d."Number") = (12, 137) then 10 * (i."Level" + 1) else 0 end) as creation,
                count(*) filter (where d."Group" <= 12 and i."Level" = 10) as plus10,
                count(*) filter (where d."Group" <= 12 and i."Level" = 11) as plus11,
                count(*) filter (where d."Group" <= 12 and i."Level" = 12) as plus12,
                count(*) filter (where d."Group" <= 12 and i."Level" = 13) as plus13,
                count(*) filter (where d."Group" <= 12 and i."Level" >= 14) as plus14_15
            from data."Item" i
            join config."ItemDefinition" d on d."Id" = i."DefinitionId"
            where i."ItemStorageId" in (select storage_id from storages)
            group by i."ItemStorageId"),
        rows as (
            select a."Id" as account_id, a."LoginName" as account, c."Id" as character_id, c."Name" as character,
                (select s."Value" from data."StatAttribute" s join config."AttributeDefinition" ad on ad."Id" = s."DefinitionId" where s."CharacterId" = c."Id" and ad."Designation" = 'Level') as level,
                (select s."Value" from data."StatAttribute" s join config."AttributeDefinition" ad on ad."Id" = s."DefinitionId" where s."CharacterId" = c."Id" and ad."Designation" = 'Resets') as resets,
                c."InventoryId" as storage_id
            from data."Character" c join data."Account" a on a."Id" = c."AccountId"
            where not a."IsBot" and not a."IsTemplate"
            union all
            select a."Id", a."LoginName", null, null, null, null, a."VaultId"
            from data."Account" a
            where not a."IsBot" and not a."IsTemplate" and a."VaultId" is not null)
        insert into tbt.weekly_snapshot (week, account_id, account, character_id, character, level, resets,
            bless, soul, chaos, life, creation, plus10, plus11, plus12, plus13, plus14_15)
        select $1, r.account_id, r.account, r.character_id, r.character, r.level, r.resets,
            coalesce(n.bless, 0), coalesce(n.soul, 0), coalesce(n.chaos, 0), coalesce(n.life, 0), coalesce(n.creation, 0),
            coalesce(n.plus10, 0), coalesce(n.plus11, 0), coalesce(n.plus12, 0), coalesce(n.plus13, 0), coalesce(n.plus14_15, 0)
        from rows r
        left join counts n on n.storage_id = r.storage_id
        where not exists (select 1 from tbt.weekly_snapshot w where w.week = $1)
        """;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private DateTime _nextCheckUtc = DateTime.UtcNow;

    private int _running;

    /// <summary>
    /// Gets the time of the week turn after the start of the Monday in UTC.
    /// </summary>
    internal static TimeSpan WeekTurnOffset { get; } = TimeSpan.FromHours(2);

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (DateTime.UtcNow < this._nextCheckUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            this._nextCheckUtc = DateTime.UtcNow + CheckInterval;
            var week = DateOnly.FromDateTime(GetWeekStart(DateTime.UtcNow));
            await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand(SnapshotSql, connection) { CommandTimeout = 300 };
            command.Parameters.AddWithValue(week);
            var rows = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (rows > 0)
            {
                gameContext.LoggerFactory.CreateLogger(this.GetType().Name)
                    .LogInformation("Took the weekly snapshot of {Week} with {Rows} rows.", week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), rows);
            }
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger(this.GetType().Name).LogWarning(ex, "Couldn't take the weekly snapshot.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextCheckUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the Monday of the week of the specified time, where the week turns on Monday at 02:00 UTC.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The Monday which starts the week.</returns>
    internal static DateTime GetWeekStart(DateTime utc)
    {
        var shifted = utc - WeekTurnOffset;
        var daysSinceMonday = ((int)shifted.DayOfWeek + 6) % 7;
        return shifted.Date.AddDays(-daysSinceMonday);
    }
}
