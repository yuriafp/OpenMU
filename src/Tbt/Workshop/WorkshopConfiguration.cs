// <copyright file="WorkshopConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Workshop;

/// <summary>
/// The configuration of the Oficina (<see cref="WorkshopPlugIn"/>). The rates and the prices are
/// values for the calibration (PLANO-SERVIDOR.md, 5.2).
/// </summary>
public class WorkshopConfiguration
{
    /// <summary>
    /// Gets or sets the maximum level of the item which gets an option.
    /// </summary>
    [Display(Name = "Maximum Item Level", Description = "The maximum level of the item which gets an option.")]
    [Range(0, 15)]
    public byte MaximumItemLevel { get; set; } = 11;

    /// <summary>
    /// Gets or sets the maximum number of excellent options of an item.
    /// </summary>
    [Display(Name = "Maximum Excellent Options", Description = "The maximum number of excellent options of an item.")]
    [Range(1, 6)]
    public int MaximumExcellentOptions { get; set; } = 3;

    /// <summary>
    /// Gets or sets the success rate of the Luck, in percent.
    /// </summary>
    [Display(Name = "Luck Success Rate", Description = "The success rate of the Luck, in percent.")]
    [Range(0, 100)]
    public int LuckSuccessRate { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of Jewels of Soul for the Luck.
    /// </summary>
    [Display(Name = "Luck Jewels of Soul", Description = "The number of Jewels of Soul for the Luck.")]
    [Range(1, 30)]
    public int LuckJewelsOfSoul { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of the Luck, in zen.
    /// </summary>
    [Display(Name = "Luck Zen", Description = "The price of the Luck, in zen.")]
    [Range(0, int.MaxValue)]
    public int LuckZen { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the success rate of an excellent option, in percent.
    /// </summary>
    [Display(Name = "Excellent Success Rate", Description = "The success rate of an excellent option, in percent.")]
    [Range(0, 100)]
    public int ExcellentSuccessRate { get; set; } = 75;

    /// <summary>
    /// Gets or sets the number of Jewels of Creation per option of the item, counting the new one:
    /// with 1, the first option takes 1 jewel, the second 2 and the third 3.
    /// </summary>
    [Display(Name = "Jewels of Creation per Option", Description = "The Jewels of Creation per option of the item, counting the new one: with 1, the first option takes 1, the second 2 and the third 3.")]
    [Range(1, 10)]
    public int JewelsOfCreationPerOption { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of an excellent option per option of the item, counting the new one, in zen:
    /// with 2,000,000, the first option costs 2 million, the second 4 million and the third 6 million.
    /// </summary>
    [Display(Name = "Excellent Zen per Option", Description = "The price of an excellent option per option of the item, counting the new one: with 2,000,000, the first costs 2 million, the second 4 million and the third 6 million.")]
    [Range(0, 300_000_000)]
    public int ExcellentZenPerOption { get; set; } = 2_000_000;

    /// <summary>
    /// Gets or sets the maximum number of wing options of a wing, counting the one from the Chaos Machine.
    /// </summary>
    [Display(Name = "Maximum Wing Options", Description = "The maximum number of wing options of a wing, counting the one from the Chaos Machine.")]
    [Range(1, 4)]
    public int MaximumWingOptions { get; set; } = 2;

    /// <summary>
    /// Gets or sets the success rate of a wing option, in percent.
    /// </summary>
    [Display(Name = "Wing Success Rate", Description = "The success rate of a wing option, in percent.")]
    [Range(0, 100)]
    public int WingSuccessRate { get; set; } = 75;

    /// <summary>
    /// Gets or sets the number of Jewels of Creation for a wing option of a second wing or a cape
    /// which isn't one of the <see cref="ThirdWingNumbers"/>. Each option takes the same.
    /// </summary>
    [Display(Name = "Second Wing Jewels of Creation", Description = "The Jewels of Creation for a wing option of a second wing or a cape which isn't a third wing. Each option takes the same.")]
    [Range(1, 10)]
    public int SecondWingJewelsOfCreation { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of a wing option of a second wing or a cape which isn't one of the
    /// <see cref="ThirdWingNumbers"/>, in zen. Each option costs the same.
    /// </summary>
    [Display(Name = "Second Wing Zen", Description = "The price of a wing option of a second wing or a cape which isn't a third wing. Each option costs the same.")]
    [Range(0, 300_000_000)]
    public int SecondWingZen { get; set; } = 2_000_000;

    /// <summary>
    /// Gets or sets the number of Jewels of Creation for a wing option of one of the <see cref="ThirdWingNumbers"/>.
    /// Each option takes the same.
    /// </summary>
    [Display(Name = "Third Wing Jewels of Creation", Description = "The Jewels of Creation for a wing option of a third wing. Each option takes the same.")]
    [Range(1, 10)]
    public int ThirdWingJewelsOfCreation { get; set; } = 2;

    /// <summary>
    /// Gets or sets the price of a wing option of one of the <see cref="ThirdWingNumbers"/>, in zen.
    /// Each option costs the same.
    /// </summary>
    [Display(Name = "Third Wing Zen", Description = "The price of a wing option of a third wing. Each option costs the same.")]
    [Range(0, 300_000_000)]
    public int ThirdWingZen { get; set; } = 4_000_000;

    /// <summary>
    /// Gets or sets the numbers of the third wings in the item group 12: the Wing of Storm (36), of
    /// Eternal (37), of Illusion (38) and of Ruin (39), the Cape of Emperor (40), the Wing of
    /// Dimension (43) and the Cape of Overrule (50).
    /// </summary>
    /// <remarks>
    /// The client knows the third wings by the tier in its item data (<c>Group12_Wing.json</c>) and
    /// shows their price in the Oficina's recipe 6 of <c>Mix.bmd</c>; a change here needs the same
    /// change there. If they disagree, the Oficina refuses the mix before it consumes anything.
    /// </remarks>
    [Display(Name = "Third Wing Numbers", Description = "The numbers of the third wings in the item group 12, e.g. the Wing of Storm (36) and the Cape of Overrule (50).")]
    public IList<short> ThirdWingNumbers { get; set; } = new List<short> { 36, 37, 38, 39, 40, 43, 50 };
}
