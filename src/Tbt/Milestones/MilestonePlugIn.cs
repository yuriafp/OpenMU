// <copyright file="MilestonePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The milestones per account and the celebrated reset (PLANO-SERVIDOR.md, 5.5 and 5.6, item 4 of 9.5).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The level and reset milestones give their reward once per account, and show the next goal.</item>
/// <item>Every reset of a human is announced to everyone. OpenMU has no plugin point for it, and the
/// reset logs the player out, so it shows when the character enters the world again (<see cref="CharacterTracker"/>).</item>
/// <item>The first wings and the first +10 of an account, and the firsts of the server, show the same
/// way: the Chaos Machine has no plugin point for its result, so the inventory is checked on the entry.</item>
/// <item>The first option of the Oficina comes from the Oficina, see <see cref="WorkshopOptionAddedAsync"/>.</item>
/// </list>
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Marcos", Description = "The milestones per account with their rewards, the celebrated resets and the firsts of the server.")]
[Guid("A84D6E19-3C5B-4E72-B1F8-5D9A2C7E0B36")]
public class MilestonePlugIn : IPlayerStateChangedPlugIn, ICharacterLevelUpPlugIn
{
    /// <summary>The levels with a progress message, every cycle, without a reward.</summary>
    private static readonly int[] ProgressLevels = [300, 350, 380];

    /// <summary>The second tier wings: Spirits, Soul, Dragon, Darkness, Despair, the Cape of Lord and the Cape of Fighter.</summary>
    private static readonly (byte Group, short Number)[] SecondTierWings = [(12, 3), (12, 4), (12, 5), (12, 6), (12, 42), (13, 30), (12, 49)];

    /// <summary>The third tier wings: Storm, Eternal, Illusion, Ruin, Cape of Emperor, Dimension and Cape of Overrule.</summary>
    private static readonly (byte Group, short Number)[] ThirdTierWings = [(12, 36), (12, 37), (12, 38), (12, 39), (12, 40), (12, 43), (12, 50)];

    /// <summary>
    /// Is called by the Oficina when it added an option to an item; announces the first option of
    /// the account and the first item of the server with three excellent options.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item.</param>
    /// <returns>The task.</returns>
    public static async ValueTask WorkshopOptionAddedAsync(Player player, Item item)
    {
        if (!player.IsHuman() || !player.GameContext.PlugInManager.IsPlugInActive(typeof(MilestonePlugIn)))
        {
            return;
        }

        try
        {
            var account = player.Account!;
            var milestones = await MilestoneStore.GetAsync(account.GetId()).ConfigureAwait(false);
            if (await TryAchieveAsync(player, milestones, "first_workshop_option", []).ConfigureAwait(false))
            {
                await AnnounceAsync(player, $"{player.Name} fez a primeira opção na Oficina!").ConfigureAwait(false);
            }

            if (item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent) >= 3
                && await MilestoneStore.TryRecordServerFirstAsync("three_excellent_options", account.LoginName, player.Name).ConfigureAwait(false))
            {
                await AnnounceAsync(player, $"{player.Name} fez o primeiro item com 3 opções excelentes do servidor!").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the milestones of the Oficina for {Character}.", player.Name);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) && player.IsHuman())
            {
                await EnteredWorldAsync(player).ConfigureAwait(false);
            }
            else if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState) && player.Account is { IsBot: false })
            {
                // Ends the stay also while the registro is off; otherwise the next entry would count as the same stay.
                await CharacterTracker.LeftWorldAsync(player).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the state change.
            player.Logger.LogWarning(ex, "Couldn't check the milestones of {Character}.", player.Name);
        }
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (!player.IsHuman() || player.SelectedCharacter is not { } character)
        {
            return;
        }

        // The plugin point is synchronous, and the milestones need the database. The task must not
        // inherit the flow: it would carry the persistence lock state of the packet handler which
        // caused the level-up, and then skip taking the lock of this player (see PlayerPersistence).
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(() => LeveledUpAsync(player, character));
        }
    }

    private static async Task EnteredWorldAsync(Player player)
    {
        var account = player.Account!;
        var milestones = await MilestoneStore.GetAsync(account.GetId()).ConfigureAwait(false);
        var previous = await CharacterTracker.EnteredWorldAsync(player).ConfigureAwait(false);
        var current = CharacterTracker.Read(account, player.SelectedCharacter!);
        if (previous is not null && current.Resets > previous.Resets)
        {
            await CelebrateResetAsync(player, current.Resets).ConfigureAwait(false);
        }

        await GiveDueMilestonesAsync(player, milestones, current.Level, current.Resets).ConfigureAwait(false);
        await CheckItemsAsync(player, milestones).ConfigureAwait(false);
        await ShowNextGoalAsync(player, milestones, current.Level, current.Resets).ConfigureAwait(false);
    }

    private static async Task LeveledUpAsync(Player player, Character character)
    {
        try
        {
            if (player.Account is not { } account)
            {
                return;
            }

            var state = CharacterTracker.Read(account, character);
            if (ProgressLevels.Contains(state.Level))
            {
                var requiredLevel = player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>()?.Configuration?.RequiredLevel ?? 400;
                if (requiredLevel > state.Level)
                {
                    await player.ShowMessageAsync($"Nível {state.Level}: faltam {requiredLevel - state.Level} níveis para o reset.").ConfigureAwait(false);
                }
            }

            // Not loaded yet right after the entry; the entry gives what's due when they are.
            if (MilestoneStore.GetLoaded(account.GetId()) is not { } milestones)
            {
                return;
            }

            // The rewards change the inventory, so they take the persistence lock of the player, like its packet handlers.
            await player.RunPersistenceExclusiveAsync(async () =>
            {
                if (!ReferenceEquals(player.SelectedCharacter, character))
                {
                    // Left the world in the meantime; the next entry gives what's due.
                    return;
                }

                var current = CharacterTracker.Read(account, character);
                if (await GiveDueMilestonesAsync(player, milestones, current.Level, current.Resets).ConfigureAwait(false))
                {
                    await ShowNextGoalAsync(player, milestones, current.Level, current.Resets).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the milestones of {Character} after the level-up.", player.Name);
        }
    }

    private static async ValueTask CelebrateResetAsync(Player player, int resets)
    {
        if (await MilestoneStore.TryRecordServerFirstAsync("reset", player.Account!.LoginName, player.Name).ConfigureAwait(false))
        {
            await AnnounceAsync(player, $"{player.Name} fez o primeiro reset do servidor!").ConfigureAwait(false);
        }
        else
        {
            await AnnounceAsync(player, $"{player.Name} fez o reset {resets}!").ConfigureAwait(false);
        }
    }

    private static async ValueTask<bool> GiveDueMilestonesAsync(Player player, AccountMilestones milestones, int level, int resets)
    {
        var due = MilestoneCatalog.GetLevelMilestones(player).Where(m => m.Level <= level)
            .Concat(MilestoneCatalog.GetResetMilestones(resets))
            .Where(m => !milestones.Has(m.Key))
            .ToList();
        var given = false;
        foreach (var milestone in due)
        {
            if (!await TryAchieveAsync(player, milestones, milestone.Key, milestone.Rewards).ConfigureAwait(false))
            {
                continue;
            }

            given = true;
            var reached = milestone.Resets > 0 ? $"Marco do reset {milestone.Resets}" : $"Marco do nível {milestone.Level}";
            await player.ShowMessageAsync($"{reached}: você ganhou {milestone.RewardText}!", MessageType.GoldenCenter).ConfigureAwait(false);
            if (milestone.Hint is { } hint)
            {
                await player.ShowMessageAsync(hint).ConfigureAwait(false);
            }
        }

        return given;
    }

    private static async ValueTask ShowNextGoalAsync(Player player, AccountMilestones milestones, int level, int resets)
    {
        if (MilestoneCatalog.GetLevelMilestones(player).FirstOrDefault(m => m.Level > level && !milestones.Has(m.Key)) is { } next)
        {
            await player.ShowMessageAsync($"Próxima meta: nível {next.Level}, com {next.RewardText}.").ConfigureAwait(false);
        }
        else if (MilestoneCatalog.GetNextResetMilestone(resets) is { } nextReset && !milestones.Has(nextReset.Key))
        {
            await player.ShowMessageAsync($"Próxima meta: reset {nextReset.Resets}, com {nextReset.RewardText}.").ConfigureAwait(false);
        }
    }

    private static async ValueTask CheckItemsAsync(Player player, AccountMilestones milestones)
    {
        var items = player.Inventory?.Items.Where(i => i.Definition is not null).ToList() ?? [];
        var account = player.Account!;
        var wings = items.Where(i => i.Definition!.ItemSlot?.ItemSlots.Contains(InventoryConstants.WingsSlot) == true).ToList();
        if (wings.Count > 0 && await TryAchieveAsync(player, milestones, "first_wings", []).ConfigureAwait(false))
        {
            await AnnounceAsync(player, $"{player.Name} conseguiu as primeiras asas!").ConfigureAwait(false);
        }

        if (wings.Any(i => SecondTierWings.Contains((i.Definition!.Group, i.Definition.Number)))
            && await MilestoneStore.TryRecordServerFirstAsync("wings2", account.LoginName, player.Name).ConfigureAwait(false))
        {
            await AnnounceAsync(player, $"{player.Name} tem a primeira asa 2 do servidor!").ConfigureAwait(false);
        }

        if (wings.Any(i => ThirdTierWings.Contains((i.Definition!.Group, i.Definition.Number)))
            && await MilestoneStore.TryRecordServerFirstAsync("wings3", account.LoginName, player.Name).ConfigureAwait(false))
        {
            await AnnounceAsync(player, $"{player.Name} tem a primeira asa 3 do servidor!").ConfigureAwait(false);
        }

        var equipment = items.Where(i => i.Definition!.Group <= 12 && i.IsWearable()).ToList();
        if (equipment.Any(i => i.Level >= 10) && await TryAchieveAsync(player, milestones, "first_plus10", []).ConfigureAwait(false))
        {
            await player.ShowMessageAsync("Seu primeiro item +10! Do +10 em diante, o Chaos Goblin destrói o item na falha.", MessageType.GoldenCenter).ConfigureAwait(false);
        }

        if (equipment.Any(i => i.Level >= 13)
            && await MilestoneStore.TryRecordServerFirstAsync("plus13", account.LoginName, player.Name).ConfigureAwait(false))
        {
            await AnnounceAsync(player, $"{player.Name} fez o primeiro item +13 do servidor!").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the milestone to the account, once: reserved in the cache, recorded in the database and
    /// then rewarded. A reward which doesn't fit into the inventory, or fails, undoes the milestone,
    /// so it's tried again on the next level-up or entry.
    /// </summary>
    private static async ValueTask<bool> TryAchieveAsync(Player player, AccountMilestones milestones, string key, IReadOnlyList<RewardItem> rewards)
    {
        // An open mix or trade keeps a backup of the inventory, which a disconnect restores without
        // the reward; so the reward waits for the next level-up or entry.
        if (rewards.Count > 0 && player.BackupInventory is not null)
        {
            return false;
        }

        if (!milestones.TryReserve(key))
        {
            return false;
        }

        var accountId = player.Account!.GetId();
        bool recorded;
        try
        {
            recorded = await MilestoneStore.TryRecordAsync(accountId, key, player.Name).ConfigureAwait(false);
        }
        catch
        {
            milestones.Release(key);
            throw;
        }

        if (!recorded)
        {
            // Recorded before, so it stays reserved.
            return false;
        }

        bool given;
        try
        {
            given = rewards.Count == 0 || await RewardDelivery.TryGiveAsync(player, rewards).ConfigureAwait(false);
            if (!given)
            {
                await player.ShowMessageAsync("Abra espaço no inventário para receber o prêmio do marco.").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't give the reward of the milestone {Milestone} to {Character}.", key, player.Name);
            given = false;
        }

        if (!given)
        {
            await UndoAsync(player, milestones, accountId, key).ConfigureAwait(false);
            return false;
        }

        ActivityLog.Write("milestone", player, new { milestone = key });
        return true;
    }

    /// <summary>
    /// Undoes a recorded milestone whose reward wasn't given. When that fails too, the milestone
    /// stays recorded without its reward, which a GM has to give.
    /// </summary>
    private static async ValueTask UndoAsync(Player player, AccountMilestones milestones, Guid accountId, string key)
    {
        try
        {
            await MilestoneStore.RemoveAsync(accountId, key).ConfigureAwait(false);
            milestones.Release(key);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The milestone {Milestone} of {Account} is recorded, but its reward wasn't given to {Character}.", key, player.Account?.LoginName, player.Name);
        }
    }

    private static ValueTask AnnounceAsync(Player player, string message)
        => player.GameContext.SendGlobalMessageAsync(message, MessageType.GoldenCenter);
}
