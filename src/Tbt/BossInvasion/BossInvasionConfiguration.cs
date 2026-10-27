// <copyright file="BossInvasionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.BossInvasion;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of a boss invasion. At each time of the <see cref="PeriodicTaskConfiguration.Timetable"/>,
/// the theme of the week is announced; after the <see cref="PeriodicTaskConfiguration.PreStartMessageDelay"/>
/// its bosses appear with their escort, and the ones nobody killed leave after the
/// <see cref="PeriodicTaskConfiguration.TaskDuration"/>.
/// </summary>
public class BossInvasionConfiguration : PeriodicTaskConfiguration
{
    /// <summary>
    /// The boxes which a boss drops for the party which kills it.
    /// </summary>
    private const int BossPrizeBoxes = 3;

    /// <summary>
    /// The plus of the boxes of a mid game boss.
    /// </summary>
    private const int MidGamePrizeBoxPlus = 4;

    /// <summary>
    /// The number of each kind of escort.
    /// </summary>
    private const int EscortCount = 4;

    /// <summary>
    /// Gets or sets the themes, one per week in turn.
    /// </summary>
    [Display(Name = "Themes", Description = "The themes, one per week in turn.", Order = 100)]
    public IList<BossInvasionTheme> Themes { get; set; } = new List<BossInvasionTheme>();

    /// <summary>
    /// Gets or sets the distance around a boss in which its escorts appear.
    /// </summary>
    [Display(Name = "Escort Radius", Description = "The distance around a boss in which its escorts appear.", Order = 101)]
    [Range(1, 20)]
    public int EscortRadius { get; set; } = 6;

    /// <summary>
    /// Gets or sets the message to all when the bosses appeared; <c>{0}</c> is the theme and <c>{1}</c>
    /// the bosses with their places.
    /// </summary>
    [Display(Name = "Appeared Message", Description = "The message to all when the bosses appeared; {0} is the theme and {1} the bosses with their places.", Order = 102)]
    public LocalizedString AppearedMessage { get; set; }

    /// <summary>
    /// Gets or sets the message to all when a boss died; <c>{0}</c> is the killer, <c>{1}</c> the boss,
    /// <c>{2}</c> the map, <c>{3}</c> the minutes of the fight and <c>{4}</c> the prize.
    /// </summary>
    [Display(Name = "Defeated Message", Description = "The message to all when a boss died; {0} is the killer, {1} the boss, {2} the map, {3} the minutes of the fight and {4} the prize.", Order = 103)]
    public LocalizedString DefeatedMessage { get; set; }

    /// <summary>
    /// Gets or sets what the message of a boss without prize boxes names as its prize.
    /// </summary>
    [Display(Name = "No Prize", Description = "What the message of a boss without prize boxes names as its prize.", Order = 104)]
    public LocalizedString NoPrize { get; set; }

    /// <summary>
    /// Gets or sets how many times the health of its definition a boss gets, when its spawn has no
    /// health of its own.
    /// </summary>
    /// <remarks>
    /// 20 times, the choice of the user in 2026-10-27, when the bosses died too easily for the end game
    /// characters: a first guess, to calibrate with the registro (PLANO-SERVIDOR.md, section 8).
    /// </remarks>
    [Display(Name = "Boss Health Multiplier", Description = "How many times the health of its definition a boss gets, when its spawn has no health of its own.", Order = 105)]
    [Range(1, 1000)]
    public int BossHealthMultiplier { get; set; } = 20;

    /// <summary>
    /// Gets or sets how often a boss with skills uses one: in turn, a hit around itself, a poison and an
    /// ice storm around its target.
    /// </summary>
    [Display(Name = "Boss Skill Interval", Description = "How often a boss with skills uses one: in turn, a hit around itself, a poison and an ice storm around its target.", Order = 106)]
    public TimeSpan SkillInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the distance in which the skills of a boss hit.
    /// </summary>
    [Display(Name = "Boss Skill Radius", Description = "The distance in which the skills of a boss hit.", Order = 107)]
    [Range(1, 10)]
    public int SkillRadius { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of the skill whose poison the poison of a boss applies, e.g. Poison (1).
    /// </summary>
    [Display(Name = "Boss Poison Skill", Description = "The number of the skill whose poison the poison of a boss applies, e.g. Poison (1).", Order = 108)]
    public short PoisonSkillNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of the skill whose ice the ice storm of a boss applies, e.g. Ice (7), which
    /// slows.
    /// </summary>
    [Display(Name = "Boss Ice Skill", Description = "The number of the skill whose ice the ice storm of a boss applies, e.g. Ice (7), which slows.", Order = 109)]
    public short IceSkillNumber { get; set; } = 7;

    /// <summary>
    /// Gets or sets the health, in percent, at which a boss with skills calls the escort of its map
    /// again; 0 is never.
    /// </summary>
    [Display(Name = "Escort Call Health (%)", Description = "The health, in percent, at which a boss with skills calls the escort of its map again; 0 is never.", Order = 110)]
    [Range(0, 100)]
    public int EscortCallHealthPercentage { get; set; } = 50;

    /// <summary>
    /// Gets or sets the health, in percent, at which a boss with skills calls the escort of its map a
    /// last time; 0 is never.
    /// </summary>
    [Display(Name = "Last Escort Call Health (%)", Description = "The health, in percent, at which a boss with skills calls the escort of its map a last time; 0 is never.", Order = 111)]
    [Range(0, 100)]
    public int LastEscortCallHealthPercentage { get; set; } = 25;

    /// <summary>
    /// Gets or sets the health, in percent, below which a boss with skills rages; 0 is never.
    /// </summary>
    [Display(Name = "Fury Health (%)", Description = "The health, in percent, below which a boss with skills rages; 0 is never.", Order = 112)]
    [Range(0, 100)]
    public int FuryHealthPercentage { get; set; } = 30;

    /// <summary>
    /// Gets or sets how much harder, in percent, a raging boss hits.
    /// </summary>
    [Display(Name = "Fury Damage Bonus (%)", Description = "How much harder, in percent, a raging boss hits.", Order = 113)]
    [Range(0, 1000)]
    public int FuryDamageBonus { get; set; } = 50;

    /// <summary>
    /// Gets or sets how much faster, in percent, a raging boss attacks and uses its skills.
    /// </summary>
    [Display(Name = "Fury Speed Bonus (%)", Description = "How much faster, in percent, a raging boss attacks and uses its skills.", Order = 114)]
    [Range(0, 300)]
    public int FurySpeedBonus { get; set; } = 50;

    /// <summary>
    /// Gets the default configuration of the mid game invasion (PLANO-SERVIDOR.md, item 13 of 9.5): on
    /// Wednesdays at 21:30 in Brasília (Thursday 00:30 in UTC), the bosses of the second wings' maps,
    /// and Lord Silvester and the Reaper of Season 8, with skills, with the monsters of the maps they
    /// invade.
    /// </summary>
    /// <returns>The default configuration.</returns>
    public static BossInvasionConfiguration CreateMidGameDefault() => CreateDefault(
        WeekDays.Thursday,
        [
            Theme(
                "Fênix da Escuridão em Icarus",
                BossInvasionTheme.Boss(Monsters.DarkPhoenix, Maps.Icarus, BossPrizeBoxes, MidGamePrizeBoxPlus),
                BossInvasionTheme.Escort(Monsters.GreatDrakan, Maps.Icarus, EscortCount),
                BossInvasionTheme.Escort(Monsters.PhantomKnight, Maps.Icarus, EscortCount),
                BossInvasionTheme.Escort(Monsters.AlphaCrust, Maps.Icarus, EscortCount)),
            Theme(
                "Maine do Inferno em Aida",
                BossInvasionTheme.Boss(Monsters.HellMaine, Maps.Aida, BossPrizeBoxes, MidGamePrizeBoxPlus),
                BossInvasionTheme.Escort(Monsters.WitchQueen, Maps.Aida, EscortCount),
                BossInvasionTheme.Escort(Monsters.BlueGolem, Maps.Aida, EscortCount),
                BossInvasionTheme.Escort(Monsters.DeathRider, Maps.Aida, EscortCount)),
            Theme(
                "Erohim em Tarkan",
                BossInvasionTheme.Boss(Monsters.Erohim, Maps.Tarkan, BossPrizeBoxes, MidGamePrizeBoxPlus),
                BossInvasionTheme.Escort(Monsters.FireGolem, Maps.Tarkan, EscortCount),
                BossInvasionTheme.Escort(Monsters.QueenBee, Maps.Tarkan, EscortCount),
                BossInvasionTheme.Escort(Monsters.PoisonGolem, Maps.Tarkan, EscortCount)),
            Theme(
                "Lorde Silvester em Lost Tower",
                BossInvasionTheme.BossWithSkills(Monsters.LordSilvester, Maps.LostTower, BossPrizeBoxes, MidGamePrizeBoxPlus),
                BossInvasionTheme.Escort(Monsters.DeathKnight, Maps.LostTower, EscortCount),
                BossInvasionTheme.Escort(Monsters.CursedWizard, Maps.LostTower, EscortCount),
                BossInvasionTheme.Escort(Monsters.Balrog, Maps.LostTower, EscortCount)),
            Theme(
                "Ceifador na Dungeon",
                BossInvasionTheme.BossWithSkills(Monsters.Reaper, Maps.Dungeon, BossPrizeBoxes, MidGamePrizeBoxPlus),
                BossInvasionTheme.Escort(Monsters.Ghost, Maps.Dungeon, EscortCount),
                BossInvasionTheme.Escort(Monsters.EliteSkeleton, Maps.Dungeon, EscortCount),
                BossInvasionTheme.Escort(Monsters.SkeletonArcher, Maps.Dungeon, EscortCount)),
        ]);

    /// <summary>
    /// Gets the default configuration of the late game invasion (PLANO-SERVIDOR.md, item 13 of 9.5): on
    /// Saturdays at 21:30 in Brasília (Sunday 00:30 in UTC), the "Noite do MU", the bosses of the last
    /// maps of Season 6, and the five elementals of Season 8.
    /// </summary>
    /// <returns>The default configuration.</returns>
    public static BossInvasionConfiguration CreateLateGameDefault() => CreateDefault(
        WeekDays.Sunday,
        [
            Theme(
                "Exército de Kalima em Lorencia",
                BossInvasionTheme.Boss(Monsters.IllusionOfKundun7, Maps.Lorencia, BossPrizeBoxes, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Escort(Monsters.Schriker7, Maps.Lorencia, EscortCount),
                BossInvasionTheme.Escort(Monsters.Necron7, Maps.Lorencia, EscortCount),
                BossInvasionTheme.Escort(Monsters.DeathAngel7, Maps.Lorencia, EscortCount)),

            // Two bosses side by side, which share the boxes of one.
            Theme(
                "Cerco de Crywolf",
                BossInvasionTheme.Boss(Monsters.Balgass, Maps.Crywolf, BossPrizeBoxes - 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Boss(Monsters.DarkElf, Maps.Crywolf, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Escort(Monsters.WerewolfHero, Maps.Crywolf, EscortCount),
                BossInvasionTheme.Escort(Monsters.ScoutHero, Maps.Crywolf, EscortCount),
                BossInvasionTheme.Escort(Monsters.Balram, Maps.Crywolf, EscortCount / 2),
                BossInvasionTheme.Escort(Monsters.Soram, Maps.Crywolf, EscortCount / 2)),
            Theme(
                "Pesadelo de Kanturu",
                BossInvasionTheme.Boss(Monsters.Nightmare, Maps.KanturuRuins, BossPrizeBoxes, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Escort(Monsters.Genocider, Maps.KanturuRuins, EscortCount),
                BossInvasionTheme.Escort(Monsters.Dreadfear, Maps.KanturuRuins, EscortCount),
                BossInvasionTheme.Escort(Monsters.TwinTale, Maps.KanturuRuins, EscortCount)),
            Theme(
                "Gelo de Raklion",
                BossInvasionTheme.Boss(Monsters.Selupan, Maps.Raklion, BossPrizeBoxes, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Escort(Monsters.DarkIronKnight, Maps.Raklion, EscortCount),
                BossInvasionTheme.Escort(Monsters.DarkCoolutin, Maps.Raklion, EscortCount),
                BossInvasionTheme.Escort(Monsters.DarkGiant, Maps.Raklion, EscortCount)),
            Theme(
                "Guarda Imperial em Devias",
                BossInvasionTheme.Boss(Monsters.Raymond, Maps.Devias, BossPrizeBoxes, BoxOfKundun.HighestPlus),
                BossInvasionTheme.Escort(Monsters.HeadMounter, Maps.Devias, EscortCount),
                BossInvasionTheme.Escort(Monsters.DarkGhost, Maps.Devias, EscortCount),
                BossInvasionTheme.Escort(Monsters.Defender, Maps.Devias, EscortCount)),

            // A hunt: an elemental with skills, each with the box of the Dark Elf of the siege, on each of
            // five maps of its element, for one group which goes from map to map or for several groups.
            // They come alone, and call the two strongest monsters of their map.
            Theme(
                "Invasão elemental",
                BossInvasionTheme.BossWithSkills(Monsters.Sellihoden, Maps.Atlans, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.CalledEscort(Monsters.LizardKing, Maps.Atlans, EscortCount),
                BossInvasionTheme.CalledEscort(Monsters.SilverValkyrie, Maps.Atlans, EscortCount),
                BossInvasionTheme.BossWithSkills(Monsters.Ukanva, Maps.Tarkan, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.CalledEscort(Monsters.BeamKnight, Maps.Tarkan, EscortCount),
                BossInvasionTheme.CalledEscort(Monsters.Tantallos, Maps.Tarkan, EscortCount),
                BossInvasionTheme.BossWithSkills(Monsters.Silla, Maps.Icarus, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.CalledEscort(Monsters.GreatDrakan, Maps.Icarus, EscortCount),
                BossInvasionTheme.CalledEscort(Monsters.PhantomKnight, Maps.Icarus, EscortCount),
                BossInvasionTheme.BossWithSkills(Monsters.Normus, Maps.Noria, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.CalledEscort(Monsters.StoneGolem, Maps.Noria, EscortCount),
                BossInvasionTheme.CalledEscort(Monsters.Agon, Maps.Noria, EscortCount),
                BossInvasionTheme.BossWithSkills(Monsters.Muff, Maps.LostTower, 1, BoxOfKundun.HighestPlus),
                BossInvasionTheme.CalledEscort(Monsters.DeathKnight, Maps.LostTower, EscortCount),
                BossInvasionTheme.CalledEscort(Monsters.DeathGorgon, Maps.LostTower, EscortCount)),
        ]);

    private static BossInvasionConfiguration CreateDefault(WeekDays days, IList<BossInvasionTheme> themes) => new()
    {
        // 00:30 in UTC is 21:30 in Brasília, after the Chaos Castle (PLANO-SERVIDOR.md, 5.4).
        Timetable = [new TimeOnly(0, 30)],
        Days = days,
        PreStartMessageDelay = TimeSpan.FromMinutes(5),
        TaskDuration = TimeSpan.FromMinutes(30),
        StartMessage = "Em {0} minutos, invasão de chefes: {1}!",
        AppearedMessage = "{0}: {1}. Reúnam o grupo!",
        DefeatedMessage = "{0} derrotou {1} em {2} depois de {3} minutos de luta, e o grupo ganhou {4}!",
        NoPrize = "a glória",
        EndMessage = "Os chefes que ninguém derrotou se retiraram.",
        Themes = themes,
    };

    private static BossInvasionTheme Theme(string name, params BossInvasionSpawn[] spawns) => new()
    {
        Name = name,
        Spawns = spawns.ToList(),
    };

    /// <summary>
    /// The maps of the default themes.
    /// </summary>
    private static class Maps
    {
        public const short Lorencia = 0;
        public const short Dungeon = 1;
        public const short Devias = 2;
        public const short Noria = 3;
        public const short LostTower = 4;
        public const short Atlans = 7;
        public const short Tarkan = 8;
        public const short Icarus = 10;
        public const short Aida = 33;
        public const short Crywolf = 34;
        public const short KanturuRuins = 37;
        public const short Raklion = 57;
    }

    /// <summary>
    /// The monsters of the default themes, by their number in Season 6, and ours (1003 on) for the
    /// bosses of the Season 8 client, which our client shows with their models.
    /// </summary>
    private static class Monsters
    {
        public const short Ghost = 11;
        public const short SkeletonArcher = 15;
        public const short EliteSkeleton = 16;
        public const short Agon = 31;
        public const short StoneGolem = 32;
        public const short CursedWizard = 34;
        public const short DeathGorgon = 35;
        public const short Balrog = 38;
        public const short DeathKnight = 40;
        public const short LizardKing = 48;
        public const short SilverValkyrie = 52;
        public const short Tantallos = 58;
        public const short BeamKnight = 61;
        public const short PhantomKnight = 72;
        public const short AlphaCrust = 74;
        public const short GreatDrakan = 75;
        public const short DarkPhoenix = 77;
        public const short IllusionOfKundun7 = 275;
        public const short FireGolem = 291;
        public const short QueenBee = 292;
        public const short PoisonGolem = 293;
        public const short Erohim = 295;
        public const short WitchQueen = 304;
        public const short BlueGolem = 305;
        public const short DeathRider = 306;
        public const short HellMaine = 309;
        public const short ScoutHero = 314;
        public const short WerewolfHero = 315;
        public const short Balram = 316;
        public const short Soram = 317;
        public const short DeathAngel7 = 334;
        public const short Necron7 = 335;
        public const short Schriker7 = 337;
        public const short DarkElf = 340;
        public const short Balgass = 349;
        public const short Genocider = 357;
        public const short TwinTale = 359;
        public const short Dreadfear = 360;
        public const short Nightmare = 361;
        public const short Selupan = 459;
        public const short Raymond = 506;
        public const short DarkGhost = 515;
        public const short HeadMounter = 517;
        public const short Defender = 518;
        public const short DarkGiant = 563;
        public const short DarkCoolutin = 564;
        public const short DarkIronKnight = 565;
        public const short LordSilvester = 1003;
        public const short Sellihoden = 1004;
        public const short Ukanva = 1005;
        public const short Silla = 1006;
        public const short Normus = 1007;
        public const short Muff = 1008;
        public const short Reaper = 1009;
    }
}
