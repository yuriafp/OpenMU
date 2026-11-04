// <copyright file="TreasureChestPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Treasure;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The treasure chest (PLANO-SERVIDOR.md, 5.4, item 12 of 9.5, decision 12): at the times of the
/// timetable, chests appear at random places outside the safe zone of each configured map, apart from
/// each other, for a few minutes. Who clicks first gets its jewels; a golden chest has more. It's a chest of our own
/// (1001, golden 1002), which our client shows with the treasure chest of Mu Kronus and handles as an
/// NPC, like the reward boxes of the Doppelganger, so it's opened by talking to it, like in that event
/// (<see cref="DoppelgangerRewardChestPlugIn"/>).
/// </summary>
/// <remarks>
/// All players see the appearance, with the cities but not the places, and each opening. The jewels fall at the chest, for
/// the one who opened it. Bots never talk to it. Every opening is written to the registro, which tells
/// the share of the chests in the jewels of the group (PLANO-SERVIDOR.md, section 8).
/// </remarks>
[PlugIn]
[Display(Name = "TBT: Baú do tesouro", Description = "Puts treasure chests at random places of each configured map (10 in each of Lorencia, Noria and Devias by default) at the times of the timetable; who opens one first gets its jewels.")]
[Guid("2B7E4C19-6A3D-4F82-9D15-8C0B5E7A3F64")]
public class TreasureChestPlugIn : PeriodicTaskBasePlugIn<TreasureChestConfiguration, TreasureChestState>, IPlayerTalkToNpcPlugIn, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The health of the chests. They're opened by talking to them, so attacks shouldn't destroy them.
    /// </summary>
    private const int ChestHealth = 1_000_000_000;

    /// <summary>
    /// The maximum distance of a player to a chest to open it, like in the Doppelganger.
    /// </summary>
    private const int MaximumOpenDistance = 4;

    /// <summary>
    /// The smallest distance between two chests of a map, so that they spread over it. When the map
    /// hasn't enough places that far apart, the remaining chests go to any other free place.
    /// </summary>
    private const int MinimumChestDistance = 8;

    /// <summary>
    /// The time after which an opened chest is removed, so that the client can show its opening.
    /// </summary>
    private static readonly TimeSpan OpenedChestRemovalDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The jewels which a chest can contain, drawn with the same chance each.
    /// </summary>
    private static readonly ItemIdentifier[] PossibleJewels =
    [
        ItemConstants.JewelOfBless,
        ItemConstants.JewelOfSoul,
        ItemConstants.JewelOfChaos,
        ItemConstants.JewelOfLife,
        ItemConstants.JewelOfCreation,
    ];

    /// <inheritdoc />
    public object CreateDefaultConfig() => TreasureChestConfiguration.CreateDefault();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        try
        {
            var state = this.GetStateByGameContext(player.GameContext);
            if (npc is not Destructible chest || !state.Chests.TryGetValue(chest, out var treasure))
            {
                return;
            }

            // Also a click on a chest which is being opened, so that the client doesn't get the message
            // of an NPC without a talk.
            eventArgs.HasBeenHandled = true;
            if (!player.IsHuman()
                || !chest.IsAlive
                || chest.GetDistanceTo(player) > MaximumOpenDistance
                || !treasure.TryClaim())
            {
                return;
            }

            try
            {
                await this.OpenAsync(player, chest, treasure.IsGolden).ConfigureAwait(false);
            }
            finally
            {
                // Even when the opening failed: a claimed chest isn't removed by the end of the round.
                RemoveLater(state, chest, player.Logger);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would keep the other plugins
            // of the talk from running.
            player.Logger.LogWarning(ex, "Couldn't open the treasure chest for {Character}.", player.Name);
        }
    }

    /// <summary>
    /// Draws the jewels of a chest, each one of the <see cref="PossibleJewels"/> with the same chance.
    /// </summary>
    /// <param name="count">The number of jewels.</param>
    /// <param name="nextIndex">Gets a random index below the specified number.</param>
    /// <returns>The jewels.</returns>
    internal static IReadOnlyList<ItemIdentifier> DrawJewels(int count, Func<int, int> nextIndex)
        => Enumerable.Range(0, Math.Max(0, count)).Select(_ => PossibleJewels[nextIndex(PossibleJewels.Length)]).ToList();

    /// <summary>
    /// Describes the drawn jewels for the message, e.g. "1 Jewel of Bless" or "2 Jewel of Soul, 1 Jewel of Life".
    /// </summary>
    /// <param name="jewels">The jewels, with their names.</param>
    /// <returns>The description.</returns>
    internal static string Describe(IEnumerable<string> jewels)
        => string.Join(", ", jewels.GroupBy(name => name).Select(group => $"{group.Count()} {group.Key}"));

    /// <summary>
    /// Lists the cities of a round for the message, e.g. "Lorencia, Noria e Devias".
    /// </summary>
    /// <param name="cities">The cities.</param>
    /// <returns>The list.</returns>
    internal static string ListCities(IReadOnlyList<string> cities)
        => cities.Count < 2 ? string.Concat(cities) : $"{string.Join(", ", cities.Take(cities.Count - 1))} e {cities[^1]}";

    /// <summary>
    /// Picks the places of the chests of a map: random ones, at least <paramref name="minimumDistance"/>
    /// apart while the map has such places, then any other free ones.
    /// </summary>
    /// <param name="places">The places of the map.</param>
    /// <param name="count">The number of chests.</param>
    /// <param name="minimumDistance">The smallest distance between two chests.</param>
    /// <param name="nextIndex">Gets a random index below the specified number.</param>
    /// <returns>The distinct places, at most <paramref name="count"/>.</returns>
    internal static IReadOnlyList<Point> PickPlaces(IReadOnlyList<Point> places, int count, int minimumDistance, Func<int, int> nextIndex)
    {
        // A random order, by Fisher-Yates.
        var shuffled = places.Distinct().ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = nextIndex(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var picked = new List<Point>();
        foreach (var place in shuffled)
        {
            if (picked.Count >= count)
            {
                return picked;
            }

            if (picked.TrueForAll(other => other.EuclideanDistanceTo(place) >= minimumDistance))
            {
                picked.Add(place);
            }
        }

        foreach (var place in shuffled)
        {
            if (picked.Count >= count)
            {
                break;
            }

            if (!picked.Contains(place))
            {
                picked.Add(place);
            }
        }

        return picked;
    }

    /// <inheritdoc />
    protected override TreasureChestState CreateState(IGameContext gameContext) => new(gameContext);

    /// <inheritdoc />
    protected override ValueTask OnPrepareEventAsync(TreasureChestState state) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask OnPreparedAsync(TreasureChestState state) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(TreasureChestState state)
    {
        var configuration = this.Configuration ??= TreasureChestConfiguration.CreateDefault();
        var logger = state.Context.LoggerFactory.CreateLogger(this.GetType().Name);
        var cities = new List<string>();
        foreach (var mapNumber in configuration.Maps.Where(number => number >= 0).Distinct())
        {
            try
            {
                if (await this.SpawnChestsAsync(state, (ushort)mapNumber, configuration, logger).ConfigureAwait(false) is { } city)
                {
                    cities.Add(city);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't put the treasure chests on the map {Map}.", mapNumber);
            }
        }

        if (cities.Count > 0)
        {
            await GlobalNotification.SendAsync(state.Context, configuration.StartMessage, TreasureChestConfiguration.CreateDefault().StartMessage, logger, (int)configuration.TaskDuration.TotalMinutes, ListCities(cities), configuration.ChestsPerMap).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnFinishedAsync(TreasureChestState state)
    {
        var logger = state.Context.LoggerFactory.CreateLogger(this.GetType().Name);
        var anyLeft = false;
        foreach (var (chest, treasure) in state.Chests.ToList())
        {
            // An opened chest is removed after its opening.
            if (!treasure.TryClaim())
            {
                continue;
            }

            anyLeft = true;
            state.Chests.TryRemove(chest, out _);
            try
            {
                await RemoveAsync(chest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't remove the treasure chest of the map {Map}.", chest.CurrentMap?.MapId);
            }
        }

        if (anyLeft && this.Configuration?.EndMessage is { } endMessage && !string.IsNullOrWhiteSpace(endMessage))
        {
            await state.Context.SendGlobalNotificationAsync(endMessage).ConfigureAwait(false);
        }
    }

    private static void RemoveLater(TreasureChestState state, Destructible chest, ILogger logger)
    {
        // The removal waits for the opening animation, without keeping the player in the dialog.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(OpenedChestRemovalDelay).ConfigureAwait(false);
                state.Chests.TryRemove(chest, out _);
                await RemoveAsync(chest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't remove the opened treasure chest.");
            }
        });
    }

    private static async ValueTask RemoveAsync(Destructible chest)
    {
        // A chest which died (area skills hit it too) removed itself, and its id may already belong to
        // another object.
        if (chest.IsDisposed || chest.IsDisposing)
        {
            return;
        }

        await chest.CurrentMap.RemoveAsync(chest).ConfigureAwait(false);
        chest.Dispose();
    }

    /// <summary>
    /// Puts the chests of a round on a map, see <see cref="TreasureChestConfiguration.ChestsPerMap"/>.
    /// </summary>
    /// <returns>The name of the map; <c>null</c>, when no chest was put on it.</returns>
    private async ValueTask<string?> SpawnChestsAsync(TreasureChestState state, ushort mapNumber, TreasureChestConfiguration configuration, ILogger logger)
    {
        if (await state.Context.GetMapAsync(mapNumber).ConfigureAwait(false) is not { } map
            || MapPlaces.GetReachablePlaces(map.Terrain) is not { Length: > 0 } places)
        {
            logger.LogWarning("The map {Map} of the treasure chests doesn't exist or has no free place.", mapNumber);
            return null;
        }

        var spawned = 0;
        foreach (var position in PickPlaces(places, configuration.ChestsPerMap, MinimumChestDistance, max => Rand.NextInt(0, max)))
        {
            try
            {
                // Not Rand.NextRandomBool(percent), which is true for one more than the percent.
                var isGolden = Rand.NextInt(0, 100) < configuration.GoldenChance;
                var chestNumber = isGolden ? configuration.GoldenChestNumber : configuration.ChestNumber;
                if (await this.SpawnChestAsync(state, map, position, chestNumber, isGolden, logger).ConfigureAwait(false) is not null)
                {
                    spawned++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't put the treasure chest at {Position} of the map {Map}.", position, mapNumber);
            }
        }

        return spawned > 0 ? map.Definition.Name.ValueInNeutralLanguage : null;
    }

    private async ValueTask<Destructible?> SpawnChestAsync(TreasureChestState state, GameMap map, Point position, short chestNumber, bool isGolden, ILogger logger)
    {
        var gameContext = state.Context;
        var mapNumber = map.MapId;
        if (gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == chestNumber) is not { } definition)
        {
            logger.LogWarning("The treasure chest {Chest} isn't configured.", chestNumber);
            return null;
        }

        var area = new MonsterSpawnArea
        {
            GameMap = map.Definition,
            MonsterDefinition = definition,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = position.X,
            X2 = position.X,
            Y1 = position.Y,
            Y2 = position.Y,
            MaximumHealthOverride = ChestHealth,
        };
        var chest = new Destructible(area, definition, map, null, gameContext.DropGenerator, gameContext.PlugInManager);
        try
        {
            chest.Initialize();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Couldn't put the treasure chest at {Position} of the map {Map}.", position, mapNumber);
            chest.Dispose();
            return null;
        }

        try
        {
            await map.AddAsync(chest).ConfigureAwait(false);
        }
        catch
        {
            chest.Dispose();
            throw;
        }

        // Known as soon as it's on the map, so that the end of the round removes it whatever happens next.
        state.Chests[chest] = new TreasureChest(isGolden);
        chest.OnSpawn();
        return chest;
    }

    private async ValueTask OpenAsync(Player player, Destructible chest, bool isGolden)
    {
        var configuration = this.Configuration ??= TreasureChestConfiguration.CreateDefault();
        var map = chest.CurrentMap;
        var jewels = DrawJewels(isGolden ? configuration.GoldenJewels : configuration.Jewels, max => Rand.NextInt(0, max))
            .Select(id => player.GameContext.Configuration.Items.FirstOrDefault(item => item.Group == id.Group && item.Number == id.Number))
            .OfType<ItemDefinition>()
            .ToList();

        // The client shows the opening of the chest as its death.
        await chest.ForEachWorldObserverAsync<IObjectGotKilledPlugIn>(p => p.ObjectGotKilledAsync(chest, player), true).ConfigureAwait(false);
        var isFirst = true;
        foreach (var jewel in jewels)
        {
            var position = isFirst ? chest.Position : map.Terrain.GetRandomCoordinate(chest.Position, 2);
            isFirst = false;
            var item = new TemporaryItem { Definition = jewel, Durability = 1 };
            await map.AddAsync(new DroppedItem(item, position, map, null, player.GetAsEnumerable())).ConfigureAwait(false);
        }

        var names = jewels.Select(jewel => jewel.Name.ValueInNeutralLanguage).ToList();
        var mapName = map.Definition.Name.ValueInNeutralLanguage;
        ActivityLog.Write("chest", player, new { map = map.MapId, golden = isGolden, jewels = names });
        await GlobalNotification.SendAsync(player.GameContext, configuration.OpenedMessage, TreasureChestConfiguration.CreateDefault().OpenedMessage, player.Logger, player.Name, mapName, Describe(names)).ConfigureAwait(false);
    }
}
