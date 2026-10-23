// <copyright file="MilestoneCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Tbt.Welcome;

/// <summary>
/// The milestones of the first cycle and of the resets (PLANO-SERVIDOR.md, 5.6), once per account.
/// The reward leads to the next activity: the cloak to the Blood Castle, the Jewel of Soul to the
/// quest of the second class, and the Jewel of Chaos to the reset and the wings.
/// </summary>
internal static class MilestoneCatalog
{
    /// <summary>The Box of Kundun +1 is the item 14/11 at level 8; +2 is level 9 and so on.</summary>
    private const byte BoxOfKundunPlusOneLevel = 8;

    private static readonly RewardItem JewelOfBless = new(14, 13);
    private static readonly RewardItem JewelOfSoul = new(14, 14);
    private static readonly RewardItem JewelOfLife = new(14, 16);
    private static readonly RewardItem JewelOfChaos = new(12, 15);
    private static readonly RewardItem LargeHealingPotions = new(14, 3, Count: 2);
    private static readonly RewardItem LargeManaPotions = new(14, 6, Count: 2);

    /// <summary>
    /// Gets the level milestones for the character of the player, ordered by level. The Blood Castle
    /// bands come from the mini game definitions, so the Magic Gladiator and the Dark Lord get them
    /// at their own levels.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The level milestones.</returns>
    public static IReadOnlyList<Milestone> GetLevelMilestones(Player player)
    {
        var isSpecialCharacter = player.SelectedCharacter?.IsSpecialCharacter() is true;
        var milestones = new List<Milestone>();
        foreach (var bloodCastle in player.GameContext.Configuration.MiniGameDefinitions
                     .Where(d => d.Type == MiniGameType.BloodCastle && !d.RequiresMasterClass && d.TicketItem is not null)
                     .OrderBy(d => d.GameLevel))
        {
            var ticket = bloodCastle.TicketItem!;
            milestones.Add(new Milestone(
                $"bc{bloodCastle.GameLevel}",
                isSpecialCharacter ? bloodCastle.MinimumSpecialCharacterLevel : bloodCastle.MinimumCharacterLevel,
                0,
                [new RewardItem(ticket.Group, ticket.Number, (byte)bloodCastle.TicketItemLevel)],
                $"{ticket.Name.ValueInNeutralLanguage} +{bloodCastle.TicketItemLevel}",
                $"Com ela você entra no Blood Castle {bloodCastle.GameLevel}. {BloodCastleTimes(player.GameContext)}"));
        }

        milestones.Add(new Milestone("level50", 50, 0, [JewelOfBless, LargeHealingPotions, LargeManaPotions], "Jewel of Bless e poções", null));
        milestones.Add(new Milestone(
            "level150",
            150,
            0,
            [JewelOfSoul],
            "Jewel of Soul",
            isSpecialCharacter ? null : "Fale com a Sevina, em Devias, para a quest da 2ª classe."));
        milestones.Add(new Milestone("level220", 220, 0, [JewelOfLife], "Jewel of Life", null));
        milestones.Add(new Milestone("level400", 400, 0, [JewelOfChaos], "Jewel of Chaos", "No nível 400, use /reset. A Jewel of Chaos serve para as asas, no Chaos Goblin."));
        return milestones.OrderBy(m => m.Level).ToList();
    }

    /// <summary>
    /// Gets the reset milestones up to the resets: a Box of Kundun +1 at the 1st, +2 at the 5th,
    /// +3 at the 10th and then every 10 resets.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <returns>The reset milestones.</returns>
    public static IEnumerable<Milestone> GetResetMilestones(int resets)
    {
        for (var reset = 1; reset <= resets; reset = NextRewardedReset(reset))
        {
            yield return GetResetMilestone(reset);
        }
    }

    /// <summary>
    /// Gets the next reset milestone after the resets.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <returns>The next reset milestone.</returns>
    public static Milestone GetNextResetMilestone(int resets) => GetResetMilestone(resets < 1 ? 1 : NextRewardedReset(resets));

    private static int NextRewardedReset(int reset) => reset switch
    {
        < 5 => 5,
        < 10 => 10,
        _ => ((reset / 10) + 1) * 10,
    };

    private static Milestone GetResetMilestone(int reset)
    {
        var plus = reset switch
        {
            1 => 1,
            5 => 2,
            _ => 3,
        };

        return new Milestone(
            $"reset{reset}",
            0,
            reset,
            [new RewardItem(14, 11, (byte)(BoxOfKundunPlusOneLevel + plus - 1))],
            $"Box of Kundun +{plus}",
            null);
    }

    private static string BloodCastleTimes(IGameContext gameContext)
    {
        var times = EventSchedule.GetTimes(gameContext, EventSchedule.BloodCastle).Select(TbtTime.Format).ToList();
        return times.Count switch
        {
            0 => string.Empty,
            1 => $"Blood Castle às {times[0]}.",
            _ => $"Blood Castle às {string.Join(", ", times.Take(times.Count - 1))} e {times[^1]}.",
        };
    }
}
