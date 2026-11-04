// <copyright file="JewelBankStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using Npgsql;
using NpgsqlTypes;

/// <summary>
/// The jewels which each account keeps with the Banqueira (<c>tbt.jewel_bank</c>), and the operations
/// whose items aren't settled yet (<c>tbt.jewel_bank_operation</c>).
/// </summary>
/// <remarks>
/// The items live in the data of OpenMU, which the player saves with its own context, and the jewels in
/// our schema. An operation is written before the items change, and settled after the save of the
/// player: a deposit adds its jewels to the account, a withdrawal was taken from it when it was
/// written. Each change of the jewels and its operation is one statement, so it happens as a whole.
/// </remarks>
internal static class JewelBankStore
{
    /// <summary>The kind of an operation which keeps jewels.</summary>
    internal const string Deposit = "deposit";

    /// <summary>The kind of an operation which takes out jewels.</summary>
    internal const string Withdrawal = "withdrawal";

    /// <summary>
    /// Adds the jewels of the operations to their accounts and forgets them: a saved deposit, or a
    /// withdrawal whose items the player didn't get.
    /// </summary>
    private const string CreditSql = """
        with settled as (delete from tbt.jewel_bank_operation where id = any($1) returning account_id, jewel, amount)
        insert into tbt.jewel_bank (account_id, jewel, amount)
        select account_id, jewel, sum(amount) from settled group by account_id, jewel
        on conflict (account_id, jewel) do update set amount = tbt.jewel_bank.amount + excluded.amount, updated_at = now()
        """;

    /// <summary>
    /// Takes the jewels of a withdrawal from the account and writes its operation, only when the account
    /// keeps enough of them.
    /// </summary>
    private const string WithdrawSql = """
        with debit as (
            update tbt.jewel_bank set amount = amount - $5, updated_at = now()
            where account_id = $2 and jewel = $4 and amount >= $5
            returning 1)
        insert into tbt.jewel_bank_operation (id, account_id, session, kind, jewel, amount, fee, item_ids)
        select $1, $2, $3, 'withdrawal', $4, $5, $6, $7 from debit
        """;

    /// <summary>
    /// Gets the jewels which the account keeps.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The amounts, by jewel; a jewel which the account never kept is missing.</returns>
    public static async ValueTask<Dictionary<BankJewel, long>> GetJewelsAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select jewel, amount from tbt.jewel_bank where account_id = $1", connection);
        command.Parameters.AddWithValue(accountId);
        var jewels = new Dictionary<BankJewel, long>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            // A jewel which a later version renamed stays in the table, untouched.
            if (BankJewel.ByName(reader.GetString(0)) is { } jewel)
            {
                jewels[jewel] = reader.GetInt64(1);
            }
        }

        return jewels;
    }

    /// <summary>
    /// Writes the operations of a deposit, before its items leave the inventory.
    /// </summary>
    /// <param name="operations">The operations, one per jewel.</param>
    /// <returns>The task.</returns>
    public static async ValueTask BeginDepositAsync(IReadOnlyList<JewelBankOperation> operations)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(connection);
        foreach (var operation in operations)
        {
            var command = new NpgsqlBatchCommand(
                "insert into tbt.jewel_bank_operation (id, account_id, session, kind, jewel, amount, fee, item_ids) values ($1, $2, $3, 'deposit', $4, $5, $6, $7)");
            AddParameters(command.Parameters, operation);
            batch.BatchCommands.Add(command);
        }

        // A batch runs in one transaction.
        await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the jewels of a withdrawal from the account and writes its operation, before its item enters the inventory.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns><c>true</c>, if the account kept enough jewels.</returns>
    public static async ValueTask<bool> BeginWithdrawalAsync(JewelBankOperation operation)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(WithdrawSql, connection);
        AddParameters(command.Parameters, operation);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Adds the jewels of the operations to their accounts and forgets them: deposits whose items
    /// left the inventory, or withdrawals whose items the player didn't get.
    /// </summary>
    /// <param name="operationIds">The operation identifiers.</param>
    /// <returns>The task.</returns>
    public static async ValueTask CreditAsync(IReadOnlyCollection<Guid> operationIds)
    {
        if (operationIds.Count == 0)
        {
            return;
        }

        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(CreditSql, connection);
        command.Parameters.AddWithValue(operationIds.ToArray());
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets the operations without changing the jewels: deposits whose items stayed in the
    /// inventory, or withdrawals whose items the player got.
    /// </summary>
    /// <param name="operationIds">The operation identifiers.</param>
    /// <returns>The task.</returns>
    public static async ValueTask ForgetAsync(IReadOnlyCollection<Guid> operationIds)
    {
        if (operationIds.Count == 0)
        {
            return;
        }

        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("delete from tbt.jewel_bank_operation where id = any($1)", connection);
        command.Parameters.AddWithValue(operationIds.ToArray());
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the operations of the account which aren't settled yet.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The operations, with how many of their items are in the saved data of OpenMU.</returns>
    public static async ValueTask<List<(JewelBankOperation Operation, int SavedItems)>> GetPendingAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select o.id, o.session, o.kind, o.jewel, o.amount, o.fee, o.item_ids,
                (select count(*) from data."Item" i where i."Id" = any(o.item_ids))::integer
            from tbt.jewel_bank_operation o
            where o.account_id = $1
            order by o.at
            """,
            connection);
        command.Parameters.AddWithValue(accountId);
        var operations = new List<(JewelBankOperation, int)>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var operation = new JewelBankOperation(
                reader.GetGuid(0),
                accountId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt64(5),
                await reader.GetFieldValueAsync<Guid[]>(6).ConfigureAwait(false));
            operations.Add((operation, reader.GetInt32(7)));
        }

        return operations;
    }

    private static void AddParameters(NpgsqlParameterCollection parameters, JewelBankOperation operation)
    {
        parameters.AddWithValue(operation.Id);
        parameters.AddWithValue(operation.AccountId);
        parameters.AddWithValue(operation.Session);
        parameters.AddWithValue(operation.Jewel);
        parameters.AddWithValue(operation.Amount);
        parameters.AddWithValue(operation.Fee);
        parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid, Value = operation.ItemIds });
    }
}
