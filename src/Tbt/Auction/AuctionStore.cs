// <copyright file="AuctionStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using Npgsql;
using NpgsqlTypes;

/// <summary>
/// The items which Otto keeps (<c>tbt.auction_listing</c>), the zen of the sales which their sellers
/// didn't take yet (<c>tbt.auction_earnings</c>), and the takings of that zen which aren't settled
/// yet (<c>tbt.auction_claim</c>).
/// </summary>
/// <remarks>
/// <para>
/// An item for sale is a row of <c>tbt.auction_listing</c>, never an item of OpenMU. The items of
/// the inventory are saved by OpenMU with the player, so a step which moves one is written first
/// as an operation of the row (its <c>op_</c> columns) and settled after the save, like the
/// operations of the Banqueira: a listing (state <see cref="Listing"/>) waits for the save which
/// removes its item, a delivery (state <see cref="Delivering"/>) for the save which adds the new one.
/// </para>
/// <para>
/// Each change of state is one statement, so it happens as a whole, also with the jewels of the
/// buyer and the zen or jewels of the seller. The condition on the state lets only one of two
/// buyers have an item.
/// </para>
/// </remarks>
internal static class AuctionStore
{
    /// <summary>The state of an item whose listing waits for the save which removes it from the inventory.</summary>
    internal const string Listing = "listing";

    /// <summary>The state of an item for sale, or whose sale ended (<c>expires_at</c>) and which waits for its seller.</summary>
    internal const string Active = "active";

    /// <summary>The state of an item which goes to an inventory, until the save which adds it.</summary>
    internal const string Delivering = "delivering";

    /// <summary>The state of an item which was sold or taken back.</summary>
    internal const string Done = "done";

    /// <summary>The operation of a listing.</summary>
    internal const string ListKind = "list";

    /// <summary>The operation of a sale: the item goes to the buyer.</summary>
    internal const string BuyKind = "buy";

    /// <summary>The operation of a return: the item goes back to its seller.</summary>
    internal const string ReturnKind = "return";

    private const string BeginListingSql = """
        insert into tbt.auction_listing (seller_account_id, seller_character, item, item_bytes, item_name, category, currency, price,
            state, expires_at, op_kind, op_session, op_account_id, op_character, op_item_id, op_at,
            item_type, excellent, ancient, sockets, item_level)
        select $1, $2, $3, $4, $5, $6, $7, $8, 'listing', now() + make_interval(days => $9), 'list', $10, $1, $2, $11, now(),
            $13, $14, $15, $16, $17
        where (select count(*) from tbt.auction_listing where seller_account_id = $1 and state <> 'done') < $12
        returning id
        """;

    private const string ActivateSql = """
        update tbt.auction_listing
        set state = 'active', expires_at = now() + (expires_at - listed_at), listed_at = now(),
            op_kind = null, op_session = null, op_account_id = null, op_character = null, op_item_id = null, op_at = null
        where id = $1 and state = 'listing'
        """;

    private const string BeginSaleSql = """
        with l as (
            select id, currency, price from tbt.auction_listing
            where id = $1 and state = 'active' and expires_at > now() and seller_account_id <> $2 and currency = $5 and price = $6
            for update),
        debit as (
            update tbt.jewel_bank b set amount = b.amount - l.price, updated_at = now()
            from l
            where l.currency <> 'zen' and b.account_id = $2 and b.jewel = l.currency and b.amount >= l.price
            returning 1),
        u as (
            update tbt.auction_listing a
            set state = 'delivering', op_kind = 'buy', op_session = $3, op_account_id = $2, op_character = $7, op_item_id = $4, op_at = now()
            from l
            where a.id = l.id and (l.currency = 'zen' or exists (select 1 from debit))
            returning a.id)
        select count(*)::integer from u
        """;

    private const string BeginReturnSql = """
        update tbt.auction_listing
        set state = 'delivering', op_kind = 'return', op_session = $3, op_account_id = $2, op_character = $5, op_item_id = $4, op_at = now()
        where id = $1 and state = 'active' and seller_account_id = $2 and (expires_at <= now()) = $6
        """;

    private const string CompleteSaleSql = """
        with l as (
            update tbt.auction_listing
            set state = 'done', outcome = 'sold', buyer_account_id = op_account_id, buyer_character = op_character, closed_at = now()
            where id = $1 and state = 'delivering' and op_kind = 'buy'
            returning seller_account_id, currency, price),
        z as (
            insert into tbt.auction_earnings (account_id, zen)
            select seller_account_id, price from l where currency = 'zen'
            on conflict (account_id) do update set zen = tbt.auction_earnings.zen + excluded.zen, updated_at = now()
            returning 1),
        j as (
            insert into tbt.jewel_bank (account_id, jewel, amount)
            select seller_account_id, currency, price from l where currency <> 'zen'
            on conflict (account_id, jewel) do update set amount = tbt.jewel_bank.amount + excluded.amount, updated_at = now()
            returning 1)
        select count(*)::integer from l
        """;

    private const string CompleteReturnSql = """
        update tbt.auction_listing set state = 'done', outcome = 'returned', closed_at = now()
        where id = $1 and state = 'delivering' and op_kind = 'return'
        """;

    private const string RevertDeliverySql = """
        with o as (
            select id, op_kind, op_account_id, currency, price from tbt.auction_listing
            where id = $1 and state = 'delivering'
            for update),
        l as (
            update tbt.auction_listing a
            set state = 'active', op_kind = null, op_session = null, op_account_id = null, op_character = null, op_item_id = null, op_at = null
            from o
            where a.id = o.id
            returning o.op_kind, o.op_account_id, o.currency, o.price),
        refund as (
            insert into tbt.jewel_bank (account_id, jewel, amount)
            select op_account_id, currency, price from l where op_kind = 'buy' and currency <> 'zen'
            on conflict (account_id, jewel) do update set amount = tbt.jewel_bank.amount + excluded.amount, updated_at = now()
            returning 1)
        select count(*)::integer from l
        """;

    private const string ListingColumns = """
        id, seller_account_id, seller_character, item::text, item_bytes, item_name, currency, price, state,
        greatest(-2147483648, least(2147483647, extract(epoch from expires_at - now())))::integer
        """;

    private const string BeginClaimSql = """
        with d as (
            update tbt.auction_earnings set zen = zen - $3, updated_at = now()
            where account_id = $2 and zen >= $3
            returning 1)
        insert into tbt.auction_claim (id, account_id, session, zen, inventory_id, money_after)
        select $1, $2, $4, $3, $5, $6 from d
        """;

    private const string RefundClaimsSql = """
        with c as (delete from tbt.auction_claim where id = any($1) returning account_id, zen)
        insert into tbt.auction_earnings (account_id, zen)
        select account_id, sum(zen) from c group by account_id
        on conflict (account_id) do update set zen = tbt.auction_earnings.zen + excluded.zen, updated_at = now()
        """;

    /// <summary>
    /// Writes an item which is put up for sale, before it leaves the inventory.
    /// </summary>
    /// <param name="listing">The listing.</param>
    /// <param name="durationDays">How many days it's for sale, from the save which removes it.</param>
    /// <param name="maximumListings">How many items the account may have with Otto.</param>
    /// <returns>The identifier; <c>null</c>, if the account has as many items with Otto as it may.</returns>
    public static async ValueTask<long?> BeginListingAsync(NewListing listing, int durationDays, int maximumListings)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(BeginListingSql, connection);
        var p = command.Parameters;
        p.AddWithValue(listing.SellerAccountId);
        p.AddWithValue(listing.SellerCharacter);
        p.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = listing.Item.ToJson() });
        p.AddWithValue(listing.ItemBytes);
        p.AddWithValue(listing.ItemName);
        p.AddWithValue((short)listing.Category);
        p.AddWithValue(listing.Currency);
        p.AddWithValue(listing.Price);
        p.AddWithValue(durationDays);
        p.AddWithValue(listing.Session);
        p.AddWithValue(listing.ItemId);
        p.AddWithValue((long)maximumListings);
        p.AddWithValue(listing.ItemType);
        p.AddWithValue(listing.Excellent);
        p.AddWithValue(listing.Ancient);
        p.AddWithValue(listing.Sockets);
        p.AddWithValue(listing.Level);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is long id ? id : null;
    }

    /// <summary>
    /// Puts an item up for sale whose listing was saved: its days start now.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The task.</returns>
    public static ValueTask ActivateAsync(long id) => ExecuteAsync(ActivateSql, id);

    /// <summary>
    /// Forgets a listing whose item stayed in the inventory.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The task.</returns>
    public static ValueTask ForgetListingAsync(long id) => ExecuteAsync("delete from tbt.auction_listing where id = $1 and state = 'listing'", id);

    /// <summary>
    /// Forgets the listing of an item of a stay whose writing failed, if it was written all the same; the item stayed in the inventory.
    /// </summary>
    /// <param name="session">The stay of the seller in the world.</param>
    /// <param name="itemId">The identifier of the item in the inventory.</param>
    /// <returns>The task.</returns>
    public static async ValueTask ForgetListingOfItemAsync(Guid session, Guid itemId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("delete from tbt.auction_listing where op_session = $1 and op_item_id = $2 and state = 'listing'", connection);
        command.Parameters.AddWithValue(session);
        command.Parameters.AddWithValue(itemId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the sale of an item to a buyer, before the item enters the inventory: takes the jewels
    /// of a price in jewels from the account of the buyer at the Banqueira.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="buyer">The account of the buyer, which mustn't be the seller.</param>
    /// <param name="buyerCharacter">The character of the buyer.</param>
    /// <param name="session">The stay of the buyer in the world.</param>
    /// <param name="itemId">The identifier of the new item.</param>
    /// <param name="currency">The currency which the buyer saw.</param>
    /// <param name="price">The price which the buyer saw.</param>
    /// <returns><c>true</c>, if the item was for sale at that price and the buyer had the jewels.</returns>
    public static async ValueTask<bool> BeginSaleAsync(long id, Guid buyer, string buyerCharacter, Guid session, Guid itemId, string currency, long price)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(BeginSaleSql, connection);
        var p = command.Parameters;
        p.AddWithValue(id);
        p.AddWithValue(buyer);
        p.AddWithValue(session);
        p.AddWithValue(itemId);
        p.AddWithValue(currency);
        p.AddWithValue(price);
        p.AddWithValue(buyerCharacter);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is 1;
    }

    /// <summary>
    /// Starts the return of an item to its seller, before the item enters the inventory.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="seller">The account of the seller.</param>
    /// <param name="sellerCharacter">The character which takes it back.</param>
    /// <param name="session">The stay of the seller in the world.</param>
    /// <param name="itemId">The identifier of the new item.</param>
    /// <param name="ended">Whether its sale ended, as the seller saw it: that decides the fee.</param>
    /// <returns><c>true</c>, if the item was with Otto, its sale ended or not as the seller saw it.</returns>
    public static async ValueTask<bool> BeginReturnAsync(long id, Guid seller, string sellerCharacter, Guid session, Guid itemId, bool ended)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(BeginReturnSql, connection);
        var p = command.Parameters;
        p.AddWithValue(id);
        p.AddWithValue(seller);
        p.AddWithValue(session);
        p.AddWithValue(itemId);
        p.AddWithValue(sellerCharacter);
        p.AddWithValue(ended);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Ends a delivery whose item was saved in the inventory: a sale gives its price to the seller,
    /// zen to <c>tbt.auction_earnings</c> and jewels to the Banqueira.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="kind">The operation: <see cref="BuyKind"/> or <see cref="ReturnKind"/>.</param>
    /// <returns><c>true</c>, if it was waiting.</returns>
    public static async ValueTask<bool> CompleteDeliveryAsync(long id, string kind)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        if (kind == BuyKind)
        {
            await using var sale = new NpgsqlCommand(CompleteSaleSql, connection);
            sale.Parameters.AddWithValue(id);
            return await sale.ExecuteScalarAsync().ConfigureAwait(false) is 1;
        }

        await using var command = new NpgsqlCommand(CompleteReturnSql, connection);
        command.Parameters.AddWithValue(id);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Undoes a delivery whose item didn't reach the inventory: the item is with Otto again, and a
    /// buyer gets the jewels of the price back.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The task.</returns>
    public static async ValueTask RevertDeliveryAsync(long id)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(RevertDeliverySql, connection);
        command.Parameters.AddWithValue(id);
        await command.ExecuteScalarAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an item which Otto keeps.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The listing; <c>null</c>, if there's none.</returns>
    public static async ValueTask<StoredListing?> GetAsync(long id)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"select {ListingColumns} from tbt.auction_listing where id = $1", connection);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        return await reader.ReadAsync().ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Gets a page of the items for sale, in the order of the search.
    /// </summary>
    /// <param name="search">What the window looks for.</param>
    /// <param name="page">The page, from 0.</param>
    /// <param name="pageSize">The items per page.</param>
    /// <returns>The items of the page, and how many are for sale with the filter.</returns>
    public static async ValueTask<(List<StoredListing> Listings, int Total)> BrowseAsync(AuctionSearch search, int page, int pageSize)
    {
        const string Filter = """
            state = 'active' and expires_at > now() and ($1 = 0 or category = $1) and ($2::text is null or currency = $2)
            and ($3::integer[] is null or item_type = any($3)) and ($4 <> 1 or excellent) and ($4 <> 2 or ancient)
            and ($4 <> 3 or sockets > 0) and item_level >= $5
            """;
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        int total;
        await using (var count = new NpgsqlCommand($"select count(*)::integer from tbt.auction_listing where {Filter}", connection))
        {
            AddFilter(count.Parameters, search);
            total = (int)(await count.ExecuteScalarAsync().ConfigureAwait(false) ?? 0);
        }

        var order = search.Sort switch
        {
            AuctionSort.PriceLow => "currency, price, id desc",
            AuctionSort.PriceHigh => "currency, price desc, id desc",
            AuctionSort.EndingSoon => "expires_at, id desc",
            _ => "id desc",
        };
        await using var command = new NpgsqlCommand($"select {ListingColumns} from tbt.auction_listing where {Filter} order by {order} offset $6 limit $7", connection);
        AddFilter(command.Parameters, search);
        command.Parameters.AddWithValue((long)page * pageSize);
        command.Parameters.AddWithValue((long)pageSize);
        return (await ReadAllAsync(command).ConfigureAwait(false), total);
    }

    /// <summary>
    /// Gets the items which Otto keeps for an account: for sale, whose sale ended, or on their way.
    /// </summary>
    /// <param name="accountId">The account of the seller.</param>
    /// <param name="limit">How many at most.</param>
    /// <returns>The items, the newest first.</returns>
    public static async ValueTask<List<StoredListing>> GetOfSellerAsync(Guid accountId, int limit)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"select {ListingColumns} from tbt.auction_listing where seller_account_id = $1 and state <> 'done' order by id desc limit $2", connection);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue((long)limit);
        return await ReadAllAsync(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the operations of an account which aren't settled yet: its listings which wait for the
    /// save which removes their item, and the deliveries to it which wait for the save which adds it.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The operations, with whether their item is in the saved data of OpenMU.</returns>
    public static async ValueTask<List<PendingOperation>> GetPendingAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select a.id, a.op_kind, a.op_session, exists (select 1 from data."Item" i where i."Id" = a.op_item_id)
            from tbt.auction_listing a
            where a.op_account_id = $1 and a.state in ('listing', 'delivering')
            order by a.op_at
            """,
            connection);
        command.Parameters.AddWithValue(accountId);
        var operations = new List<PendingOperation>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            operations.Add(new PendingOperation(reader.GetInt64(0), reader.GetString(1), reader.GetGuid(2), reader.GetBoolean(3)));
        }

        return operations;
    }

    /// <summary>
    /// Gets the zen of the sales of an account which its seller didn't take yet.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The zen.</returns>
    public static async ValueTask<long> GetEarningsAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select zen from tbt.auction_earnings where account_id = $1", connection);
        command.Parameters.AddWithValue(accountId);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is long zen ? zen : 0;
    }

    /// <summary>
    /// Takes zen of the sales from the account and writes the taking, before the zen enters the inventory.
    /// </summary>
    /// <param name="claim">The taking.</param>
    /// <returns><c>true</c>, if the account had the zen.</returns>
    public static async ValueTask<bool> BeginClaimAsync(ZenClaim claim)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(BeginClaimSql, connection);
        var p = command.Parameters;
        p.AddWithValue(claim.Id);
        p.AddWithValue(claim.AccountId);
        p.AddWithValue(claim.Zen);
        p.AddWithValue(claim.Session);
        p.AddWithValue(claim.InventoryId);
        p.AddWithValue(claim.MoneyAfter);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Forgets takings of zen whose zen was saved in the inventory.
    /// </summary>
    /// <param name="ids">The identifiers.</param>
    /// <returns>The task.</returns>
    public static ValueTask ForgetClaimsAsync(IReadOnlyCollection<Guid> ids) => ExecuteClaimsAsync("delete from tbt.auction_claim where id = any($1)", ids);

    /// <summary>
    /// Gives the zen of takings which didn't reach the inventory back to their accounts, and forgets them.
    /// </summary>
    /// <param name="ids">The identifiers.</param>
    /// <returns>The task.</returns>
    public static ValueTask RefundClaimsAsync(IReadOnlyCollection<Guid> ids) => ExecuteClaimsAsync(RefundClaimsSql, ids);

    /// <summary>
    /// Gets the takings of zen of an account which aren't settled yet.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The takings, with the zen of their inventory in the saved data of OpenMU (<c>null</c>, if it's gone).</returns>
    public static async ValueTask<List<(ZenClaim Claim, long? SavedMoney)>> GetPendingClaimsAsync(Guid accountId)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select c.id, c.session, c.zen, c.inventory_id, c.money_after, s."Money"
            from tbt.auction_claim c left join data."ItemStorage" s on s."Id" = c.inventory_id
            where c.account_id = $1
            order by c.at
            """,
            connection);
        command.Parameters.AddWithValue(accountId);
        var claims = new List<(ZenClaim, long?)>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var claim = new ZenClaim(reader.GetGuid(0), accountId, reader.GetGuid(1), reader.GetInt64(2), reader.GetGuid(3), reader.GetInt64(4));
            claims.Add((claim, await reader.IsDBNullAsync(5).ConfigureAwait(false) ? null : reader.GetInt32(5)));
        }

        return claims;
    }

    private static void AddFilter(NpgsqlParameterCollection parameters, AuctionSearch search)
    {
        parameters.AddWithValue((short)search.Category);
        parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)search.Currency ?? DBNull.Value });
        parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer, Value = (object?)search.ItemTypes ?? DBNull.Value });
        parameters.AddWithValue((short)search.Quality);
        parameters.AddWithValue(search.MinimumLevel);
    }

    private static async ValueTask<List<StoredListing>> ReadAllAsync(NpgsqlCommand command)
    {
        var listings = new List<StoredListing>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            listings.Add(Read(reader));
        }

        return listings;
    }

    private static StoredListing Read(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        (byte[])reader.GetValue(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetInt64(7),
        reader.GetString(8),
        reader.GetInt32(9));

    private static async ValueTask ExecuteAsync(string sql, long id)
    {
        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async ValueTask ExecuteClaimsAsync(string sql, IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await using var connection = await TbtDatabase.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(ids.ToArray());
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
