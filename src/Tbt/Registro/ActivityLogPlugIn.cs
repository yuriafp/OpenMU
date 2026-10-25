// <copyright file="ActivityLogPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The registro (PLANO-SERVIDOR.md, section 8, item 1 of 9.5): entries and exits of the world, resets,
/// mini game participation and deaths by other players, in <c>tbt.event</c>. Bots are recorded too,
/// with <c>is_bot</c> set, but only the state of the human characters is tracked. The exit of a human
/// carries the counters of the stay (<see cref="SessionCounters"/>).
/// </summary>
[PlugIn]
[Display(Name = "TBT: Registro", Description = "Records the entries and exits of the world, with the kills, experience, zen and jewels of each stay, the resets, the mini games and the deaths by other players in the table tbt.event.")]
[Guid("9C3E1A47-2B6D-4F85-A0C9-7E4D2B1F6A38")]
public class ActivityLogPlugIn : IPlayerStateChangedPlugIn, IMiniGameStartedPlugIn, IMiniGameEndedPlugIn, IAttackableGotKilledPlugIn,
    IPlayerGainedExperiencePlugIn, IMonsterItemDroppedPlugIn, IMonsterMoneyDroppedPlugIn
{
    /// <summary>
    /// The characters with which the players are in the world; OpenMU takes the character away from
    /// the player before the exit reaches the plugins.
    /// </summary>
    private readonly ConditionalWeakTable<Player, Character> _characters = new();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (TbtPlayerExtensions.IsEnteringWorld(previousState, currentState) && player.SelectedCharacter is { } character)
        {
            this._characters.AddOrUpdate(player, character);
            ActivityLog.Write("login", player);
            if (player.Account is { } account && player.IsHuman())
            {
                SessionCounters.Start(player, account, character);
                await this.TrackEntryAsync(player).ConfigureAwait(false);
            }
        }
        else if (TbtPlayerExtensions.IsLeavingWorld(previousState, currentState) && this._characters.TryGetValue(player, out var leftCharacter))
        {
            this._characters.Remove(player);
            var stay = player.Account is { } leftAccount ? SessionCounters.Finish(player, leftAccount, leftCharacter) : null;
            ActivityLog.Write("logout", player, stay, leftCharacter);
            if (player.Account is { IsBot: false })
            {
                try
                {
                    await CharacterTracker.LeftWorldAsync(player).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogWarning(ex, "Couldn't track the exit of {Character}.", leftCharacter.Name);
                }
            }
        }
    }

    /// <inheritdoc />
    public ValueTask MiniGameStartedAsync(MiniGameContext miniGame, IReadOnlyCollection<Player> players)
    {
        var definition = miniGame.Definition;
        foreach (var player in players)
        {
            ActivityLog.Write("minigame_start", player, new { type = definition.Type.ToString(), level = definition.GameLevel, players = players.Count });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        var definition = miniGame.Definition;
        foreach (var player in finishers)
        {
            ActivityLog.Write("minigame_end", player, new { type = definition.Type.ToString(), level = definition.GameLevel, winner = ReferenceEquals(player, winner) });
        }

        if (winner is not null && !finishers.Contains(winner))
        {
            ActivityLog.Write("minigame_end", winner, new { type = definition.Type.ToString(), level = definition.GameLevel, winner = true });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is Player victim && killer is Player murderer)
        {
            ActivityLog.Write("pvp_death", victim, new
            {
                killer = murderer.Name,
                killer_account = murderer.Account?.LoginName,
                killer_is_bot = murderer.Account?.IsBot is true,
                duel = victim.DuelRoom is not null,
                minigame = victim.CurrentMiniGame?.Definition.Type.ToString(),
            });
        }
        else if (killed is Monster { SummonedBy: null } && GetHunter(killer) is { } hunter)
        {
            SessionCounters.Of(hunter)?.AddKill();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask PlayerGainedExperienceAsync(Player player, int experience, IAttackable killedObject, bool isMasterExperience)
    {
        if (!isMasterExperience)
        {
            SessionCounters.Of(player)?.AddExperience(experience);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask MonsterItemDroppedAsync(AttackableNpcBase monster, Player killer, DroppedItem droppedItem)
    {
        SessionCounters.Of(killer)?.AddDroppedItem(droppedItem.Item);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask MonsterMoneyDroppedAsync(AttackableNpcBase monster, Player killer, DroppedMoney droppedMoney)
    {
        SessionCounters.Of(killer)?.AddDroppedZen(droppedMoney.Amount);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the player who gets the credit of a kill: the killer itself, or the owner of the Dark
    /// Raven or the summoned monster which killed, as OpenMU credits the experience and the drops.
    /// </summary>
    /// <param name="killer">The killer.</param>
    /// <returns>The player, or <c>null</c> when no player killed.</returns>
    internal static Player? GetHunter(IAttacker? killer)
        => killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;

    private async ValueTask TrackEntryAsync(Player player)
    {
        try
        {
            var previous = await CharacterTracker.EnteredWorldAsync(player).ConfigureAwait(false);
            var current = CharacterTracker.Read(player.Account!, player.SelectedCharacter!);
            if (previous is not null && previous.Resets < current.Resets)
            {
                ActivityLog.Write("reset", player, new { previous = previous.Resets });
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't track the entry of {Character}.", player.Name);
        }
    }
}
