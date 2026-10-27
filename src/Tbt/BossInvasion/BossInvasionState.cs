// <copyright file="BossInvasionState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The run of a boss invasion on one game server.
/// </summary>
public class BossInvasionState : PeriodicTaskGameServerState
{
    private int _hasEnded;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossInvasionState"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    public BossInvasionState(IGameContext context)
        : base(context)
    {
    }

    /// <summary>
    /// Gets or sets the theme of the run, chosen when it's announced; <c>null</c> when no theme can run.
    /// </summary>
    internal BossInvasionTheme? Theme { get; set; }

    /// <summary>
    /// Gets or sets the time when the bosses appeared, in UTC.
    /// </summary>
    internal DateTime AppearedUtc { get; set; }

    /// <summary>
    /// Gets the monsters of the run whose death wasn't handled yet, with what they are, so that the end
    /// of the run removes the living ones; the next run forgets the others.
    /// </summary>
    internal ConcurrentDictionary<Monster, BossInvasionSpawn> Monsters { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the run ended, so that an escort which a boss called
    /// meanwhile leaves at once instead of staying after the others.
    /// </summary>
    /// <remarks>
    /// It's set with a full fence before the end of the run looks at the <see cref="Monsters"/>, so
    /// either the end sees a called escort, or the call sees the end.
    /// </remarks>
    internal bool HasEnded
    {
        get => Volatile.Read(ref this._hasEnded) != 0;
        set => Interlocked.Exchange(ref this._hasEnded, value ? 1 : 0);
    }
}
