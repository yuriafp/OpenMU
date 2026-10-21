// <copyright file="DropAnnouncementConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Drops;

/// <summary>
/// The configuration of the <see cref="DropAnnouncementPlugIn"/>.
/// </summary>
public class DropAnnouncementConfiguration
{
    /// <summary>
    /// Gets or sets the message; <c>{0}</c> is the character, <c>{1}</c> the item and <c>{2}</c> the map.
    /// </summary>
    [Display(Name = "Message", Description = "The message; {0} is the character, {1} the item and {2} the map.")]
    public string Message { get; set; } = "{0} dropou {1} em {2}!";

    /// <summary>
    /// Gets or sets a value indicating whether excellent items are announced.
    /// </summary>
    [Display(Name = "Announce Excellent Items")]
    public bool AnnounceExcellentItems { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ancient items are announced.
    /// </summary>
    [Display(Name = "Announce Ancient Items")]
    public bool AnnounceAncientItems { get; set; } = true;

    /// <summary>
    /// Gets or sets the lowest Box of Kundun which is announced, from +1 to +5; 0 announces none.
    /// The Golden Invasion drops about 60 boxes of +1 to +5.
    /// </summary>
    [Display(Name = "Minimum Box of Kundun", Description = "The lowest Box of Kundun which is announced, from +1 to +5; 0 announces none. The Golden Invasion drops about 60 boxes of +1 to +5.")]
    [Range(0, 5)]
    public int MinimumBoxOfKundun { get; set; } = 1;
}
