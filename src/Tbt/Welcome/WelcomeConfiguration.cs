// <copyright file="WelcomeConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Welcome;

/// <summary>
/// The configuration of the <see cref="WelcomePlugIn"/>. An empty text isn't shown.
/// </summary>
public class WelcomeConfiguration
{
    /// <summary>
    /// Gets or sets the message for all other players when a human enters the world; <c>{0}</c> is the character name.
    /// </summary>
    [Display(Name = "Entry Message", Description = "Shown to all other players when a human enters the world; {0} is the character name.")]
    public string EntryMessage { get; set; } = "{0} entrou no jogo.";

    /// <summary>
    /// Gets or sets the first tip for the player who entered the world.
    /// </summary>
    [Display(Name = "First Tip", Description = "The first tip for the player who entered the world.")]
    public string FirstTip { get; set; } = "Dica: o Elf Soldier (Lorencia, Devias e Noria) dá buff até o nível 220 e quests de caça.";

    /// <summary>
    /// Gets or sets the second tip for the player who entered the world.
    /// </summary>
    [Display(Name = "Second Tip", Description = "The second tip for the player who entered the world.")]
    public string SecondTip { get; set; } = "Dica: a Oficina, em Lorencia, põe Luck e até 3 opções excelentes em itens até +11.";

    /// <summary>
    /// Gets or sets the message with the next events; <c>{0}</c> is the list of the events.
    /// </summary>
    [Display(Name = "Next Events Message", Description = "The message with the next events; {0} is the list of the events.")]
    public string NextEventsMessage { get; set; } = "Próximos eventos: {0}.";

    /// <summary>
    /// Gets or sets the number of events in the <see cref="NextEventsMessage"/>.
    /// </summary>
    [Display(Name = "Next Events Count", Description = "The number of events in the next events message.")]
    [Range(1, 10)]
    public int NextEventsCount { get; set; } = 3;
}
