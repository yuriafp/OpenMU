// <copyright file="BossInvasionPlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The boss invasion (PLANO-SERVIDOR.md, 5.4, item 13 of 9.5): once a week, the bosses of a theme
/// appear with their escort at a random place outside the town of their maps, with many times the
/// health of their definition, so that it takes a group to kill them. The party which kills a boss
/// gets its Boxes of Kundun. The themes take turns, one per week.
/// </summary>
/// <remarks>
/// It's our own periodic task instead of the <c>BaseInvasionPlugIn</c> of OpenMU, which can't give a
/// monster its own health or gather the escort around the boss. The monsters have the basic monster
/// intelligence, so no boss brings the phases of its own map (Crywolf, Kanturu, Raklion); the bosses
/// with skills have the <see cref="InvasionBossIntelligence"/>. Every appearance, kill and retreat is
/// written to the registro, which tells how long the fights take (PLANO-SERVIDOR.md, section 8).
/// </remarks>
public abstract class BossInvasionPlugInBase : PeriodicTaskBasePlugIn<BossInvasionConfiguration, BossInvasionState>, IAttackableGotKilledPlugIn, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The distance around a dead boss in which its prize boxes fall.
    /// </summary>
    private const byte PrizeSpread = 2;

    private const int DaysPerWeek = 7;

    private BossInvasionConfiguration? _defaultConfiguration;

    /// <summary>
    /// Gets the default configuration, whose messages replace the configured ones with a bad format.
    /// </summary>
    private BossInvasionConfiguration DefaultConfiguration => this._defaultConfiguration ??= this.CreateDefaultConfiguration();

    /// <inheritdoc />
    public object CreateDefaultConfig() => this.CreateDefaultConfiguration();

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        // The plugin point is only called when the killer is a player, or a summon or a pet of one.
        if (killed is not Monster monster || GetKillingPlayer(killer) is not { } player)
        {
            return;
        }

        try
        {
            var state = this.GetStateByGameContext(player.GameContext);
            if (state.Monsters.TryRemove(monster, out var spawn) && spawn.IsBoss)
            {
                await this.OnBossKilledAsync(state, monster, spawn, player).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // The plugin proxies of OpenMU don't catch, so an exception here would break the death.
            player.Logger.LogWarning(ex, "Couldn't handle the death of the boss {Boss}.", monster.Definition.Designation.ValueInNeutralLanguage);
        }
    }

    /// <summary>
    /// Chooses the theme of the week: the themes take turns, one per week from Monday on in UTC, and a
    /// theme which can't run gives its turn to the next one.
    /// </summary>
    /// <param name="themes">The themes.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="canRun">Determines whether a theme can run.</param>
    /// <returns>The theme, or <c>null</c> when none can run.</returns>
    internal static BossInvasionTheme? ChooseTheme(IList<BossInvasionTheme> themes, DateTime utcNow, Func<BossInvasionTheme, bool> canRun)
    {
        // The weeks since the 1st of January of the year 1, a Monday.
        var week = utcNow.Ticks / TimeSpan.TicksPerDay / DaysPerWeek;
        for (var i = 0; i < themes.Count; i++)
        {
            var theme = themes[(int)((week + i) % themes.Count)];
            if (canRun(theme))
            {
                return theme;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the places around a center, within a distance in both directions.
    /// </summary>
    /// <param name="places">The places of the map.</param>
    /// <param name="center">The center, one of the places.</param>
    /// <param name="radius">The distance.</param>
    /// <returns>The places around the center, the center included.</returns>
    internal static Point[] GetPlacesAround(IEnumerable<Point> places, Point center, int radius)
        => places.Where(p => Math.Abs(p.X - center.X) <= radius && Math.Abs(p.Y - center.Y) <= radius).ToArray();

    /// <summary>
    /// Gets the health of a monster of the invasion: the health of its spawn, or the health of its
    /// definition times the multiplier for a boss.
    /// </summary>
    /// <param name="spawn">The spawn of the monster.</param>
    /// <param name="definitionHealth">The health of its definition.</param>
    /// <param name="bossHealthMultiplier">The multiplier of the health of a boss; below 1, it counts as 1.</param>
    /// <returns>The health, or <c>null</c> for the health of the definition.</returns>
    internal static int? GetHealth(BossInvasionSpawn spawn, float definitionHealth, int bossHealthMultiplier)
    {
        if (spawn.Health > 0)
        {
            return spawn.Health;
        }

        if (!spawn.IsBoss || definitionHealth <= 0)
        {
            return null;
        }

        // The range of the multiplier holds in the admin panel only; a boss without health would never die.
        return (int)Math.Min(int.MaxValue, (double)definitionHealth * Math.Max(1, bossHealthMultiplier));
    }

    /// <summary>
    /// Creates the default configuration of the invasion.
    /// </summary>
    /// <returns>The default configuration.</returns>
    protected abstract BossInvasionConfiguration CreateDefaultConfiguration();

    /// <inheritdoc />
    protected override BossInvasionState CreateState(IGameContext gameContext) => new(gameContext);

    /// <inheritdoc />
    protected override ValueTask OnPrepareEventAsync(BossInvasionState state)
    {
        // The monsters of the last run which died without a player as their killer are still known.
        state.Monsters.Clear();
        state.HasEnded = false;
        var configuration = this.Configuration ??= this.CreateDefaultConfiguration();
        state.Theme = ChooseTheme(configuration.Themes, DateTime.UtcNow, theme => CanRun(state.Context, theme));
        if (state.Theme is null)
        {
            state.Context.LoggerFactory.CreateLogger(this.GetType().Name).LogWarning("No theme of the boss invasion can run: none has a boss which is configured and active, on an active map.");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask OnPreparedAsync(BossInvasionState state)
    {
        if (state.Theme is not { } theme)
        {
            return;
        }

        var configuration = this.Configuration ??= this.CreateDefaultConfiguration();
        var logger = state.Context.LoggerFactory.CreateLogger(this.GetType().Name);
        await GlobalNotification.SendAsync(state.Context, configuration.StartMessage, this.DefaultConfiguration.StartMessage, logger, (int)configuration.PreStartMessageDelay.TotalMinutes, theme.Name).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(BossInvasionState state)
    {
        if (state.Theme is not { } theme)
        {
            return;
        }

        var configuration = this.Configuration ??= this.CreateDefaultConfiguration();
        var logger = state.Context.LoggerFactory.CreateLogger(this.GetType().Name);
        state.AppearedUtc = DateTime.UtcNow;
        var bosses = new List<string>();
        foreach (var spawnsOfMap in theme.Spawns.Where(spawn => spawn.Map >= 0).GroupBy(spawn => spawn.Map))
        {
            try
            {
                // The bosses first, so that the first one is at the center of its escort.
                var spawns = spawnsOfMap.OrderByDescending(spawn => spawn.IsBoss).ToList();
                bosses.AddRange(await this.SpawnOnMapAsync(state, (ushort)spawnsOfMap.Key, spawns, configuration, logger).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't put the monsters of the boss invasion on the map {Map}.", spawnsOfMap.Key);
            }
        }

        if (bosses.Count == 0)
        {
            return;
        }

        ActivityLog.Write("boss_invasion", state.Context, new { theme = theme.Name, bosses });
        await GlobalNotification.SendAsync(state.Context, configuration.AppearedMessage, this.DefaultConfiguration.AppearedMessage, logger, theme.Name, string.Join(", ", bosses)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnFinishedAsync(BossInvasionState state)
    {
        // First, so that an escort which a boss calls from now on leaves at once (CallEscortAsync).
        state.HasEnded = true;
        var logger = state.Context.LoggerFactory.CreateLogger(this.GetType().Name);
        var escaped = new List<string>();
        foreach (var (monster, spawn) in state.Monsters.ToList())
        {
            // A monster which is dying now is left to its death, which gives the prize of a boss.
            if (!monster.IsAlive || !state.Monsters.TryRemove(monster, out _))
            {
                continue;
            }

            if (spawn.IsBoss)
            {
                escaped.Add(monster.Definition.Designation.ValueInNeutralLanguage);
            }

            try
            {
                await RemoveAsync(monster).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't remove the monster {Monster} of the boss invasion.", monster.Definition.Designation.ValueInNeutralLanguage);
            }
        }

        if (escaped.Count > 0)
        {
            ActivityLog.Write("boss_escape", state.Context, new { theme = state.Theme?.Name, bosses = escaped });
            var configuration = this.Configuration ??= this.CreateDefaultConfiguration();
            await GlobalNotification.SendAsync(state.Context, configuration.EndMessage, this.DefaultConfiguration.EndMessage, logger).ConfigureAwait(false);
        }

        state.Theme = null;
    }

    private static bool CanRun(IGameContext gameContext, BossInvasionTheme theme)
        => theme.Spawns.Any(spawn => spawn.IsBoss
                                     && GetActiveDefinition(gameContext, spawn) is not null
                                     && gameContext.Configuration.Maps.Any(map => map.Number == spawn.Map && map.IsActive));

    private static MonsterDefinition? GetActiveDefinition(IGameContext gameContext, BossInvasionSpawn spawn)
        => gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == spawn.MonsterNumber && m.IsActive);

    private static float GetDefinitionHealth(MonsterDefinition definition)
        => definition.Attributes.FirstOrDefault(attribute => attribute.AttributeDefinition == Stats.MaximumHealth)?.Value ?? 0;

    /// <summary>
    /// Gets the player who killed: the killer itself, or the owner of the summon or the pet which killed,
    /// e.g. the elf whose summon hit last.
    /// </summary>
    /// <param name="killer">The killer.</param>
    /// <returns>The player, or <c>null</c> when no player killed.</returns>
    private static Player? GetKillingPlayer(IAttacker? killer)
        => killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;

    /// <summary>
    /// Gets the players who get the prize of a boss: the humans of the killer's party on the map of the
    /// boss, or the killer alone. Without a human, the boxes are free for all.
    /// </summary>
    /// <param name="killer">The killer of the boss.</param>
    /// <param name="map">The map of the boss.</param>
    /// <returns>The players.</returns>
    private static IReadOnlyList<Player> GetPrizeOwners(Player killer, GameMap map)
    {
        IEnumerable<Player> candidates = killer.Party?.PartyList.OfType<Player>() is { } members ? members : [killer];
        return candidates.Where(player => player.IsHuman() && player.CurrentMap == map).Distinct().ToList();
    }

    private static async ValueTask<string?> DropPrizeAsync(IGameContext gameContext, GameMap map, Point position, BossInvasionSpawn spawn, IReadOnlyList<Player> owners, ILogger logger)
    {
        if (spawn.PrizeBoxes <= 0)
        {
            return null;
        }

        if (gameContext.Configuration.Items.FirstOrDefault(item => item.Group == BoxOfKundun.Group && item.Number == BoxOfKundun.Number) is not { } definition)
        {
            logger.LogWarning("The Box of Kundun isn't configured, so the boss gave no prize.");
            return null;
        }

        var plus = Math.Clamp(spawn.PrizeBoxPlus, 1, BoxOfKundun.HighestPlus);
        for (var i = 0; i < spawn.PrizeBoxes; i++)
        {
            var box = new TemporaryItem { Definition = definition, Level = BoxOfKundun.LevelOf(plus), Durability = 1 };
            var boxPosition = i == 0 ? position : map.Terrain.GetRandomCoordinate(position, PrizeSpread);
            await map.AddAsync(new DroppedItem(box, boxPosition, map, null, owners.Count > 0 ? owners : null)).ConfigureAwait(false);
        }

        return $"{spawn.PrizeBoxes} {BoxOfKundun.NameOf(plus)}";
    }

    private static async ValueTask RemoveAsync(Monster monster)
    {
        // A monster which died removed itself, and its id may already belong to another object.
        if (monster.IsDisposed || monster.IsDisposing)
        {
            return;
        }

        await monster.CurrentMap.RemoveAsync(monster).ConfigureAwait(false);
        monster.Dispose();
    }

    private async ValueTask<List<string>> SpawnOnMapAsync(BossInvasionState state, ushort mapNumber, IReadOnlyList<BossInvasionSpawn> spawns, BossInvasionConfiguration configuration, ILogger logger)
    {
        var bosses = new List<string>();
        if (await state.Context.GetMapAsync(mapNumber).ConfigureAwait(false) is not { } map
            || MapPlaces.GetReachablePlaces(map.Terrain) is not { Length: > 0 } places)
        {
            logger.LogWarning("The map {Map} of the boss invasion doesn't exist or has no free place.", mapNumber);
            return bosses;
        }

        var center = places[Rand.NextInt(0, places.Length)];
        var placesAround = GetPlacesAround(places, center, configuration.EscortRadius);
        var mapName = map.Definition.Name.ValueInNeutralLanguage;
        var escorts = spawns.Where(spawn => !spawn.IsBoss).ToList();
        var isFirst = true;
        foreach (var spawn in spawns.Where(spawn => spawn.IsBoss || !spawn.OnlyCalled))
        {
            if (GetActiveDefinition(state.Context, spawn) is not { } definition)
            {
                logger.LogWarning("The monster {Monster} of the boss invasion isn't configured or isn't active.", spawn.MonsterNumber);
                continue;
            }

            for (var i = 0; i < spawn.Count; i++)
            {
                var position = isFirst ? center : placesAround[Rand.NextInt(0, placesAround.Length)];
                isFirst = false;
                INpcIntelligence intelligence = spawn is { IsBoss: true, HasSkills: true }
                    ? new InvasionBossIntelligence(configuration, state.Context, boss => this.CallEscortAsync(state, map, places, escorts, configuration, boss, logger))
                    : new BasicMonsterIntelligence();
                if (await this.SpawnAsync(state, map, definition, spawn, position, intelligence, configuration, logger).ConfigureAwait(false) is not null && spawn.IsBoss)
                {
                    bosses.Add($"{definition.Designation.ValueInNeutralLanguage} ({mapName}: {position.X}, {position.Y})");
                }
            }
        }

        return bosses;
    }

    /// <summary>
    /// Calls the escort of a map to its boss with skills: each escort of the map comes again around the
    /// boss, also the ones which come only when called.
    /// </summary>
    private async ValueTask CallEscortAsync(BossInvasionState state, GameMap map, Point[] places, IReadOnlyList<BossInvasionSpawn> escorts, BossInvasionConfiguration configuration, Monster boss, ILogger logger)
    {
        // A boss which left with the end of the run calls nobody, who would stay after it.
        if (state.HasEnded || !state.Monsters.ContainsKey(boss))
        {
            return;
        }

        try
        {
            var placesAround = GetPlacesAround(places, boss.Position, configuration.EscortRadius);
            if (placesAround.Length == 0)
            {
                placesAround = [boss.Position];
            }

            foreach (var spawn in escorts)
            {
                if (GetActiveDefinition(state.Context, spawn) is not { } definition)
                {
                    logger.LogWarning("The monster {Monster} of the boss invasion isn't configured or isn't active.", spawn.MonsterNumber);
                    continue;
                }

                for (var i = 0; i < spawn.Count; i++)
                {
                    var position = placesAround[Rand.NextInt(0, placesAround.Length)];
                    var escort = await this.SpawnAsync(state, map, definition, spawn, position, new BasicMonsterIntelligence(), configuration, logger).ConfigureAwait(false);

                    // The run ended while the escort came: the end may not have seen it, so it leaves now.
                    if (escort is not null && state.HasEnded)
                    {
                        if (state.Monsters.TryRemove(escort, out _))
                        {
                            await RemoveAsync(escort).ConfigureAwait(false);
                        }

                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't call the escort of the boss {Boss}.", boss.Definition.Designation.ValueInNeutralLanguage);
        }
    }

    private async ValueTask<Monster?> SpawnAsync(BossInvasionState state, GameMap map, MonsterDefinition definition, BossInvasionSpawn spawn, Point position, INpcIntelligence intelligence, BossInvasionConfiguration configuration, ILogger logger)
    {
        var gameContext = state.Context;
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
            MaximumHealthOverride = GetHealth(spawn, GetDefinitionHealth(definition), configuration.BossHealthMultiplier),
        };
        var monster = new Monster(area, definition, map, gameContext.DropGenerator, intelligence, gameContext.PlugInManager, gameContext.PathFinderPool);
        try
        {
            monster.Initialize();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Couldn't put the monster {Monster} at {Position} of the map {Map}.", definition.Number, position, map.MapId);
            monster.Dispose();
            return null;
        }

        try
        {
            await map.AddAsync(monster).ConfigureAwait(false);
        }
        catch
        {
            monster.Dispose();
            throw;
        }

        // Known as soon as it's on the map, so that the end of the run removes it whatever happens next.
        state.Monsters[monster] = spawn;
        monster.OnSpawn();
        return monster;
    }

    private async ValueTask OnBossKilledAsync(BossInvasionState state, Monster boss, BossInvasionSpawn spawn, Player killer)
    {
        var configuration = this.Configuration ??= this.CreateDefaultConfiguration();
        var map = boss.CurrentMap;
        var owners = GetPrizeOwners(killer, map);
        var prize = await DropPrizeAsync(state.Context, map, boss.Position, spawn, owners, killer.Logger).ConfigureAwait(false);
        var fight = DateTime.UtcNow - state.AppearedUtc;
        var bossName = boss.Definition.Designation.ValueInNeutralLanguage;
        ActivityLog.Write("boss_kill", killer, new
        {
            theme = state.Theme?.Name,
            boss = bossName,
            map = map.MapId,
            seconds = (int)fight.TotalSeconds,
            party = owners.Select(player => player.Name).ToList(),
            prize,
        });
        await GlobalNotification.SendAsync(
            state.Context,
            configuration.DefeatedMessage,
            this.DefaultConfiguration.DefeatedMessage,
            killer.Logger,
            killer.Name,
            bossName,
            map.Definition.Name.ValueInNeutralLanguage,
            (int)Math.Ceiling(fight.TotalMinutes),
            prize ?? configuration.NoPrize.ValueInNeutralLanguage).ConfigureAwait(false);
    }
}
