// <copyright file="StarterKitConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// The configuration of the <see cref="StarterKitPlugIn"/>.
/// </summary>
public class StarterKitConfiguration
{
    /// <summary>
    /// Gets or sets the number of days the kit lasts, from the first entry of the character into the world.
    /// </summary>
    [Display(Name = "Days", Description = "The number of days the kit lasts, from the first entry of the character into the world.")]
    [Range(1, 365)]
    public int Days { get; set; } = 10;

    /// <summary>
    /// Gets or sets the extra level-up points of a new human character. The reset replaces all
    /// points, so they last until the first reset.
    /// </summary>
    [Display(Name = "Level-Up Points", Description = "The extra level-up points of a new human character; the reset replaces them, like all points.")]
    [Range(0, 65535)]
    public int LevelUpPoints { get; set; } = 500;

    /// <summary>
    /// Gets or sets the time of creation from which on a character gets the kit, in UTC. The
    /// characters created before never get it.
    /// </summary>
    [Display(Name = "Created From (UTC)", Description = "Only the characters created from this time on (UTC) get the kit, on their first entry into the world.")]
    public DateTime CreatedFromUtc { get; set; } = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
}
