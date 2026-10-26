// <copyright file="EventGoblinPointsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.GoblinPoints;

/// <summary>
/// The configuration of the <see cref="EventGoblinPointsPlugIn"/>.
/// </summary>
public class EventGoblinPointsConfiguration
{
    /// <summary>
    /// Gets or sets the Goblin Points for staying in an event until its end; the same as an hour
    /// online gives, so an event night pays about a ticket (20 GP).
    /// </summary>
    [Display(Name = "Points", Description = "The Goblin Points for staying in an event until its end.")]
    [Range(0, 1000)]
    public int Points { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Goblin Points which the winner of an event gets on top, e.g. who delivers the
    /// weapon in the Blood Castle.
    /// </summary>
    [Display(Name = "Winner Points", Description = "The Goblin Points which the winner of an event gets on top.")]
    [Range(0, 1000)]
    public int WinnerPoints { get; set; } = 10;

    /// <summary>
    /// Gets or sets the message; <c>{0}</c> is the points and <c>{1}</c> the event.
    /// </summary>
    [Display(Name = "Message", Description = "The message; {0} is the points and {1} the event.")]
    public string Message { get; set; } = "Você ganhou {0} Goblin Points no {1}.";
}
