// <copyright file="JewelBanker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// What the Banqueira does: she keeps the jewels and packs of the inventory for the account, and
/// gives them out again as a jewel or a pack, for a fee in zen per jewel. Runs under the persistence
/// lock of the player, like the packet handlers.
/// </summary>
/// <remarks>
/// <para>
/// The jewels and the items must never both stay or both go. An operation is written before the
/// inventory changes and settled after the save of the player (<see cref="JewelBankStore"/>). When
/// the save fails, or the server stops in between, the saved data of OpenMU tells what happened:
/// the items of a deposit are gone when it was saved, the item of a withdrawal is there.
/// </para>
/// <para>
/// That holds only while the player didn't change the items since; so an operation of an earlier
/// stay in the world is settled at the entry, before the player can use its items, and an
/// operation of the current stay only when its outcome is known. Each stay has its own
/// identifier, the session of its operations.
/// </para>
/// </remarks>
internal static class JewelBanker
{
    private static readonly ConditionalWeakTable<Player, Session> Sessions = new();

    /// <summary>
    /// How long the entry and the exit wait between their tries to settle: an operation of an
    /// earlier stay must be settled before the player can use its items.
    /// </summary>
    private static readonly TimeSpan SettleRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How a waiting operation is settled.
    /// </summary>
    internal enum Settlement
    {
        /// <summary>It waits: its outcome isn't known yet.</summary>
        Wait,

        /// <summary>Its jewels go to the account: a saved deposit, or a withdrawal whose item the player didn't get.</summary>
        Credit,

        /// <summary>It's forgotten: a deposit whose items stayed, or a withdrawal whose item the player got.</summary>
        Forget,
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
    /// Ends the stay of the player in the world: settles the operations whose outcome it knows,
    /// which the next stay couldn't tell by the saved data once the player used their items.
    /// </summary>
    /// <param name="player">The player, which leaves the world.</param>
    /// <returns><c>true</c>, if they're settled.</returns>
    public static async ValueTask<bool> LeaveAsync(Player player)
    {
        if (!Sessions.TryGetValue(player, out var session) || session.Outcomes.Count == 0)
        {
            return true;
        }

        return await SettleWithRetriesAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the window of the Banqueira, with the jewels of the account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns><c>true</c>, if the window opened.</returns>
    public static async ValueTask<bool> OpenAsync(Player player, JewelBankConfiguration configuration)
    {
        if (player.Account is not { } account)
        {
            return false;
        }

        var accountId = account.GetId();
        await SettleAsync(player, accountId).ConfigureAwait(false);
        return await SendContentsAsync(player, accountId, configuration, JewelBankContentsRef.JewelBankFlags.Open).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a request of the window of the Banqueira.
    /// </summary>
    /// <param name="player">The player, which talks to the Banqueira.</param>
    /// <param name="request">The request.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The task.</returns>
    public static async ValueTask HandleAsync(Player player, JewelBankRequest request, JewelBankConfiguration configuration)
    {
        if (player.Account is not { } account || player.Inventory is not { } inventory)
        {
            return;
        }

        // A mix or a trade restores the backup of the inventory when it's canceled.
        if (player.BackupInventory is not null)
        {
            await player.ShowMessageAsync("Feche a troca ou o mix antes de usar a Banqueira.").ConfigureAwait(false);
            return;
        }

        var accountId = account.GetId();
        await SettleAsync(player, accountId).ConfigureAwait(false);
        switch (request.Action)
        {
            case JewelBankAction.Deposit when request.JewelIndex < BankJewel.All.Count:
                await DepositAsync(player, account, inventory, [BankJewel.All[request.JewelIndex]], configuration).ConfigureAwait(false);
                break;
            case JewelBankAction.DepositAll:
                await DepositAsync(player, account, inventory, BankJewel.All, configuration).ConfigureAwait(false);
                break;
            case JewelBankAction.Withdraw when request.JewelIndex < BankJewel.All.Count:
                await WithdrawAsync(player, accountId, BankJewel.All[request.JewelIndex], request.Amount, configuration).ConfigureAwait(false);
                break;
            default:
                return;
        }

        await SendContentsAsync(player, accountId, configuration, JewelBankContentsRef.JewelBankFlags.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the contents of the window.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="JewelBankContentsRef.Length"/> bytes.</param>
    /// <param name="jewels">The jewels which the account keeps.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The length of the packet.</returns>
    internal static int WritePacket(Span<byte> target, IReadOnlyDictionary<BankJewel, long> jewels, JewelBankConfiguration configuration, JewelBankContentsRef.JewelBankFlags flags)
    {
        target = target[..JewelBankContentsRef.Length];
        target.Clear();
        var packet = new JewelBankContentsRef(target)
        {
            Flags = flags,
            JewelCount = (byte)BankJewel.All.Count,
            DepositFeePerJewel = configuration.DepositFeePerJewel,
            WithdrawalFeePerJewel = configuration.WithdrawalFeePerJewel,
        };
        for (var i = 0; i < BankJewel.All.Count; i++)
        {
            packet.SetJewel(i, BankJewel.All[i], jewels.GetValueOrDefault(BankJewel.All[i]));
        }

        return JewelBankContentsRef.Length;
    }

    /// <summary>
    /// Gets the items of the bag of the inventory which the Banqueira keeps: not the equipped ones,
    /// and not those of the personal store.
    /// </summary>
    /// <param name="inventory">The inventory.</param>
    /// <param name="jewels">The jewels to keep.</param>
    /// <returns>The items, with their jewel and amount.</returns>
    internal static List<(Item Item, BankJewel Jewel, int Amount)> GetDepositItems(IStorage inventory, IReadOnlyCollection<BankJewel> jewels)
    {
        var items = new List<(Item, BankJewel, int)>();
        foreach (var item in inventory.Items)
        {
            if (item.ItemSlot >= InventoryConstants.EquippableSlotsCount
                && item.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex
                && BankJewel.Of(item) is { } found
                && jewels.Contains(found.Jewel))
            {
                items.Add((item, found.Jewel, found.Amount));
            }
        }

        return items;
    }

    /// <summary>
    /// Decides how a waiting operation is settled.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="savedItems">How many of its items are in the saved data of OpenMU.</param>
    /// <param name="session">The current stay of the player.</param>
    /// <param name="known">The outcome which the current stay knows: <c>true</c> adds the jewels, <c>false</c> forgets them.</param>
    /// <returns>How it's settled.</returns>
    internal static Settlement Decide(JewelBankOperation operation, int savedItems, Guid session, bool? known)
    {
        if (known is { } outcome)
        {
            return outcome ? Settlement.Credit : Settlement.Forget;
        }

        var isDeposit = operation.Kind == JewelBankStore.Deposit;
        if (operation.Session == session)
        {
            // A deposit whose save failed: a later save removes its items. A withdrawal of this stay
            // always knows its outcome, unless its item couldn't be taken back; the next stay decides it.
            return isDeposit && savedItems == 0 ? Settlement.Credit : Settlement.Wait;
        }

        if (isDeposit)
        {
            // Saved, when all its items are gone; else they came back with the inventory.
            return savedItems == 0 ? Settlement.Credit : Settlement.Forget;
        }

        // Saved, when its item is there; else it never reached the inventory.
        return savedItems > 0 ? Settlement.Forget : Settlement.Credit;
    }

    private static async ValueTask DepositAsync(Player player, Account account, IStorage inventory, IReadOnlyCollection<BankJewel> jewels, JewelBankConfiguration configuration)
    {
        var items = GetDepositItems(inventory, jewels);
        if (items.Count == 0)
        {
            await player.ShowMessageAsync(jewels.Count == 1 ? "Você não tem essa joia no inventário." : "Você não tem joias no inventário.").ConfigureAwait(false);
            return;
        }

        var total = items.Sum(i => (long)i.Amount);
        var fee = total * configuration.DepositFeePerJewel;
        if (fee > player.Money)
        {
            await player.ShowMessageAsync($"Para guardar {total:N0} joias, a taxa é de {fee:N0} zen.").ConfigureAwait(false);
            return;
        }

        // One operation per item, so the saved data settles each one whole.
        var session = SessionOf(player);
        var accountId = account.GetId();
        var deposits = items
            .Select(i => new Deposit(i.Item, new JewelBankOperation(
                Guid.NewGuid(),
                accountId,
                session.Id,
                JewelBankStore.Deposit,
                i.Jewel.Name,
                i.Amount,
                (long)i.Amount * configuration.DepositFeePerJewel,
                [i.Item.GetId()])))
            .ToList();
        var operations = deposits.Select(d => d.Operation).ToList();
        try
        {
            await JewelBankStore.BeginDepositAsync(operations).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't write the deposit of {Character} at the Banqueira.", player.Name);
            await player.ShowMessageAsync("A Banqueira não pode atender agora. Tente de novo mais tarde.").ConfigureAwait(false);
            return;
        }

        // The money may have changed while the operations were written.
        if (!player.TryRemoveMoney((int)fee))
        {
            await SettleKnownAsync(player, session, operations.Select(o => o.Id), credit: false).ConfigureAwait(false);
            await player.ShowMessageAsync($"Para guardar {total:N0} joias, a taxa é de {fee:N0} zen.").ConfigureAwait(false);
            return;
        }

        // From here on, the operations are settled by the saved data, whatever happens.
        var removed = await RemoveItemsAsync(player, inventory, deposits).ConfigureAwait(false);
        var removedTotal = removed.Sum(d => (long)d.Operation.Amount);
        var removedFee = removed.Sum(d => d.Operation.Fee);
        if (removed.Count < deposits.Count)
        {
            // The items after the one which failed stayed; that one is settled by the saved data.
            player.TryAddMoney((int)(fee - removedFee));
            var untouched = deposits.Skip(removed.Count + 1).Select(d => d.Operation.Id);
            await SettleKnownAsync(player, session, untouched, credit: false).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        if (removed.Count == 0)
        {
            await player.ShowMessageAsync("A Banqueira não pode atender agora. Tente de novo mais tarde.").ConfigureAwait(false);
            return;
        }

        SessionCounters.Of(player)?.AddBankedJewels(JewelCount.Of(removed.Select(d => d.Item)));
        var loggedJewels = removed.GroupBy(d => d.Operation.Jewel).ToDictionary(g => g.Key, g => g.Sum(d => d.Operation.Amount));
        ActivityLog.Write("jewel_bank", player, new { action = JewelBankStore.Deposit, jewels = loggedJewels, zen = removedFee });

        if (!await SaveAsync(player).ConfigureAwait(false))
        {
            // The next save of the player settles it, at the latest the one of its exit.
            await player.ShowMessageAsync($"Você entregou {removedTotal:N0} joias. Elas aparecem na Banqueira assim que o personagem for salvo.").ConfigureAwait(false);
            return;
        }

        await SettleKnownAsync(player, session, removed.Select(d => d.Operation.Id), credit: true).ConfigureAwait(false);
        await player.ShowMessageAsync($"Você guardou {removedTotal:N0} joias por {removedFee:N0} zen.").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the items of a deposit from the inventory, up to the first one which fails.
    /// </summary>
    /// <returns>The deposits whose items were removed, in their order.</returns>
    private static async ValueTask<List<Deposit>> RemoveItemsAsync(Player player, IStorage inventory, List<Deposit> deposits)
    {
        var removed = new List<Deposit>();
        foreach (var deposit in deposits)
        {
            try
            {
                var slot = deposit.Item.ItemSlot;
                await inventory.RemoveItemAsync(deposit.Item).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(deposit.Item).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Couldn't remove the item of the deposit {Operation} of {Character} at the Banqueira.", deposit.Operation.Id, player.Name);
                break;
            }

            removed.Add(deposit);
        }

        return removed;
    }

    private static async ValueTask WithdrawAsync(Player player, Guid accountId, BankJewel jewel, int amount, JewelBankConfiguration configuration)
    {
        if (jewel.ItemFor(amount) is not { } target
            || player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == target.Group && d.Number == target.Number) is not { } definition)
        {
            return;
        }

        var kept = (await JewelBankStore.GetJewelsAsync(accountId).ConfigureAwait(false)).GetValueOrDefault(jewel);
        if (kept < amount)
        {
            await player.ShowMessageAsync(kept == 0 ? "Você não tem essa joia guardada." : $"Você tem só {kept:N0} dessa joia guardadas.").ConfigureAwait(false);
            return;
        }

        var fee = (long)amount * configuration.WithdrawalFeePerJewel;
        if (fee > player.Money)
        {
            await player.ShowMessageAsync($"Para retirar {amount:N0} joias, a taxa é de {fee:N0} zen.").ConfigureAwait(false);
            return;
        }

        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = target.Level;
        item.Durability = definition.Durability;
        if (player.Inventory?.CheckInvSpace(item) is null)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync("Abra espaço no inventário para retirar a joia.").ConfigureAwait(false);
            return;
        }

        var session = SessionOf(player);
        var operation = new JewelBankOperation(Guid.NewGuid(), accountId, session.Id, JewelBankStore.Withdrawal, jewel.Name, amount, fee, [item.GetId()]);
        bool began;
        try
        {
            began = await JewelBankStore.BeginWithdrawalAsync(operation).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't write the withdrawal of {Character} at the Banqueira.", player.Name);
            began = false;
        }

        if (!began)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.ShowMessageAsync("A Banqueira não pode atender agora. Tente de novo mais tarde.").ConfigureAwait(false);
            return;
        }

        // The money may have changed while the operation was written.
        if (!player.TryRemoveMoney((int)fee))
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await SettleKnownAsync(player, session, [operation.Id], credit: true).ConfigureAwait(false);
            await player.ShowMessageAsync($"Para retirar {amount:N0} joias, a taxa é de {fee:N0} zen.").ConfigureAwait(false);
            return;
        }

        bool given;
        try
        {
            // Adds and saves the item, or takes it back and deletes it.
            given = await RewardDelivery.TryGiveAsync(player, [item]).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't give the withdrawal of {Character} at the Banqueira.", player.Name);
            given = false;
        }

        if (!given && player.Inventory?.Items.Contains(item) is true)
        {
            // The item couldn't be taken back: the saved data settles the operation at the next entry.
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            player.Logger.LogWarning("The withdrawal {Operation} of {Character} at the Banqueira waits for the next entry.", operation.Id, player.Name);
            await player.ShowMessageAsync("A Banqueira confere a retirada na sua próxima entrada no jogo.").ConfigureAwait(false);
            return;
        }

        if (!given)
        {
            // Not saved: the zen and the jewels go back; the item isn't in the inventory anymore.
            player.TryAddMoney((int)fee);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await SettleKnownAsync(player, session, [operation.Id], credit: true).ConfigureAwait(false);
            await player.ShowMessageAsync("A Banqueira não conseguiu entregar a joia. Nada foi cobrado.").ConfigureAwait(false);
            return;
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        SessionCounters.Of(player)?.AddBankedJewels(-JewelCount.Of(item));
        ActivityLog.Write("jewel_bank", player, new { action = JewelBankStore.Withdrawal, jewels = new Dictionary<string, int> { [jewel.Name] = amount }, zen = fee });
        await SettleKnownAsync(player, session, [operation.Id], credit: false).ConfigureAwait(false);
        await player.ShowMessageAsync($"Você retirou {amount:N0} joias por {fee:N0} zen.").ConfigureAwait(false);
    }

    /// <summary>
    /// Settles the operations of the account which wait: those whose outcome this stay knows, and
    /// those of earlier stays, by the saved data. An operation of this stay without a known outcome
    /// is a deposit whose save failed, settled once its items are gone from the saved data.
    /// </summary>
    /// <returns><c>true</c>, if all which could be settled is.</returns>
    private static async ValueTask<bool> SettleAsync(Player player, Guid accountId)
    {
        var session = SessionOf(player);
        List<(JewelBankOperation Operation, int SavedItems)> pending;
        try
        {
            pending = await JewelBankStore.GetPendingAsync(accountId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the waiting operations of {Character} at the Banqueira.", player.Name);
            return false;
        }

        var credit = new List<Guid>();
        var forget = new List<Guid>();
        foreach (var (operation, savedItems) in pending)
        {
            bool? known = session.Outcomes.TryGetValue(operation.Id, out var outcome) ? outcome : null;
            switch (Decide(operation, savedItems, session.Id, known))
            {
                case Settlement.Credit:
                    credit.Add(operation.Id);
                    break;
                case Settlement.Forget:
                    forget.Add(operation.Id);
                    break;
                default:
                    break;
            }
        }

        if (credit.Count + forget.Count == 0)
        {
            return true;
        }

        try
        {
            await JewelBankStore.CreditAsync(credit).ConfigureAwait(false);
            await JewelBankStore.ForgetAsync(forget).ConfigureAwait(false);
            foreach (var id in credit.Concat(forget))
            {
                session.Outcomes.Remove(id);
            }

            player.Logger.LogInformation("Banqueira: settled {Credited} operations with and {Forgotten} without jewels for {Character}.", credit.Count, forget.Count, player.Name);
            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't settle the waiting operations of {Character} at the Banqueira.", player.Name);
            return false;
        }
    }

    /// <summary>
    /// Settles the operations of the account which wait, trying again a few times.
    /// </summary>
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
    /// Settles operations of this stay whose outcome is known. When that fails, the outcome is kept,
    /// and the next request of the stay settles them.
    /// </summary>
    private static async ValueTask SettleKnownAsync(Player player, Session session, IEnumerable<Guid> operationIds, bool credit)
    {
        var ids = operationIds.ToList();
        try
        {
            if (credit)
            {
                await JewelBankStore.CreditAsync(ids).ConfigureAwait(false);
            }
            else
            {
                await JewelBankStore.ForgetAsync(ids).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't settle an operation of {Character} at the Banqueira; the next request tries again.", player.Name);
            foreach (var id in ids)
            {
                session.Outcomes[id] = credit;
            }
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
            player.Logger.LogWarning(ex, "Couldn't save {Character} after a deposit at the Banqueira.", player.Name);
            return false;
        }
    }

    private static async ValueTask<bool> SendContentsAsync(Player player, Guid accountId, JewelBankConfiguration configuration, JewelBankContentsRef.JewelBankFlags flags)
    {
        if (player.GetConnection() is not { } connection)
        {
            return false;
        }

        Dictionary<BankJewel, long> jewels;
        try
        {
            jewels = await JewelBankStore.GetJewelsAsync(accountId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't read the jewels of {Character} at the Banqueira.", player.Name);
            await player.ShowMessageAsync("A Banqueira não pode atender agora. Tente de novo mais tarde.").ConfigureAwait(false);
            return false;
        }

        await connection.SendAsync(() => WritePacket(connection.Output.GetSpan(JewelBankContentsRef.Length)[..JewelBankContentsRef.Length], jewels, configuration, flags)).ConfigureAwait(false);
        return true;
    }

    private static Session SessionOf(Player player) => Sessions.GetValue(player, _ => new Session());

    /// <summary>
    /// An item of a deposit, with its operation.
    /// </summary>
    private sealed record Deposit(Item Item, JewelBankOperation Operation);

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
        /// Gets the known outcomes of operations of the stay which couldn't be settled yet:
        /// <c>true</c> adds their jewels to the account, <c>false</c> forgets them.
        /// </summary>
        public Dictionary<Guid, bool> Outcomes { get; } = new();
    }
}
