// <copyright file="TreasureChestState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Treasure;

using System.Collections.Concurrent;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The chests of a round on one game server.
/// </summary>
public class TreasureChestState : PeriodicTaskGameServerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TreasureChestState"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    public TreasureChestState(IGameContext context)
        : base(context)
    {
    }

    /// <summary>
    /// Gets the chests of the round which are still on their maps. An opened chest stays here until it's
    /// removed, so that the clicks on it during its opening are still taken as clicks on a chest.
    /// </summary>
    internal ConcurrentDictionary<Destructible, TreasureChest> Chests { get; } = new();
}
