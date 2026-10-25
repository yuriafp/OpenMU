// <copyright file="SessionCounters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Registro;

using System.Runtime.CompilerServices;
using System.Threading;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// The counters of a human's stay in the world, for the calibration (PLANO-SERVIDOR.md, section 8):
/// the monsters the character killed, the experience it gained, the zen and the jewels its kills
/// dropped, and the zen and the jewels it had in the inventory and the vault at the entry and at
/// the exit. They go into the data of the logout event, so a stay which ends with a crash of the
/// server is lost.
/// </summary>
/// <remarks>
/// A kill and its drops count for the player who killed, also in a party. The counted experience is
/// only that of the player's own kills outside a party: OpenMU hands out the experience of a party
/// kill without the plugin point. So the data also has the experience of the character at the entry
/// and at the exit, whose difference holds everything when the stay had no reset, which ends a
/// stay. The counted experience is the calculated one, also when the character can't take it
/// (at the maximum level), so the difference is the one to trust. Master experience isn't counted,
/// and neither is the money of a mini game, which is paid instead of dropped. A disconnect leaves
/// the jewels in an open Chaos Machine out of those at the exit, since OpenMU puts them back later.
/// </remarks>
internal sealed class SessionCounters
{
    private static readonly ConditionalWeakTable<Player, SessionCounters> Sessions = new();

    private readonly long _experienceAtEntry;

    private readonly long _zenAtEntry;

    private readonly JewelCount _jewelsAtEntry;

    private readonly Lock _jewelLock = new();

    private long _kills;

    private long _experience;

    private long _droppedZen;

    private JewelCount _droppedJewels;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCounters"/> class, at the entry into the world.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="character">The character which entered the world.</param>
    internal SessionCounters(Account account, Character character)
    {
        this._experienceAtEntry = character.Experience;
        this._zenAtEntry = GetZen(account, character);
        this._jewelsAtEntry = GetJewels(account, character);
    }

    /// <summary>
    /// Starts the counters of a human who entered the world.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="account">The account.</param>
    /// <param name="character">The character which entered the world.</param>
    public static void Start(Player player, Account account, Character character)
    {
        try
        {
            Sessions.AddOrUpdate(player, new SessionCounters(account, character));
        }
        catch (Exception ex)
        {
            // It runs on the state change of the player, which must never fail: the stay just isn't counted.
            player.Logger.LogWarning(ex, "Couldn't start the counters of the stay of {Character}.", character.Name);
        }
    }

    /// <summary>
    /// Gets the counters of the player's stay in the world.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The counters, or <c>null</c> when the player isn't a human in the world.</returns>
    public static SessionCounters? Of(Player player)
        => Sessions.TryGetValue(player, out var counters) ? counters : null;

    /// <summary>
    /// Ends the counters of the player's stay in the world.
    /// </summary>
    /// <param name="player">The player who left the world.</param>
    /// <param name="account">The account.</param>
    /// <param name="character">The character which left the world.</param>
    /// <returns>The data of the stay, or <c>null</c> when it wasn't counted.</returns>
    public static object? Finish(Player player, Account account, Character character)
    {
        if (!Sessions.TryGetValue(player, out var counters))
        {
            return null;
        }

        Sessions.Remove(player);
        try
        {
            return counters.ToData(account, character);
        }
        catch (Exception ex)
        {
            // It runs on the state change of the player, e.g. a disconnect, which must never fail.
            player.Logger.LogWarning(ex, "Couldn't end the counters of the stay of {Character}.", character.Name);
            return null;
        }
    }

    /// <summary>
    /// Counts a killed monster.
    /// </summary>
    public void AddKill() => Interlocked.Increment(ref this._kills);

    /// <summary>
    /// Counts gained experience.
    /// </summary>
    /// <param name="experience">The experience.</param>
    public void AddExperience(int experience) => Interlocked.Add(ref this._experience, experience);

    /// <summary>
    /// Counts zen which a kill dropped.
    /// </summary>
    /// <param name="amount">The amount.</param>
    public void AddDroppedZen(uint amount) => Interlocked.Add(ref this._droppedZen, amount);

    /// <summary>
    /// Counts an item which a kill dropped, when it's a jewel.
    /// </summary>
    /// <param name="item">The item.</param>
    public void AddDroppedItem(Item item)
    {
        var jewels = JewelCount.Of(item);
        if (jewels == default)
        {
            return;
        }

        lock (this._jewelLock)
        {
            this._droppedJewels += jewels;
        }
    }

    /// <summary>
    /// Gets the data of the stay, at the exit from the world.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="character">The character which left the world.</param>
    /// <returns>The data, serialized as a JSON object.</returns>
    internal object ToData(Account account, Character character)
    {
        JewelCount droppedJewels;
        lock (this._jewelLock)
        {
            droppedJewels = this._droppedJewels;
        }

        return new
        {
            kills = Interlocked.Read(ref this._kills),
            exp = Interlocked.Read(ref this._experience),
            exp_in = this._experienceAtEntry,
            exp_out = character.Experience,
            zen_drops = Interlocked.Read(ref this._droppedZen),
            zen_in = this._zenAtEntry,
            zen_out = GetZen(account, character),
            jewel_drops = droppedJewels.ToData(),
            jewels_in = this._jewelsAtEntry.ToData(),
            jewels_out = GetJewels(account, character).ToData(),
        };
    }

    private static long GetZen(Account account, Character character)
        => (long)(character.Inventory?.Money ?? 0) + (account.Vault?.Money ?? 0);

    private static JewelCount GetJewels(Account account, Character character)
        => JewelCount.Of((character.Inventory?.Items ?? []).Concat(account.Vault?.Items ?? []));
}
