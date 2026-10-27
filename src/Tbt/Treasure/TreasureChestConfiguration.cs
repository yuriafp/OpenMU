// <copyright file="TreasureChestConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Treasure;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="TreasureChestPlugIn"/>. The chests appear at the times of the
/// <see cref="PeriodicTaskConfiguration.Timetable"/>, and the unopened ones vanish after the
/// <see cref="PeriodicTaskConfiguration.TaskDuration"/>.
/// </summary>
public class TreasureChestConfiguration : PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets or sets the numbers of the maps with a chest each, e.g. Lorencia (0), Noria (3) and Devias (2).
    /// </summary>
    /// <remarks>
    /// Like <c>CrywolfEventDefinition</c>, a list of <see cref="short"/>: the admin panel has no field for
    /// <see cref="ushort"/>.
    /// </remarks>
    [Display(Name = "Maps", Description = "The numbers of the maps with a chest each, e.g. Lorencia (0), Noria (3) and Devias (2).", Order = 100)]
    public IList<short> Maps { get; set; } = new List<short>();

    /// <summary>
    /// Gets or sets the chance, in percent, that a chest is a golden one.
    /// </summary>
    [Display(Name = "Golden Chance", Description = "The chance, in percent, that a chest is a golden one.", Order = 101)]
    [Range(0, 100)]
    public int GoldenChance { get; set; }

    /// <summary>
    /// Gets or sets the number of jewels of a chest.
    /// </summary>
    [Display(Name = "Jewels", Description = "The number of jewels of a chest.", Order = 102)]
    [Range(0, 20)]
    public int Jewels { get; set; }

    /// <summary>
    /// Gets or sets the number of jewels of a golden chest.
    /// </summary>
    [Display(Name = "Golden Jewels", Description = "The number of jewels of a golden chest.", Order = 103)]
    [Range(0, 20)]
    public int GoldenJewels { get; set; }

    /// <summary>
    /// Gets or sets the number of the monster definition of the chest.
    /// </summary>
    [Display(Name = "Chest Number", Description = "The number of the monster definition of the chest, e.g. our Treasure Chest (1001).", Order = 104)]
    public short ChestNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of the monster definition of the golden chest.
    /// </summary>
    [Display(Name = "Golden Chest Number", Description = "The number of the monster definition of the golden chest, e.g. our Golden Treasure Chest (1002).", Order = 105)]
    public short GoldenChestNumber { get; set; }

    /// <summary>
    /// Gets or sets the message to all when a chest is opened; <c>{0}</c> is the player, <c>{1}</c> the
    /// map and <c>{2}</c> the jewels.
    /// </summary>
    [Display(Name = "Opened Message", Description = "The message to all when a chest is opened; {0} is the player, {1} the map and {2} the jewels.", Order = 106)]
    public LocalizedString OpenedMessage { get; set; }

    /// <summary>
    /// Gets the default configuration (PLANO-SERVIDOR.md, 5.4, decision 12): every 3 hours, one chest
    /// in Lorencia, Noria and Devias for 10 minutes, one in ten golden, with one jewel or, golden, three.
    /// The times keep a quarter of an hour before the events of the agenda: 18:45 and 21:45 in Brasília
    /// are the rounds of the night.
    /// </summary>
    /// <returns>The default configuration.</returns>
    public static TreasureChestConfiguration CreateDefault() => new()
    {
        Timetable = GenerateTimeSequence(TimeSpan.FromHours(3), new TimeOnly(0, 45)).ToList(),
        TaskDuration = TimeSpan.FromMinutes(10),
        StartMessage = "Baús do tesouro apareceram por {0} minutos: {1}. Quem abrir primeiro leva!",
        EndMessage = "Os baús do tesouro que ninguém abriu sumiram.",
        Maps = new List<short> { 0, 3, 2 },
        GoldenChance = 10,
        Jewels = 1,
        GoldenJewels = 3,
        ChestNumber = 1001,
        GoldenChestNumber = 1002,
        OpenedMessage = "{0} abriu o baú do tesouro em {1} e ganhou {2}!",
    };
}
