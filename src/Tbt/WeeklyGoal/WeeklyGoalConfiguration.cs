// <copyright file="WeeklyGoalConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="WeeklyGoalPlugIn"/>.
/// </summary>
public class WeeklyGoalConfiguration
{
    /// <summary>
    /// Gets or sets the goals, one per week in turn. A change applies right away, also to the
    /// current week: a goal of another kind starts its count again.
    /// </summary>
    [Display(Name = "Goals", Description = "The goals, one per week in turn. A change applies right away, also to the current week.", Order = 1)]
    public IList<WeeklyGoalDefinition> Goals { get; set; } = new List<WeeklyGoalDefinition>();

    /// <summary>
    /// Gets or sets the experience bonus, in percent, for all humans in the world, from the moment
    /// the goal is reached until the week turns. It multiplies the experience rate, like the Happy
    /// Hour, so both stack. At most 255, the byte the window of the client gets it in.
    /// </summary>
    [Display(Name = "Experience Bonus Percent", Description = "The experience bonus, in percent, for all humans in the world, from the moment the goal is reached until the week turns. At most 255.", Order = 2)]
    [Range(0, byte.MaxValue)]
    public int ExperienceBonusPercent { get; set; }

    /// <summary>
    /// Gets or sets the Jewels of Bless of the reward of each account which counted for the goal.
    /// </summary>
    [Display(Name = "Jewels of Bless", Description = "The Jewels of Bless of the reward of each account which counted for the goal.", Order = 3)]
    [Range(0, 20)]
    public int JewelsOfBless { get; set; }

    /// <summary>
    /// Gets or sets the Jewels of Soul of the reward of each account which counted for the goal.
    /// </summary>
    [Display(Name = "Jewels of Soul", Description = "The Jewels of Soul of the reward of each account which counted for the goal.", Order = 4)]
    [Range(0, 20)]
    public int JewelsOfSoul { get; set; }

    /// <summary>
    /// Gets or sets the plus of the Box of Kundun of the reward, 1 to 5; 0 gives none.
    /// </summary>
    [Display(Name = "Box of Kundun Plus", Description = "The plus of the Box of Kundun of the reward, 1 to 5; 0 gives none.", Order = 5)]
    [Range(0, BoxOfKundun.HighestPlus)]
    public int BoxOfKundunPlus { get; set; }

    /// <summary>
    /// Gets or sets the Goblin Points of the reward.
    /// </summary>
    [Display(Name = "Goblin Points", Description = "The Goblin Points of the reward.", Order = 6)]
    [Range(0, 10_000)]
    public int GoblinPoints { get; set; }

    /// <summary>
    /// Gets or sets the step of the progress messages, in percent, e.g. 25 for 25%, 50% and 75%;
    /// 0 for none. Reaching the goal has a message of its own.
    /// </summary>
    [Display(Name = "Announcement Step", Description = "The step of the progress messages, in percent, e.g. 25 for 25%, 50% and 75%; 0 for none.", Order = 7)]
    [Range(0, 100)]
    public int AnnouncementStep { get; set; }

    /// <summary>
    /// Gets or sets the message to all when the week turns; <c>{0}</c> is the target, <c>{1}</c> the
    /// name of the goal, <c>{2}</c> the experience bonus and <c>{3}</c> the end of the week.
    /// </summary>
    [Display(Name = "New Goal Message", Description = "The message to all when the week turns; {0} is the target, {1} the name of the goal, {2} the experience bonus and {3} the end of the week.", Order = 100)]
    public LocalizedString NewGoalMessage { get; set; }

    /// <summary>
    /// Gets or sets the message to all at a step of the progress; <c>{0}</c> is the step, <c>{1}</c>
    /// the count, <c>{2}</c> the target and <c>{3}</c> the name of the goal.
    /// </summary>
    [Display(Name = "Progress Message", Description = "The message to all at a step of the progress; {0} is the step, {1} the count, {2} the target and {3} the name of the goal.", Order = 101)]
    public LocalizedString ProgressMessage { get; set; }

    /// <summary>
    /// Gets or sets the message to all when the goal is reached; <c>{0}</c> is the target, <c>{1}</c>
    /// the name of the goal, <c>{2}</c> the experience bonus and <c>{3}</c> the end of the week.
    /// </summary>
    [Display(Name = "Reached Message", Description = "The message to all when the goal is reached; {0} is the target, {1} the name of the goal, {2} the experience bonus and {3} the end of the week.", Order = 102)]
    public LocalizedString ReachedMessage { get; set; }

    /// <summary>
    /// Gets or sets the message on the entry while the goal isn't reached; <c>{0}</c> is the count,
    /// <c>{1}</c> the target, <c>{2}</c> the name of the goal and <c>{3}</c> what the account counted.
    /// </summary>
    [Display(Name = "Entry Message", Description = "The message on the entry while the goal isn't reached; {0} is the count, {1} the target, {2} the name of the goal and {3} what the account counted.", Order = 103)]
    public string EntryMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message on the entry after the goal was reached; <c>{0}</c> is the experience
    /// bonus and <c>{1}</c> the end of the week.
    /// </summary>
    [Display(Name = "Entry Reached Message", Description = "The message on the entry after the goal was reached; {0} is the experience bonus and {1} the end of the week.", Order = 104)]
    public string EntryReachedMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message of a given reward; <c>{0}</c> is the reward.
    /// </summary>
    [Display(Name = "Reward Message", Description = "The message of a given reward; {0} is the reward.", Order = 105)]
    public string RewardMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message when the reward doesn't fit into the inventory.
    /// </summary>
    [Display(Name = "No Space Message", Description = "The message when the reward doesn't fit into the inventory.", Order = 106)]
    public string NoSpaceMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the answer of <c>/meta</c> in a week without a goal.
    /// </summary>
    [Display(Name = "No Goal Message", Description = "The answer of /meta in a week without a goal.", Order = 107)]
    public string NoGoalMessage { get; set; } = string.Empty;

    /// <summary>
    /// Creates the default configuration (PLANO-SERVIDOR.md, 5.5): a first guess for 4 to 6 friends.
    /// </summary>
    /// <returns>The default configuration.</returns>
    public static WeeklyGoalConfiguration CreateDefault() => new()
    {
        Goals =
        [
            Goal(WeeklyGoalKind.BloodCastle, 20, "Blood Castles completos"),
            Goal(WeeklyGoalKind.DevilSquare, 20, "Devil Squares completos"),
            Goal(WeeklyGoalKind.ChaosCastle, 10, "entradas no Chaos Castle"),
            Goal(WeeklyGoalKind.Resets, 15, "resets do grupo"),
            Goal(WeeklyGoalKind.TreasureChests, 15, "baús do tesouro abertos"),
        ],
        ExperienceBonusPercent = 20,
        JewelsOfBless = 1,
        JewelsOfSoul = 1,
        BoxOfKundunPlus = 3,
        GoblinPoints = 100,
        AnnouncementStep = 25,
        NewGoalMessage = "Meta da semana: {0} {1}! Quem somar ganha prêmio, e ao bater, todos ganham +{2}% de exp até {3}. Veja com /meta.",
        ProgressMessage = "Meta da semana em {0}%: {1} de {2} {3}.",
        ReachedMessage = "Meta da semana batida: {0} {1}! +{2}% de exp para todos até {3}, e quem somou ganha o prêmio.",
        EntryMessage = "Meta da semana: {0} de {1} {2}, e você somou {3}. Veja com /meta.",
        EntryReachedMessage = "Meta da semana batida: +{0}% de exp até {1}.",
        RewardMessage = "Prêmio da meta da semana: {0}!",
        NoSpaceMessage = "Abra espaço no inventário para receber o prêmio da meta da semana.",
        NoGoalMessage = "Não há meta coletiva nesta semana.",
    };

    private static WeeklyGoalDefinition Goal(WeeklyGoalKind kind, int target, string name) => new()
    {
        Kind = kind,
        Target = target,
        Name = name,
    };
}
