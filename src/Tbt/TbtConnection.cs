// <copyright file="TbtConnection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Reflection;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;

/// <summary>
/// The connection of a player to its game client, for the packets of our plugins which OpenMU doesn't define.
/// </summary>
/// <remarks>
/// The <see cref="RemotePlayer"/> keeps its connection internal to the game server, so it's read by
/// reflection, which leaves OpenMU unchanged. The alternative is an <c>InternalsVisibleTo</c> for
/// this assembly in src/GameServer/Properties/AssemblyInfo.cs. Should OpenMU rename the property,
/// the tests of the period items fail.
/// </remarks>
internal static class TbtConnection
{
    private static readonly Func<RemotePlayer, IConnection?> ConnectionOfRemotePlayer =
        typeof(RemotePlayer).GetProperty("Connection", BindingFlags.Instance | BindingFlags.NonPublic)?.GetMethod?.CreateDelegate<Func<RemotePlayer, IConnection?>>()
        ?? throw new MissingMemberException(typeof(RemotePlayer).FullName, "Connection");

    /// <summary>
    /// Gets the connection of the player to its game client.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The connection, or <c>null</c> for a player without a game client, like a bot or an offline leveling.</returns>
    public static IConnection? GetConnection(this Player player)
        => player is RemotePlayer remotePlayer ? ConnectionOfRemotePlayer(remotePlayer) : null;
}
