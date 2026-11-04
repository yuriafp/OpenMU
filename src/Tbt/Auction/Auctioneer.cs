// <copyright file="Auctioneer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Tbt.JewelBank;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.Registro;
using MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// What Otto, o Leiloeiro, does: he keeps the items which the players put up for sale, at a price in
/// zen or in jewels, and gives them to their buyers, or back to their sellers. Runs under the
/// persistence lock of the player, like the packet handlers.
/// </summary>
/// <remarks>
/// <para>
/// An item must never be both with Otto and in an inventory, nor in neither. Each step which moves
/// one is written first in <see cref="AuctionStore"/> and settled after the save of the player: a
/// listing, when the save removed its item; a delivery, when the save added the new item. When the
/// save fails, or the server stops in between, the saved data of OpenMU tells what happened.
/// </para>
/// <para>
/// That holds only while the player didn't change the item since; so an operation of an earlier
/// stay in the world is settled at the entry, before the player can use the item, and an operation
/// of the current stay only when its outcome is known. The zen of the fees and of a price in zen
/// is in the inventory, which the same save keeps; the jewels of a price in jewels are at the
/// Banqueira, and change with the state of the item, in one statement.
/// </para>
/// <para>
/// The zen of the sales waits with Otto (<c>tbt.auction_earnings</c>). Taking it is written first,
/// too, with the zen which the inventory has with it: the saved inventory tells whether it was saved.
/// </para>
/// </remarks>
internal static class Auctioneer
{
    /// <summary>The message when the database fails.</summary>
    internal const string Unavailable = "O Otto não pode atender agora. Tente de novo mais tarde.";

    private static readonly ConditionalWeakTable<Player, Session> Sessions = new();

    private static readonly TimeSpan SettleRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How often a stay tries again to save the deliveries whose save failed.
    /// </summary>
    private static readonly TimeSpan UnsavedDeliverySaveInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How a waiting operation is settled.
    /// </summary>
    internal enum Settlement
    {
        /// <summary>It waits: its outcome isn't known yet.</summary>
        Wait,

        /// <summary>It happened: the listing's item is for sale, the delivery's item is in the inventory.</summary>
        Complete,

        /// <summary>It didn't: the listing's item stayed in the inventory, the delivery's item is with Otto again.</summary>
        Undo,
    }

    /// <summary>
    /// Starts a new stay of the player in the world, and settles the operations of the earlier ones.
    /// </summary>
    /// <param name="player">The player, which just entered the world.</param>
    /// <returns><c>true</c>, if nothing of an earlier stay waits anymore.</returns>
    public static async ValueTask<bool> EnterAsync(Player player)
    {
        Sessions.AddOrUpdate(player, new Session());
        return await SettleWithRetriesAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the stay of the player in the world: settles the operations whose outcome it knows.
    /// </summary>
    /// <param name="player">The player, which leaves the world.</param>
    /// <returns><c>true</c>, if they're settled.</returns>
    public static async ValueTask<bool> LeaveAsync(Player player)
    {
        if (!Sessions.TryGetValue(player, out var session)
            || session.Outcomes.Count + session.ClaimOutcomes.Count + session.ListingsToForget.Count + session.UnsavedDeliveries.Count == 0)
        {
            return true;
        }

        return await SettleWithRetriesAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the window of Otto, with the first page of the items for sale.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns><c>true</c>, if the window opened.</returns>
    public static async ValueTask<bool> OpenAsync(Player player, AuctionConfiguration configuration)
    {
        if (player.Account is not { } account)
        {
            return false;
        }

        await SettleAsync(player, account.GetId()).ConfigureAwait(false);
        return await SendBrowseAsync(player, account.GetId(), configuration, AuctionSearch.All, 0, open: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a request of the window of Otto.
    /// </summary>
    /// <param name="player">The player, which talks to Otto.</param>
    /// <param name="request">The request.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The task.</returns>
    public static async ValueTask HandleAsync(Player player, AuctionRequest request, AuctionConfiguration configuration)
    {
        if (player.Account is not { } account || player.Inventory is not { } inventory || player.SelectedCharacter is not { } character)
        {
            return;
        }

        var accountId = account.GetId();
        var settled = await SettleAsync(player, accountId).ConfigureAwait(false);
        switch (request.Action)
        {
            case AuctionAction.Browse:
                await SendBrowseAsync(player, accountId, configuration, SearchOf(request), (int)Math.Clamp(request.Value, 0, ushort.MaxValue), open: false).ConfigureAwait(false);
                return;
            case AuctionAction.Mine:
                await SendMineAsync(player, accountId, configuration).ConfigureAwait(false);
                return;
            default:
                break;
        }

        // While an operation of this stay waits for its outcome, its item is in the inventory or not
        // as the next save decides; another step could move that item, and the saved data would
        // then settle the operation wrongly. The next save settles it, at the latest the one of the exit.
        if (!settled || SessionOf(player).Waiting > 0)
        {
            await player.ShowMessageAsync("O Otto ainda confere o seu último pedido. Tente de novo em instantes.").ConfigureAwait(false);
            await SendResultAsync(player, request.Action, false).ConfigureAwait(false);
            return;
        }

        // A mix or a trade restores the backup of the inventory when it's canceled.
        if (player.BackupInventory is not null)
        {
            await player.ShowMessageAsync("Feche a troca ou o mix antes de usar o Otto.").ConfigureAwait(false);
            await SendResultAsync(player, request.Action, false).ConfigureAwait(false);
            return;
        }

        var done = request.Action switch
        {
            AuctionAction.List => await ListAsync(player, accountId, character, inventory, request.Argument1, request.Argument2, request.Price, configuration).ConfigureAwait(false),
            AuctionAction.Buy => await BuyAsync(player, accountId, character, request.Value, request.Argument2, request.Price, configuration).ConfigureAwait(false),
            AuctionAction.Return => await ReturnAsync(player, accountId, character, request.Value, request.Argument1 != 0, configuration).ConfigureAwait(false),
            AuctionAction.ClaimZen => await ClaimZenAsync(player, accountId, character).ConfigureAwait(false),
            _ => false,
        };

        await SendResultAsync(player, request.Action, done).ConfigureAwait(false);
    }

    /// <summary>
    /// Decides how a waiting operation is settled.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="session">The current stay of the player.</param>
    /// <param name="known">The outcome which the current stay knows: <c>true</c> completes it, <c>false</c> undoes it.</param>
    /// <returns>How it's settled.</returns>
    internal static Settlement Decide(PendingOperation operation, Guid session, bool? known)
    {
        if (known is { } outcome)
        {
            return outcome ? Settlement.Complete : Settlement.Undo;
        }

        var isListing = operation.Kind == AuctionStore.ListKind;
        if (operation.Session == session)
        {
            // A listing whose save failed: a later save removes its item. A delivery of this stay
            // always knows its outcome, unless its item couldn't be saved nor taken back.
            return isListing && !operation.ItemSaved ? Settlement.Complete : Settlement.Wait;
        }

        if (isListing)
        {
            // Saved, when its item is gone; else it came back with the inventory.
            return operation.ItemSaved ? Settlement.Undo : Settlement.Complete;
        }

        // Saved, when its new item is there; else it never reached the inventory.
        return operation.ItemSaved ? Settlement.Complete : Settlement.Undo;
    }

    /// <summary>
    /// Decides how a waiting taking of zen is settled.
    /// </summary>
    /// <param name="claim">The taking.</param>
    /// <param name="savedMoney">The zen of its inventory in the saved data; <c>null</c>, if the inventory is gone.</param>
    /// <param name="session">The current stay of the player.</param>
    /// <param name="known">The outcome which the current stay knows: <c>true</c> forgets it, <c>false</c> gives the zen back.</param>
    /// <returns><see cref="Settlement.Complete"/>, if the zen was saved in the inventory.</returns>
    internal static Settlement DecideClaim(ZenClaim claim, long? savedMoney, Guid session, bool? known)
    {
        if (known is { } outcome)
        {
            return outcome ? Settlement.Complete : Settlement.Undo;
        }

        if (claim.Session == session)
        {
            return Settlement.Wait;
        }

        // The stay which took it saved right after, and forgot the taking; only a stop of the server
        // in between leaves it. So the saved zen is the zen with it, or the zen before it.
        return savedMoney == claim.MoneyAfter ? Settlement.Complete : Settlement.Undo;
    }

    /// <summary>
    /// Gets whether Otto takes an item of the inventory: one of the bag, which can be traded, not of the starter kit.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The reason why not, for the player; <c>null</c>, if he takes it.</returns>
    internal static string? RefusalOf(Item item)
    {
        if (item.ItemSlot < InventoryConstants.EquippableSlotsCount || item.ItemSlot >= InventoryConstants.FirstStoreItemSlotIndex)
        {
            return "O Otto só aceita itens da bolsa do inventário.";
        }

        if (item.Definition is not { IsTradable: true, IsBoundToCharacter: false })
        {
            return "Esse item não pode ser negociado.";
        }

        return StarterKitStore.IsKitItem(item) ? "Itens do kit inicial não vão a leilão." : null;
    }

    /// <summary>
    /// Gets what a request of the window looks for: anything it doesn't know is "all".
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The search.</returns>
    internal static AuctionSearch SearchOf(AuctionRequest request)
    {
        var category = (AuctionCategory)request.Argument1;
        var currency = request.Argument2 == AuctionCurrency.Any ? null : AuctionCurrency.NameOf(request.Argument2);
        return new AuctionSearch(
            Enum.IsDefined(category) ? category : AuctionCategory.All,
            currency,
            request.Quality,
            request.ItemTypes);
    }

    /// <summary>
    /// Answers a request which failed with a result which says so, so the window of the client unlocks
    /// at once. A page answers the requests for one, so they get none.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="action">The action of the request.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask RefuseAsync(Player player, AuctionAction action)
    {
        if (action is AuctionAction.Browse or AuctionAction.Mine)
        {
            return;
        }

        try
        {
            await SendResultAsync(player, action, false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Couldn't send the result of a failed request of {Character} at Otto.", player.Name);
        }
    }

    /// <summary>
    /// Gets the highest price of a currency.
    /// </summary>
    /// <param name="currency">The name of the currency.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The highest price.</returns>
    internal static long MaximumPriceOf(string currency, GameConfiguration gameConfiguration, AuctionConfiguration configuration)
        => currency == AuctionCurrency.ZenName ? gameConfiguration.MaximumInventoryMoney : configuration.MaximumJewelPrice;

    private static async ValueTask<bool> ListAsync(Player player, Guid accountId, Character character, IStorage inventory, byte slot, byte currencyCode, uint price, AuctionConfiguration configuration)
    {
        if (inventory.GetItem(slot) is not { } item)
        {
            await player.ShowMessageAsync("Escolha um item da bolsa.").ConfigureAwait(false);
            return false;
        }

        if (RefusalOf(item) is { } refusal)
        {
            await player.ShowMessageAsync(refusal).ConfigureAwait(false);
            return false;
        }

        if (AuctionCurrency.NameOf(currencyCode) is not { } currency
            || price == 0
            || price > MaximumPriceOf(currency, player.GameContext.Configuration, configuration))
        {
            await player.ShowMessageAsync(currencyCode == AuctionCurrency.Zen
                ? $"O preço em zen vai de 1 a {player.GameContext.Configuration.MaximumInventoryMoney:N0}."
                : $"O preço em joias vai de 1 a {configuration.MaximumJewelPrice:N0}.").ConfigureAwait(false);
            return false;
        }

        if (player.Money < configuration.ListingFee)
        {
            await player.ShowMessageAsync($"A taxa para anunciar é de {configuration.ListingFee:N0} zen.").ConfigureAwait(false);
            return false;
        }

        var bytes = SerializeForClient(player, item);
        if (AuctionItem.Of(item, player.GameContext.Configuration) is not { } stored || bytes is null)
        {
            await player.ShowMessageAsync("O Otto não aceita esse item.").ConfigureAwait(false);
            return false;
        }

        var session = SessionOf(player);
        var itemName = $"{item.Definition!.Name} +{item.Level}";
        var listing = new NewListing(
            accountId,
            character.Name,
            stored,
            bytes,
            itemName,
            AuctionCategories.Of(item),
            currency,
            price,
            session.Id,
            item.GetId(),
            (item.Definition.Group * AuctionSearch.ItemsPerGroup) + item.Definition.Number,
            item.IsExcellent(),
            item.IsAncient(),
            (short)item.SocketCount,
            item.Level);
        long? id;
        try
        {
            id = await AuctionStore.BeginListingAsync(listing, configuration.DurationDays, configuration.MaximumListingsPerAccount).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The listing may have been written all the same: it's forgotten by its item, which stays.
            player.Logger.LogWarning(ex, "Couldn't write the listing of {Character} at Otto.", player.Name);
            await ForgetListingOfItemAsync(player, session, listing.ItemId).ConfigureAwait(false);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        if (id is not { } listingId)
        {
            await player.ShowMessageAsync($"Você já tem {configuration.MaximumListingsPerAccount} itens com o Otto. Retire ou venda algum antes.").ConfigureAwait(false);
            return false;
        }

        // The money may have changed while the listing was written.
        if (!player.TryRemoveMoney(configuration.ListingFee))
        {
            await SettleKnownAsync(player, session, listingId, AuctionStore.ListKind, complete: false).ConfigureAwait(false);
            await player.ShowMessageAsync($"A taxa para anunciar é de {configuration.ListingFee:N0} zen.").ConfigureAwait(false);
            return false;
        }

        try
        {
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't remove the item of the listing {Listing} of {Character} at Otto.", listingId, player.Name);
            if (inventory.Items.Contains(item))
            {
                // It stayed: the listing never happened.
                player.TryAddMoney(configuration.ListingFee);
                await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                await SettleKnownAsync(player, session, listingId, AuctionStore.ListKind, complete: false).ConfigureAwait(false);
                await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
                return false;
            }

            // It left the inventory: the saved data settles the listing.
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        ActivityLog.Write("auction", player, new { action = "list", id = listingId, item = itemName, currency, price, zen = configuration.ListingFee });
        if (!await SaveAsync(player).ConfigureAwait(false))
        {
            // The next save of the player settles it, at the latest the one of its exit.
            await player.ShowMessageAsync("Você entregou o item ao Otto. Ele aparece no leilão assim que o personagem for salvo.").ConfigureAwait(false);
            return true;
        }

        await SettleKnownAsync(player, session, listingId, AuctionStore.ListKind, complete: true).ConfigureAwait(false);
        await player.ShowMessageAsync($"{itemName} está à venda por {AuctionCurrency.Describe(currency, price)} durante {configuration.DurationDays} dias.").ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> BuyAsync(Player player, Guid accountId, Character character, long listingId, byte currencyCode, uint price, AuctionConfiguration configuration)
    {
        var listing = await GetListingAsync(player, listingId).ConfigureAwait(false);
        if (listing is null || listing.State != AuctionStore.Active || listing.Ended
            || AuctionCurrency.NameOf(currencyCode) != listing.Currency || listing.Price != price)
        {
            await player.ShowMessageAsync("Esse item não está mais à venda.").ConfigureAwait(false);
            return false;
        }

        if (listing.SellerAccountId == accountId)
        {
            await player.ShowMessageAsync("Esse item é seu. Para tê-lo de volta, retire o anúncio.").ConfigureAwait(false);
            return false;
        }

        var isZen = listing.Currency == AuctionCurrency.ZenName;
        var zen = configuration.BuyerFee + (isZen ? listing.Price : 0);
        if (zen > player.Money)
        {
            await player.ShowMessageAsync(isZen
                ? $"Você precisa de {zen:N0} zen: o preço e a taxa de {configuration.BuyerFee:N0}."
                : $"A taxa de compra é de {configuration.BuyerFee:N0} zen.").ConfigureAwait(false);
            return false;
        }

        if (AuctionCurrency.JewelOf(listing.Currency) is { } jewel)
        {
            var kept = await GetKeptJewelsAsync(player, accountId, jewel).ConfigureAwait(false);
            if (kept is null)
            {
                return false;
            }

            if (kept < listing.Price)
            {
                await player.ShowMessageAsync($"O pagamento sai das joias guardadas na Ella: você tem {AuctionCurrency.Describe(listing.Currency, kept.Value)} lá.").ConfigureAwait(false);
                return false;
            }
        }

        if (await CreateItemAsync(player, listing).ConfigureAwait(false) is not { } item)
        {
            return false;
        }

        var session = SessionOf(player);
        bool began;
        try
        {
            began = await AuctionStore.BeginSaleAsync(listing.Id, accountId, character.Name, session.Id, item.GetId(), listing.Currency, listing.Price).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't write the purchase of {Character} at Otto.", player.Name);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        if (!began)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync("Esse item não está mais à venda, ou faltam joias na Ella.").ConfigureAwait(false);
            return false;
        }

        if (!await DeliverAsync(player, session, listing, item, AuctionStore.BuyKind, (int)zen).ConfigureAwait(false))
        {
            return false;
        }

        ActivityLog.Write("auction", player, new { action = "buy", id = listing.Id, item = listing.ItemName, seller = listing.SellerCharacter, currency = listing.Currency, price = listing.Price, zen });
        await player.ShowMessageAsync($"Você comprou {listing.ItemName} por {AuctionCurrency.Describe(listing.Currency, listing.Price)} (taxa de {configuration.BuyerFee:N0} zen).").ConfigureAwait(false);
        await NotifySellerAsync(player, listing).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> ReturnAsync(Player player, Guid accountId, Character character, long listingId, bool ended, AuctionConfiguration configuration)
    {
        var listing = await GetListingAsync(player, listingId).ConfigureAwait(false);
        if (listing is null || listing.SellerAccountId != accountId || listing.State != AuctionStore.Active)
        {
            await player.ShowMessageAsync("Esse item não está com o Otto.").ConfigureAwait(false);
            return false;
        }

        // The fee as the player saw it in the window; the store checks it still holds.
        var zen = listing.Ended ? 0 : configuration.CancelFee;
        if (listing.Ended != ended)
        {
            await player.ShowMessageAsync("O prazo desse anúncio acabou de mudar. Confira e tente de novo.").ConfigureAwait(false);
            return false;
        }

        if (zen > player.Money)
        {
            await player.ShowMessageAsync($"A taxa para retirar um item ainda à venda é de {zen:N0} zen.").ConfigureAwait(false);
            return false;
        }

        if (await CreateItemAsync(player, listing).ConfigureAwait(false) is not { } item)
        {
            return false;
        }

        var session = SessionOf(player);
        bool began;
        try
        {
            began = await AuctionStore.BeginReturnAsync(listing.Id, accountId, character.Name, session.Id, item.GetId(), listing.Ended).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't write the return of {Character} at Otto.", player.Name);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        if (!began)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync("Esse item não está mais com o Otto, ou o prazo dele mudou. Confira e tente de novo.").ConfigureAwait(false);
            return false;
        }

        if (!await DeliverAsync(player, session, listing, item, AuctionStore.ReturnKind, zen).ConfigureAwait(false))
        {
            return false;
        }

        ActivityLog.Write("auction", player, new { action = "return", id = listing.Id, item = listing.ItemName, ended = listing.Ended, zen });
        await player.ShowMessageAsync(zen == 0
            ? $"O Otto devolveu {listing.ItemName}."
            : $"O Otto devolveu {listing.ItemName} (taxa de {zen:N0} zen).").ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Takes the zen and gives the item of a delivery which was written, and settles it.
    /// </summary>
    /// <returns><c>true</c>, if the item is in the inventory.</returns>
    private static async ValueTask<bool> DeliverAsync(Player player, Session session, StoredListing listing, Item item, string kind, int zen)
    {
        // The money may have changed while the delivery was written.
        if (!player.TryRemoveMoney(zen))
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await SettleKnownAsync(player, session, listing.Id, kind, complete: false).ConfigureAwait(false);
            await player.ShowMessageAsync($"Faltou zen: são {zen:N0}.").ConfigureAwait(false);
            return false;
        }

        bool given;
        try
        {
            // Adds and saves the item, or takes it back and deletes it.
            given = await RewardDelivery.TryGiveAsync(player, [item]).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't give the item of the listing {Listing} to {Character}.", listing.Id, player.Name);
            given = false;
        }

        if (!given && player.Inventory?.Items.Contains(item) is true)
        {
            // The item couldn't be taken back. Once it's saved, the delivery happened; until then,
            // the saved data settles it at the next entry.
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            if (await SaveAsync(player).ConfigureAwait(false))
            {
                await SettleKnownAsync(player, session, listing.Id, kind, complete: true).ConfigureAwait(false);
                return true;
            }

            // The next settling of the stay saves again; else the next entry settles it by the saved data.
            session.UnsavedDeliveries.Add(listing.Id);
            player.Logger.LogError("The delivery of the listing {Listing} to {Character} waits for the next entry.", listing.Id, player.Name);
            await player.ShowMessageAsync("O Otto confere a entrega no seu próximo pedido, ou na sua próxima entrada no jogo.").ConfigureAwait(false);
            return false;
        }

        if (!given)
        {
            // Not saved: the zen goes back, and the item is with Otto again.
            player.TryAddMoney(zen);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await SettleKnownAsync(player, session, listing.Id, kind, complete: false).ConfigureAwait(false);
            await player.ShowMessageAsync("O Otto não conseguiu entregar o item. Nada foi cobrado.").ConfigureAwait(false);
            return false;
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        await SettleKnownAsync(player, session, listing.Id, kind, complete: true).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> ClaimZenAsync(Player player, Guid accountId, Character character)
    {
        long earnings;
        try
        {
            earnings = await AuctionStore.GetEarningsAsync(accountId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the earnings of {Character} at Otto.", player.Name);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        var room = (long)player.GameContext.Configuration.MaximumInventoryMoney - player.Money;
        var zen = (int)Math.Clamp(Math.Min(earnings, room), 0, int.MaxValue);
        if (earnings == 0)
        {
            await player.ShowMessageAsync("Você não tem zen de vendas para receber.").ConfigureAwait(false);
            return false;
        }

        if (zen == 0 || character.Inventory is not { } inventoryStorage)
        {
            await player.ShowMessageAsync("Você não tem espaço para mais zen no inventário.").ConfigureAwait(false);
            return false;
        }

        // Saved first, so the saved zen is the zen of the inventory now, never the zen with the taking.
        if (!await SaveAsync(player).ConfigureAwait(false))
        {
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        var session = SessionOf(player);
        var claim = new ZenClaim(Guid.NewGuid(), accountId, session.Id, zen, inventoryStorage.GetId(), (long)player.Money + zen);
        bool began;
        try
        {
            began = await AuctionStore.BeginClaimAsync(claim).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't write the taking of zen of {Character} at Otto.", player.Name);
            began = false;
        }

        if (!began)
        {
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        if (!player.TryAddMoney(zen))
        {
            await SettleClaimKnownAsync(player, session, claim.Id, complete: false).ConfigureAwait(false);
            await player.ShowMessageAsync("Você não tem espaço para mais zen no inventário.").ConfigureAwait(false);
            return false;
        }

        if (!await SaveAsync(player).ConfigureAwait(false))
        {
            // Not saved: the zen leaves the inventory again, so no later save keeps it, and goes back to Otto.
            player.TryRemoveMoney(zen);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await SettleClaimKnownAsync(player, session, claim.Id, complete: false).ConfigureAwait(false);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        await SettleClaimKnownAsync(player, session, claim.Id, complete: true).ConfigureAwait(false);
        ActivityLog.Write("auction", player, new { action = "claim", zen });
        await player.ShowMessageAsync(zen < earnings
            ? $"Você recebeu {zen:N0} zen das vendas. Faltam {earnings - zen:N0}, quando houver espaço."
            : $"Você recebeu {zen:N0} zen das vendas.").ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<StoredListing?> GetListingAsync(Player player, long id)
    {
        try
        {
            return await AuctionStore.GetAsync(id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the listing {Listing} for {Character} at Otto.", id, player.Name);
            return null;
        }
    }

    private static async ValueTask<long?> GetKeptJewelsAsync(Player player, Guid accountId, BankJewel jewel)
    {
        try
        {
            return (await JewelBankStore.GetJewelsAsync(accountId).ConfigureAwait(false)).GetValueOrDefault(jewel);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the jewels of {Character} for Otto.", player.Name);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Creates the item of a listing in the persistence context of the player, when it fits into the inventory.
    /// </summary>
    private static async ValueTask<Item?> CreateItemAsync(Player player, StoredListing listing)
    {
        Item? item;
        try
        {
            item = AuctionItem.FromJson(listing.ItemJson).Create(player.PersistenceContext, player.GameContext.Configuration);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't read the item of the listing {Listing} at Otto.", listing.Id);
            item = null;
        }

        if (item is null)
        {
            player.Logger.LogError("The item of the listing {Listing} at Otto doesn't match the configuration.", listing.Id);
            await player.ShowMessageAsync("O Otto não encontra esse item agora. Avise o administrador.").ConfigureAwait(false);
            return null;
        }

        if (player.Inventory?.CheckInvSpace(item) is null)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync("Abra espaço no inventário para receber o item.").ConfigureAwait(false);
            return null;
        }

        return item;
    }

    private static async ValueTask NotifySellerAsync(Player buyer, StoredListing listing)
    {
        try
        {
            if (buyer.GameContext.GetPlayerByCharacterName(listing.SellerCharacter) is { } seller && seller.Account?.GetId() == listing.SellerAccountId)
            {
                var where = listing.Currency == AuctionCurrency.ZenName ? "Receba o zen com o Otto." : "As joias estão na Ella.";
                await seller.ShowMessageAsync($"O Otto vendeu {listing.ItemName} por {AuctionCurrency.Describe(listing.Currency, listing.Price)}. {where}").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            buyer.Logger.LogDebug(ex, "Couldn't tell the seller of the listing {Listing} about the sale.", listing.Id);
        }
    }

    private static byte[]? SerializeForClient(Player player, Item item)
    {
        if (player.ViewPlugIns.GetPlugIn<IItemSerializer>() is not { } serializer)
        {
            return null;
        }

        Span<byte> buffer = stackalloc byte[Math.Max(serializer.NeededSpace, AuctionContentsWriter.ItemLength)];
        buffer.Clear();
        var length = serializer.SerializeItem(buffer, item);
        return length is > 0 and <= AuctionContentsWriter.ItemLength ? buffer[..length].ToArray() : null;
    }

    private static async ValueTask<bool> SendBrowseAsync(Player player, Guid accountId, AuctionConfiguration configuration, AuctionSearch search, int page, bool open)
    {
        if (player.GetConnection() is not { } connection)
        {
            return false;
        }

        var category = search.Category;
        var currencyCode = search.Currency is null ? AuctionCurrency.Any : AuctionCurrency.CodeOf(search.Currency) ?? AuctionCurrency.Any;

        AuctionPage contents;
        try
        {
            var pageSize = Math.Clamp(configuration.PageSize, 1, AuctionContentsWriter.MaximumEntries);
            var (listings, total) = await AuctionStore.BrowseAsync(search, page, pageSize).ConfigureAwait(false);
            var pages = Math.Max(1, (total + pageSize - 1) / pageSize);
            if (page >= pages && page > 0)
            {
                page = pages - 1;
                (listings, total) = await AuctionStore.BrowseAsync(search, page, pageSize).ConfigureAwait(false);
            }

            var mine = (await AuctionStore.GetOfSellerAsync(accountId, AuctionContentsWriter.MaximumEntries).ConfigureAwait(false)).Count;
            var earnings = await AuctionStore.GetEarningsAsync(accountId).ConfigureAwait(false);
            contents = new AuctionPage(AuctionView.Browse, open, category, currencyCode, page, pages, mine, earnings, listings, accountId);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the items for sale for {Character} at Otto.", player.Name);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return false;
        }

        await SendAsync(connection, contents, configuration).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendMineAsync(Player player, Guid accountId, AuctionConfiguration configuration)
    {
        if (player.GetConnection() is not { } connection)
        {
            return;
        }

        AuctionPage contents;
        try
        {
            var listings = await AuctionStore.GetOfSellerAsync(accountId, AuctionContentsWriter.MaximumEntries).ConfigureAwait(false);
            var earnings = await AuctionStore.GetEarningsAsync(accountId).ConfigureAwait(false);
            contents = new AuctionPage(AuctionView.Mine, false, AuctionCategory.All, AuctionCurrency.Any, 0, 1, listings.Count, earnings, listings, accountId);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the items of {Character} at Otto.", player.Name);
            await player.ShowMessageAsync(Unavailable).ConfigureAwait(false);
            return;
        }

        await SendAsync(connection, contents, configuration).ConfigureAwait(false);
    }

    private static async ValueTask SendAsync(IConnection connection, AuctionPage contents, AuctionConfiguration configuration)
    {
        var length = AuctionContentsWriter.LengthOf(contents.Listings.Count);
        await connection.SendAsync(() => AuctionContentsWriter.Write(connection.Output.GetSpan(length)[..length], contents, configuration)).ConfigureAwait(false);
    }

    private static async ValueTask SendResultAsync(Player player, AuctionAction action, bool done)
    {
        if (player.GetConnection() is not { } connection)
        {
            return;
        }

        await connection.SendAsync(() => AuctionContentsWriter.WriteResult(connection.Output.GetSpan(AuctionContentsWriter.ResultLength)[..AuctionContentsWriter.ResultLength], action, done)).ConfigureAwait(false);
    }

    /// <summary>
    /// Settles the operations of the account which wait: those whose outcome this stay knows, and
    /// those of earlier stays, by the saved data.
    /// </summary>
    /// <returns><c>true</c>, if all which could be settled is.</returns>
    private static async ValueTask<bool> SettleAsync(Player player, Guid accountId)
    {
        var session = SessionOf(player);
        session.Waiting = 0;
        try
        {
            foreach (var itemId in session.ListingsToForget.ToList())
            {
                await AuctionStore.ForgetListingOfItemAsync(session.Id, itemId).ConfigureAwait(false);
                session.ListingsToForget.Remove(itemId);
            }

            // A delivery whose item is with the player but wasn't saved happened, once a save keeps it.
            // Not during a trade or a mix, whose inventory isn't final; and once in a while, as the
            // window asks for pages often.
            if (session.UnsavedDeliveries.Count > 0
                && player.BackupInventory is null
                && DateTime.UtcNow - session.LastDeliverySave >= UnsavedDeliverySaveInterval)
            {
                session.LastDeliverySave = DateTime.UtcNow;
                if (await SaveAsync(player).ConfigureAwait(false))
                {
                    foreach (var id in session.UnsavedDeliveries)
                    {
                        session.Outcomes[id] = true;
                    }

                    session.UnsavedDeliveries.Clear();
                }
            }

            var pending = await AuctionStore.GetPendingAsync(accountId).ConfigureAwait(false);

            // An outcome whose operation doesn't wait anymore was settled already, maybe by a statement
            // which failed only after its commit; kept, it could settle a later operation of the same listing.
            foreach (var id in session.Outcomes.Keys.Where(id => pending.All(o => o.Id != id)).ToList())
            {
                session.Outcomes.Remove(id);
            }

            foreach (var operation in pending)
            {
                bool? known = session.Outcomes.TryGetValue(operation.Id, out var outcome) ? outcome : null;
                var settlement = Decide(operation, session.Id, known);
                if (settlement != Settlement.Wait)
                {
                    await ApplyAsync(operation.Id, operation.Kind, settlement == Settlement.Complete).ConfigureAwait(false);
                    session.Outcomes.Remove(operation.Id);
                    player.Logger.LogInformation("Otto: settled the {Kind} of the listing {Listing} of {Character}: {Settlement}.", operation.Kind, operation.Id, player.Name, settlement);
                }
                else
                {
                    session.Waiting++;
                }
            }

            var claims = await AuctionStore.GetPendingClaimsAsync(accountId).ConfigureAwait(false);
            foreach (var id in session.ClaimOutcomes.Keys.Where(id => claims.All(c => c.Claim.Id != id)).ToList())
            {
                session.ClaimOutcomes.Remove(id);
            }

            foreach (var (claim, savedMoney) in claims)
            {
                bool? known = session.ClaimOutcomes.TryGetValue(claim.Id, out var outcome) ? outcome : null;
                var settlement = DecideClaim(claim, savedMoney, session.Id, known);
                if (settlement == Settlement.Complete)
                {
                    await AuctionStore.ForgetClaimsAsync([claim.Id]).ConfigureAwait(false);
                }
                else if (settlement == Settlement.Undo)
                {
                    await AuctionStore.RefundClaimsAsync([claim.Id]).ConfigureAwait(false);
                }

                if (settlement != Settlement.Wait)
                {
                    session.ClaimOutcomes.Remove(claim.Id);
                    player.Logger.LogInformation("Otto: settled the taking of {Zen} zen of {Character}: {Settlement}.", claim.Zen, player.Name, settlement);
                }
                else
                {
                    session.Waiting++;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't settle the waiting operations of {Character} at Otto.", player.Name);
            return false;
        }
    }

    private static async ValueTask ApplyAsync(long id, string kind, bool complete)
    {
        switch (kind, complete)
        {
            case (AuctionStore.ListKind, true):
                await AuctionStore.ActivateAsync(id).ConfigureAwait(false);
                break;
            case (AuctionStore.ListKind, false):
                await AuctionStore.ForgetListingAsync(id).ConfigureAwait(false);
                break;
            case (_, true):
                await AuctionStore.CompleteDeliveryAsync(id, kind).ConfigureAwait(false);
                break;
            default:
                await AuctionStore.RevertDeliveryAsync(id).ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask<bool> SettleWithRetriesAsync(Player player)
    {
        if (player.Account is not { } account)
        {
            return true;
        }

        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            if (await SettleAsync(player, account.GetId()).ConfigureAwait(false))
            {
                return true;
            }

            if (attempt == attempts)
            {
                return false;
            }

            await Task.Delay(SettleRetryDelay).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Settles an operation of this stay whose outcome is known. When that fails, the outcome is
    /// kept, and the next request of the stay settles it.
    /// </summary>
    private static async ValueTask SettleKnownAsync(Player player, Session session, long id, string kind, bool complete)
    {
        try
        {
            await ApplyAsync(id, kind, complete).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't settle the listing {Listing} of {Character} at Otto; the next request tries again.", id, player.Name);
            session.Outcomes[id] = complete;
        }
    }

    private static async ValueTask SettleClaimKnownAsync(Player player, Session session, Guid id, bool complete)
    {
        try
        {
            if (complete)
            {
                await AuctionStore.ForgetClaimsAsync([id]).ConfigureAwait(false);
            }
            else
            {
                await AuctionStore.RefundClaimsAsync([id]).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't settle a taking of zen of {Character} at Otto; the next request tries again.", player.Name);
            session.ClaimOutcomes[id] = complete;
        }
    }

    /// <summary>
    /// Forgets a listing whose writing failed, by its item, which stayed in the inventory. When that
    /// fails too, the next request of the stay tries again, and nothing else of Otto happens before.
    /// </summary>
    private static async ValueTask ForgetListingOfItemAsync(Player player, Session session, Guid itemId)
    {
        try
        {
            await AuctionStore.ForgetListingOfItemAsync(session.Id, itemId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't forget a listing of {Character} at Otto; the next request tries again.", player.Name);
            session.ListingsToForget.Add(itemId);
        }
    }

    private static async ValueTask<bool> SaveAsync(Player player)
    {
        try
        {
            return await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't save {Character} at Otto.", player.Name);
            return false;
        }
    }

    private static Session SessionOf(Player player) => Sessions.GetValue(player, _ => new Session());

    /// <summary>
    /// A stay of the player in the world.
    /// </summary>
    private sealed class Session
    {
        /// <summary>
        /// Gets the identifier, which the operations of the stay carry.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the known outcomes of operations of the stay which couldn't be settled yet, by listing:
        /// <c>true</c> completes them, <c>false</c> undoes them.
        /// </summary>
        public Dictionary<long, bool> Outcomes { get; } = new();

        /// <summary>
        /// Gets the known outcomes of takings of zen of the stay which couldn't be settled yet:
        /// <c>true</c> forgets them, <c>false</c> gives their zen back.
        /// </summary>
        public Dictionary<Guid, bool> ClaimOutcomes { get; } = new();

        /// <summary>
        /// Gets the items whose listing failed to be written and may be there all the same; forgotten before anything else.
        /// </summary>
        public HashSet<Guid> ListingsToForget { get; } = new();

        /// <summary>
        /// Gets the deliveries of the stay whose item is with the player, but whose save failed; a later save completes them.
        /// </summary>
        public HashSet<long> UnsavedDeliveries { get; } = new();

        /// <summary>
        /// Gets or sets when a save for <see cref="UnsavedDeliveries"/> was last tried.
        /// </summary>
        public DateTime LastDeliverySave { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets how many operations of the account waited for their outcome at the last settling.
        /// </summary>
        public int Waiting { get; set; }
    }
}
