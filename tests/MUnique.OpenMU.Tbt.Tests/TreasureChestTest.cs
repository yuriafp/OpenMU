// <copyright file="TreasureChestTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using System.Globalization;
using System.Text.RegularExpressions;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Treasure;

/// <summary>
/// Tests the treasure chest: its default configuration, the jewels and the message.
/// </summary>
[TestFixture]
public class TreasureChestTest
{
    /// <summary>
    /// By default, the chests appear every 3 hours at a quarter to the hour, for 10 minutes, 10 in each
    /// of Lorencia, Noria and Devias, one in ten golden, with one jewel or, golden, three.
    /// </summary>
    [Test]
    public void DefaultConfigurationFollowsThePlan()
    {
        var configuration = TreasureChestConfiguration.CreateDefault();

        Assert.That(configuration.Timetable.Select(time => time.ToString("HH:mm", CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "00:45", "03:45", "06:45", "09:45", "12:45", "15:45", "18:45", "21:45" }));
        Assert.That(configuration.TaskDuration, Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(configuration.Maps, Is.EqualTo(new short[] { 0, 3, 2 }));
        Assert.That(configuration.ChestsPerMap, Is.EqualTo(10));
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


    /// <summary>
    /// A configuration saved before the number of chests per map existed gets 10 chests per map.
    /// </summary>
    [Test]
    public void ConfigurationWithoutChestsPerMapGetsTen()
    {
        var registerConverter = !JsonConverterRegistry.Converters.OfType<LocalizedStringJsonConverter>().Any();
        if (registerConverter)
        {
            JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        }

        try
        {
            var stored = new PlugInConfiguration();
            stored.SetConfiguration(new TreasureChestConfiguration { ChestsPerMap = 3 }, null);
            stored.CustomConfiguration = Regex.Replace(stored.CustomConfiguration!, @"""ChestsPerMap"":\s*\d+,?", string.Empty);
            Assume.That(stored.CustomConfiguration, Does.Not.Contain("ChestsPerMap"));

            Assert.That(stored.GetConfiguration<TreasureChestConfiguration>(null)?.ChestsPerMap, Is.EqualTo(10));
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
    /// The message names the cities only, the last one after an "e".
    /// </summary>
    [Test]
    public void CitiesAreListedInTheMessage()
    {
        Assert.That(TreasureChestPlugIn.ListCities(["Lorencia", "Noria", "Devias"]), Is.EqualTo("Lorencia, Noria e Devias"));
        Assert.That(TreasureChestPlugIn.ListCities(["Lorencia", "Noria"]), Is.EqualTo("Lorencia e Noria"));
        Assert.That(TreasureChestPlugIn.ListCities(["Lorencia"]), Is.EqualTo("Lorencia"));
        Assert.That(TreasureChestPlugIn.ListCities([]), Is.Empty);
    }

    /// <summary>
    /// The default message reads well with its arguments: the minutes, the cities and the chests per city.
    /// </summary>
    [Test]
    public void StartMessageTellsTheCitiesWithoutPlaces()
    {
        var message = TreasureChestConfiguration.CreateDefault().StartMessage.ValueInNeutralLanguage;

        Assert.That(string.Format(CultureInfo.InvariantCulture, message!, 10, "Lorencia, Noria e Devias", 10), Is.EqualTo("10 baús do tesouro apareceram em cada cidade por 10 minutos: Lorencia, Noria e Devias. Cada baú é de quem abrir primeiro!"));
    }

    /// <summary>
    /// On a big map, the chests are distinct and apart from each other.
    /// </summary>
    [Test]
    public void PlacesAreApartOnABigMap()
    {
        var places = Grid(100);
        var random = new Random(7);

        var picked = TreasureChestPlugIn.PickPlaces(places, 10, 8, random.Next);

        Assert.That(picked, Has.Count.EqualTo(10));
        Assert.That(picked, Is.Unique);
        Assert.That(picked, Is.SubsetOf(places));
        foreach (var place in picked)
        {
            Assert.That(picked.Where(other => other != place).Min(other => other.EuclideanDistanceTo(place)), Is.GreaterThanOrEqualTo(8));
        }
    }

    /// <summary>
    /// On a small map, the chests which don't fit apart go to the other free places, never twice to one.
    /// </summary>
    [Test]
    public void PlacesFillASmallMapWithoutRepeating()
    {
        var places = Grid(4);
        var random = new Random(7);

        Assert.That(TreasureChestPlugIn.PickPlaces(places, 10, 8, random.Next), Has.Count.EqualTo(10).And.Unique);
        Assert.That(TreasureChestPlugIn.PickPlaces(places, 30, 8, random.Next), Has.Count.EqualTo(16).And.Unique);
        Assert.That(TreasureChestPlugIn.PickPlaces([], 10, 8, random.Next), Is.Empty);
    }

    private static Point[] Grid(int size)
        => Enumerable.Range(0, size).SelectMany(x => Enumerable.Range(0, size).Select(y => new Point((byte)x, (byte)y))).ToArray();
}
