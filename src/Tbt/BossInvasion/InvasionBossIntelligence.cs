// <copyright file="InvasionBossIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames.ImperialGuardian;
using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// The intelligence of a boss of the invasion which has skills (PLANO-SERVIDOR.md, item 13 of 9.5).
/// Additionally to the basic behavior, it uses a skill in the interval of the configuration: in turn, a
/// hit around itself, a poison and an ice storm around its target. It calls the escort of its map
/// again at two healths, and rages below a third one: it hits harder and faster.
/// </summary>
/// <remarks>
/// Like the intelligence of the monsters of the imperial guardian, a skill takes the place of the
/// normal attack of its tick. The client shows the skills by the monster skill packet; our client plays
/// their effects for our bosses from the Season 8 client.
/// </remarks>
public sealed class InvasionBossIntelligence : BasicMonsterIntelligence
{
    /// <summary>
    /// The number of the monster skill packet which shows the hit around the boss. It has to match our
    /// client (MuMain, <c>Engine/Object/ServerMonsters.cpp</c>), which plays its effect.
    /// </summary>
    internal const short ShockSkillNumber = 23;

    /// <summary>
    /// The number of the monster skill packet which shows the poison at a hit player; see
    /// <see cref="ShockSkillNumber"/>.
    /// </summary>
    internal const short PoisonSkillNumber = 34;

    /// <summary>
    /// The number of the monster skill packet which shows the ice storm at a hit player; see
    /// <see cref="ShockSkillNumber"/>.
    /// </summary>
    internal const short IceSkillNumber = 35;

    /// <summary>
    /// The number of the monster skill packet which shows the call of the escort; see
    /// <see cref="ShockSkillNumber"/>.
    /// </summary>
    internal const short EscortCallSkillNumber = 20;

    /// <summary>
    /// The number of the monster skill packet which shows the rage; see <see cref="ShockSkillNumber"/>.
    /// </summary>
    internal const short FurySkillNumber = 59;

    private const double OneHundredPercent = 100.0;

    private static readonly BossSkill[] SkillTurns = [BossSkill.Shock, BossSkill.Poison, BossSkill.Ice];

    private readonly BossInvasionConfiguration _configuration;
    private readonly IGameContext _gameContext;
    private readonly Func<Monster, ValueTask> _callEscortAsync;
    private readonly int[] _escortCallHealths;
    private readonly ConcurrentDictionary<short, ElementalEffect?> _elementalEffects = new();
    private int _escortCalls;
    private int _isInFury;
    private int _skillTurn;
    private DateTime _nextSkillUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvasionBossIntelligence"/> class.
    /// </summary>
    /// <param name="configuration">The configuration of the invasion, with the boss skills.</param>
    /// <param name="gameContext">The game context.</param>
    /// <param name="callEscortAsync">Calls the escort of the map of the boss to it.</param>
    public InvasionBossIntelligence(BossInvasionConfiguration configuration, IGameContext gameContext, Func<Monster, ValueTask> callEscortAsync)
    {
        this._configuration = configuration;
        this._gameContext = gameContext;
        this._callEscortAsync = callEscortAsync;
        this._escortCallHealths = GetEscortCallHealths(configuration);
    }

    private enum BossSkill
    {
        Shock,
        Poison,
        Ice,
    }

    /// <summary>
    /// Gets a value indicating whether the boss rages.
    /// </summary>
    public bool IsInFury => Volatile.Read(ref this._isInFury) != 0;

    /// <summary>
    /// Gets the healths, in percent, at which a boss calls its escort, from the highest one on; a health
    /// of 0 is never.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The healths.</returns>
    internal static int[] GetEscortCallHealths(BossInvasionConfiguration configuration)
        => new[] { configuration.EscortCallHealthPercentage, configuration.LastEscortCallHealthPercentage }
            .Where(health => health > 0)
            .OrderByDescending(health => health)
            .ToArray();

    /// <summary>
    /// Gets the number of escort calls which are due at a health: one for each call health which it
    /// reached.
    /// </summary>
    /// <param name="escortCallHealths">The healths of the calls, from the highest one on.</param>
    /// <param name="health">The health, in percent.</param>
    /// <returns>The number of calls.</returns>
    internal static int GetDueEscortCalls(IReadOnlyList<int> escortCallHealths, double health)
        => escortCallHealths.Count(callHealth => health <= callHealth);

    /// <summary>
    /// Gets how often an action of a raging boss comes: its interval, faster by the speed bonus.
    /// </summary>
    /// <param name="interval">The interval without rage.</param>
    /// <param name="speedBonus">The speed bonus, in percent.</param>
    /// <returns>The interval in rage.</returns>
    internal static TimeSpan GetFuryInterval(TimeSpan interval, int speedBonus)
        => interval / (1 + (speedBonus / OneHundredPercent));

    /// <inheritdoc />
    protected override void OnStart()
    {
        base.OnStart();
        this._nextSkillUtc = DateTime.UtcNow + this.GetSkillInterval();
    }

    /// <inheritdoc />
    /// <remarks>
    /// When the boss uses one of its skills, it doesn't attack normally in the same tick.
    /// </remarks>
    protected override async ValueTask<bool> CanAttackAsync()
    {
        var monster = this.Monster;
        var maximumHealth = monster.Attributes[Stats.MaximumHealth];
        var health = maximumHealth > 0 ? monster.Health * OneHundredPercent / maximumHealth : OneHundredPercent;
        await this.CallEscortAsync(monster, health).ConfigureAwait(false);
        await this.TryGetIntoFuryAsync(monster, health).ConfigureAwait(false);

        // The target of the last tick, checked like the basic intelligence checks a target it attacks.
        if (DateTime.UtcNow < this._nextSkillUtc
            || this.CurrentTarget is not { } target
            || !target.IsActive()
            || target is Player { IsInvisible: true }
            || target.IsTeleporting
            || target.IsAtSafezone()
            || !target.IsInRange(monster.Position, monster.Definition.AttackRange)
            || monster.IsAtSafezone()
            || !monster.HasLineOfSightTo(target))
        {
            return true;
        }

        this._nextSkillUtc = DateTime.UtcNow + this.GetSkillInterval();
        var skill = SkillTurns[this._skillTurn++ % SkillTurns.Length];
        await this.UseSkillAsync(monster, target, skill).ConfigureAwait(false);
        return false;
    }

    private static ValueTask ShowSkillAsync(Monster monster, IAttackable target, short skillNumber)
    {
        return monster.ForEachWorldObserverAsync<IImperialGuardianViewPlugIn>(p => p.ShowMonsterSkillAsync(monster, target, skillNumber), true);
    }

    private TimeSpan GetSkillInterval()
        => this.IsInFury ? GetFuryInterval(this._configuration.SkillInterval, this._configuration.FurySpeedBonus) : this._configuration.SkillInterval;

    private async ValueTask CallEscortAsync(Monster monster, double health)
    {
        var dueCalls = GetDueEscortCalls(this._escortCallHealths, health);
        var calls = Volatile.Read(ref this._escortCalls);
        while (calls < dueCalls)
        {
            if (Interlocked.CompareExchange(ref this._escortCalls, calls + 1, calls) == calls)
            {
                await ShowSkillAsync(monster, this.CurrentTarget ?? monster, EscortCallSkillNumber).ConfigureAwait(false);
                await this._callEscortAsync(monster).ConfigureAwait(false);
            }

            calls = Volatile.Read(ref this._escortCalls);
        }
    }

    private async ValueTask TryGetIntoFuryAsync(Monster monster, double health)
    {
        if (health >= this._configuration.FuryHealthPercentage
            || Interlocked.Exchange(ref this._isInFury, 1) != 0)
        {
            return;
        }

        var damageFactor = (float)(1 + (this._configuration.FuryDamageBonus / OneHundredPercent));
        foreach (var attribute in new[] { Stats.MinimumPhysBaseDmg, Stats.MaximumPhysBaseDmg })
        {
            monster.Attributes.AddElement(new SimpleElement(damageFactor, AggregateType.Multiplicate), attribute);
        }

        this.ChangeTickInterval(GetFuryInterval(monster.Definition.AttackDelay, this._configuration.FurySpeedBonus));
        this._nextSkillUtc = DateTime.UtcNow + this.GetSkillInterval();

        // The client shows the rage as the animation of a skill, like the imperial guardian.
        await ShowSkillAsync(monster, this.CurrentTarget ?? monster, FurySkillNumber).ConfigureAwait(false);
    }

    private async ValueTask UseSkillAsync(Monster monster, IAttackable target, BossSkill skill)
    {
        var (skillNumber, center, effectSkillNumber) = skill switch
        {
            BossSkill.Poison => (PoisonSkillNumber, target.Position, this._configuration.PoisonSkillNumber),
            BossSkill.Ice => (IceSkillNumber, target.Position, this._configuration.IceSkillNumber),
            _ => (ShockSkillNumber, monster.Position, default(short?)),
        };

        var players = monster.CurrentMap.GetAttackablesInRange(center, this._configuration.SkillRadius)
            .OfType<Player>()
            .Where(player => player.IsActive() && !player.IsAtSafezone() && !player.IsInvisible)
            .ToList();

        // The client plays the effect of the poison and the ice at the target of the packet, so each hit
        // player gets one; the hit around the boss is one effect at the boss.
        if (effectSkillNumber is null || players.Count == 0)
        {
            await ShowSkillAsync(monster, target, skillNumber).ConfigureAwait(false);
        }
        else
        {
            foreach (var player in players)
            {
                await ShowSkillAsync(monster, player, skillNumber).ConfigureAwait(false);
            }
        }

        var effect = effectSkillNumber is { } number ? this._elementalEffects.GetOrAdd(number, this.CreateElementalEffect, monster) : null;
        foreach (var player in players)
        {
            var hitInfo = await player.AttackByAsync(monster, null, false).ConfigureAwait(false);
            if (effect is { } elemental)
            {
                await player.TryApplyElementalEffectsAsync(monster, elemental.Skill, elemental.PowerUps, elemental.Duration, hitInfo).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates the effect of a skill, e.g. the poison of Poison, with its power-ups for this boss.
    /// </summary>
    /// <param name="skillNumber">The number of the skill.</param>
    /// <param name="monster">The boss.</param>
    /// <returns>The effect, or <c>null</c> when the skill isn't configured or has no effect.</returns>
    private ElementalEffect? CreateElementalEffect(short skillNumber, Monster monster)
    {
        ElementalEffect? effect = null;
        if (this._gameContext.Configuration.Skills.FirstOrDefault(definition => definition.Number == skillNumber) is { } skill
            && skill.MagicEffectDef is { Duration: { } duration } magicEffect
            && magicEffect.PowerUpDefinitions.All(powerUp => powerUp.Boost is not null && powerUp.TargetAttribute is not null))
        {
            effect = new ElementalEffect(
                skill,
                [.. magicEffect.PowerUpDefinitions.Select(powerUp => (powerUp.TargetAttribute!, monster.Attributes.CreateElement(powerUp)))],
                monster.Attributes.CreateDurationElement(duration));
        }
        else
        {
            this._gameContext.LoggerFactory.CreateLogger<InvasionBossIntelligence>().LogWarning("The skill {Skill} of the boss skills isn't configured or has no effect, so the boss hits without it.", skillNumber);
        }

        return effect;
    }

    /// <summary>
    /// The effect of a skill, with its power-ups for the boss.
    /// </summary>
    /// <param name="Skill">The skill.</param>
    /// <param name="PowerUps">The power-ups.</param>
    /// <param name="Duration">The duration.</param>
    private sealed record ElementalEffect(Skill Skill, IReadOnlyCollection<(AttributeDefinition Target, IElement Boost)> PowerUps, IElement Duration);
}
