// <copyright file="TreasureChestTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using System.Globalization;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Treasure;

/// <summary>
/// Tests the treasure chest: its default configuration, the jewels and the message.
/// </summary>
[TestFixture]
public class TreasureChestTest
{
    /// <summary>
    /// By default, the chests appear every 3 hours at a quarter to the hour, for 10 minutes, in
    /// Lorencia, Noria and Devias, one in ten golden, with one jewel or, golden, three.
    /// </summary>
    [Test]
    public void DefaultConfigurationFollowsThePlan()
    {
        var configuration = TreasureChestConfiguration.CreateDefault();

        Assert.That(configuration.Timetable.Select(time => time.ToString("HH:mm", CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "00:45", "03:45", "06:45", "09:45", "12:45", "15:45", "18:45", "21:45" }));
        Assert.That(configuration.TaskDuration, Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(configuration.Maps, Is.EqualTo(new short[] { 0, 3, 2 }));
        Assert.That((configuration.GoldenChance, configuration.Jewels, configuration.GoldenJewels), Is.EqualTo((10, 1, 3)));
        Assert.That((configuration.ChestNumber, configuration.GoldenChestNumber), Is.EqualTo(((short)1001, (short)1002)));
    }

    /// <summary>
    /// The configuration survives a save/load round-trip through the plug-in configuration storage, with
    /// the JSON converters which the server registers at startup.
    /// </summary>
    [Test]
    public void ConfigurationSurvivesRoundTrip()
    {
        var registerConverter = !JsonConverterRegistry.Converters.OfType<LocalizedStringJsonConverter>().Any();
        if (registerConverter)
        {
            JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        }

        try
        {
            var stored = new PlugInConfiguration();
            stored.SetConfiguration(TreasureChestConfiguration.CreateDefault(), null);

            var configuration = stored.GetConfiguration<TreasureChestConfiguration>(null);

            Assert.That(configuration?.Maps, Is.EqualTo(new short[] { 0, 3, 2 }));
            Assert.That(configuration?.OpenedMessage.ValueInNeutralLanguage, Is.EqualTo("{0} abriu o baú do tesouro em {1} e ganhou {2}!"));
        }
        finally
        {
            if (registerConverter)
            {
                JsonConverterRegistry.ClearConverters();
            }
        }
    }

    /// <summary>
    /// The jewels are drawn from Bless, Soul, Chaos, Life and Creation, one random index each.
    /// </summary>
    [Test]
    public void JewelsAreDrawnFromTheFiveJewels()
    {
        var indexes = new Queue<int>([0, 4, 2]);

        var jewels = TreasureChestPlugIn.DrawJewels(3, _ => indexes.Dequeue());

        Assert.That(jewels, Is.EqualTo(new[] { ItemConstants.JewelOfBless, ItemConstants.JewelOfCreation, ItemConstants.JewelOfChaos }));
        Assert.That(TreasureChestPlugIn.DrawJewels(0, _ => 0), Is.Empty);
    }

    /// <summary>
    /// The message counts the jewels of each kind.
    /// </summary>
    [Test]
    public void JewelsAreCountedInTheMessage()
    {
        Assert.That(TreasureChestPlugIn.Describe(["Jewel of Soul", "Jewel of Life", "Jewel of Soul"]), Is.EqualTo("2 Jewel of Soul, 1 Jewel of Life"));
    }
}
