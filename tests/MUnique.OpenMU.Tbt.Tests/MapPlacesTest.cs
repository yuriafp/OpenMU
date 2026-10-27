// <copyright file="MapPlacesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Tests the places where our events put something for the players, the treasure chests and the bosses.
/// </summary>
[TestFixture]
public class MapPlacesTest
{
    /// <summary>
    /// A place is only where one can walk to from a safe zone, also diagonally, and never in it.
    /// </summary>
    [Test]
    public void PlacesAreOnlyWhereTheTownReaches()
    {
        var terrain = new GameMapTerrain((byte[]?)null);
        Array.Clear(terrain.WalkMap);
        Array.Clear(terrain.SafezoneMap);
        terrain.WalkMap[10, 10] = terrain.SafezoneMap[10, 10] = true;
        terrain.WalkMap[11, 11] = true;
        terrain.WalkMap[12, 11] = true;
        terrain.WalkMap[50, 50] = true;

        var places = MapPlaces.FindReachablePlaces(terrain);

        Assert.That(places, Is.EquivalentTo(new[] { new Point(11, 11), new Point(12, 11) }));
    }

    /// <summary>
    /// On a map without a safe zone, any walkable tile is a place.
    /// </summary>
    [Test]
    public void PlacesAreAnywhereWithoutSafeZone()
    {
        var terrain = new GameMapTerrain((byte[]?)null);
        Array.Clear(terrain.WalkMap);
        Array.Clear(terrain.SafezoneMap);
        terrain.WalkMap[11, 11] = true;
        terrain.WalkMap[50, 50] = true;

        var places = MapPlaces.FindReachablePlaces(terrain);

        Assert.That(places, Is.EquivalentTo(new[] { new Point(11, 11), new Point(50, 50) }));
    }
}
