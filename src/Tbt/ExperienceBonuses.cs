// <copyright file="ExperienceBonuses.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Threading;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The experience bonuses of one plugin: a multiplier of the experience rate of each player, like the
/// Happy Hour, so the bonuses of several plugins stack. All game servers of the process share the
/// plugin instance, and so its bonuses.
/// </summary>
internal sealed class ExperienceBonuses
{
    private readonly Lock _lock = new();

    private readonly Dictionary<Player, ExperienceBonus> _bonuses = new();

    private readonly Type _plugInType;

    private int _isSubscribedToDeactivation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperienceBonuses"/> class.
    /// </summary>
    /// <param name="plugInType">The type of the plugin, whose deactivation ends the bonuses.</param>
    public ExperienceBonuses(Type plugInType)
    {
        this._plugInType = plugInType;
    }

    /// <summary>
    /// Gets the bonus of the player, attached to its current attributes. A new bonus starts neutral. A
    /// bonus of other attributes, e.g. of a character which the player had while the plugin was off,
    /// is replaced.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The bonus, or <c>null</c> when the player has no attributes.</returns>
    public ExperienceBonus? Attach(Player player)
    {
        if (player.Attributes is not { } attributes)
        {
            return null;
        }

        lock (this._lock)
        {
            if (this._bonuses.TryGetValue(player, out var bonus))
            {
                if (ReferenceEquals(bonus.Attributes, attributes))
                {
                    return bonus;
                }

                bonus.Attributes.RemoveElement(bonus.Multiplier, Stats.ExperienceRate);
            }

            bonus = new ExperienceBonus(attributes);
            attributes.AddElement(bonus.Multiplier, Stats.ExperienceRate);
            this._bonuses[player] = bonus;
            return bonus;
        }
    }

    /// <summary>
    /// Removes the bonus of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    public void Detach(Player player)
    {
        lock (this._lock)
        {
            this.Remove(player);
        }
    }

    /// <summary>
    /// Determines whether the bonus is still the one of the player, e.g. after a read during which the
    /// player might have left.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="bonus">The bonus.</param>
    /// <returns><c>true</c>, if it's the bonus of the player.</returns>
    public bool IsAttached(Player player, ExperienceBonus bonus)
    {
        lock (this._lock)
        {
            return this._bonuses.TryGetValue(player, out var current) && ReferenceEquals(current, bonus);
        }
    }

    /// <summary>
    /// Removes the bonuses of the players which aren't in the world of the game server anymore, e.g.
    /// which left while the plugin was off.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="players">The players of the game context.</param>
    public void DetachLeftPlayers(IGameContext gameContext, IList<Player> players)
    {
        lock (this._lock)
        {
            foreach (var player in this._bonuses.Keys.ToList())
            {
                if (ReferenceEquals(player.GameContext, gameContext) && (!players.Contains(player) || !player.IsInWorld()))
                {
                    this.Remove(player);
                }
            }
        }
    }

    /// <summary>
    /// Removes the bonuses of all players of the game server.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    public void DetachAll(IGameContext gameContext)
    {
        lock (this._lock)
        {
            foreach (var player in this._bonuses.Keys.Where(player => ReferenceEquals(player.GameContext, gameContext)).ToList())
            {
                this.Remove(player);
            }
        }
    }

    /// <summary>
    /// Subscribes to the deactivation of the plugin, so that switching it off in the admin panel ends
    /// the bonuses right away. Switching it on again attaches them on its next check.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    public void SubscribeToDeactivation(PlugInManager plugInManager)
    {
        if (Interlocked.Exchange(ref this._isSubscribedToDeactivation, 1) == 0)
        {
            plugInManager.PlugInDeactivated += this.OnPlugInDeactivated;
        }
    }

    /// <summary>
    /// Removes the bonus of the player and its multiplier. Must be called in the <see cref="_lock"/>.
    /// </summary>
    /// <param name="player">The player.</param>
    private void Remove(Player player)
    {
        if (this._bonuses.Remove(player, out var bonus))
        {
            bonus.Attributes.RemoveElement(bonus.Multiplier, Stats.ExperienceRate);
        }
    }

    private void OnPlugInDeactivated(object? sender, PlugInEventArgs e)
    {
        if (e.PlugInType != this._plugInType)
        {
            return;
        }

        lock (this._lock)
        {
            foreach (var player in this._bonuses.Keys.ToList())
            {
                this.Remove(player);
            }
        }
    }
}
