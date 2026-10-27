// <copyright file="MapPlaces.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The places of a map where our events put something for the players to find, like the treasure
/// chests and the invading bosses.
/// </summary>
internal static class MapPlaces
{
    /// <summary>
    /// The places of each terrain. The terrains of the maps don't change at runtime, so they're
    /// computed once.
    /// </summary>
    private static readonly ConditionalWeakTable<GameMapTerrain, Point[]> ReachablePlaces = new();

    /// <summary>
    /// Gets the walkable tiles outside the safe zones which can be walked to from a safe zone, so that
    /// nobody gets a place which can't be reached (some of Devias). Without a safe zone, all walkable
    /// tiles outside of it. Computed once per terrain.
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>The places.</returns>
    public static Point[] GetReachablePlaces(GameMapTerrain terrain)
        => ReachablePlaces.GetValue(terrain, FindReachablePlaces);

    /// <summary>
    /// Finds the walkable tiles outside the safe zones which can be walked to from a safe zone, see
    /// <see cref="GetReachablePlaces"/>.
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>The places.</returns>
    internal static Point[] FindReachablePlaces(GameMapTerrain terrain)
    {
        var size = terrain.WalkMap.GetLength(0);
        var reached = new bool[size, size];
        var queue = new Queue<(int X, int Y)>();
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                if (terrain.WalkMap[x, y] && terrain.SafezoneMap[x, y])
                {
                    reached[x, y] = true;
                    queue.Enqueue((x, y));
                }
            }
        }

        if (queue.Count == 0)
        {
            return GetWalkablePlaces(terrain);
        }

        var places = new List<Point>();
        while (queue.TryDequeue(out var tile))
        {
            if (!terrain.SafezoneMap[tile.X, tile.Y])
            {
                places.Add(new Point((byte)tile.X, (byte)tile.Y));
            }

            // Like the walking of the players, also diagonally.
            for (var x = Math.Max(0, tile.X - 1); x <= Math.Min(size - 1, tile.X + 1); x++)
            {
                for (var y = Math.Max(0, tile.Y - 1); y <= Math.Min(size - 1, tile.Y + 1); y++)
                {
                    if (!reached[x, y] && terrain.WalkMap[x, y])
                    {
                        reached[x, y] = true;
                        queue.Enqueue((x, y));
                    }
                }
            }
        }

        return places.ToArray();
    }

    private static Point[] GetWalkablePlaces(GameMapTerrain terrain)
    {
        var size = terrain.WalkMap.GetLength(0);
        var places = new List<Point>();
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                if (terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y])
                {
                    places.Add(new Point((byte)x, (byte)y));
                }
            }
        }

        return places.ToArray();
    }
}
