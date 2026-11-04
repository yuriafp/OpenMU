// <copyright file="AuctionItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using System.Runtime.CompilerServices;
using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence;

/// <summary>
/// An item which Otto keeps, whole: while it's for sale, it isn't in the data of OpenMU, and the
/// delivery creates it again (<see cref="Create"/>). The options and sets are the identifiers of
/// their configuration objects, so nothing of the item is lost; an item with something which the
/// configuration doesn't have isn't taken (<see cref="Of"/> answers <c>null</c>).
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number.</param>
/// <param name="Level">The level.</param>
/// <param name="Durability">The durability.</param>
/// <param name="HasSkill">Whether it has the skill.</param>
/// <param name="SocketCount">The number of sockets.</param>
/// <param name="PetExperience">The experience of a pet.</param>
/// <param name="Options">The options: the identifier of the <see cref="IncreasableItemOption"/>, its level and its index.</param>
/// <param name="Sets">The identifiers of the <see cref="ItemOfItemSet"/> of the item, like its ancient set.</param>
internal sealed record AuctionItem(
    byte Group,
    short Number,
    byte Level,
    double Durability,
    bool HasSkill,
    int SocketCount,
    int PetExperience,
    IReadOnlyList<AuctionItemOption> Options,
    IReadOnlyList<Guid> Sets)
{
    private static readonly ConditionalWeakTable<GameConfiguration, Lookup> Lookups = new();

    /// <summary>
    /// Takes the data of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The game configuration, which must know all options and sets of the item.</param>
    /// <returns>The data; <c>null</c>, if the configuration doesn't know something of the item, which then couldn't be created again.</returns>
    public static AuctionItem? Of(Item item, GameConfiguration configuration)
    {
        if (item.Definition is not { } definition)
        {
            return null;
        }

        var lookup = LookupOf(configuration);
        var options = new List<AuctionItemOption>();
        foreach (var link in item.ItemOptions)
        {
            if (link.ItemOption is not { } option || !lookup.Options.ContainsKey(option.GetId()))
            {
                return null;
            }

            options.Add(new AuctionItemOption(option.GetId(), link.Level, link.Index));
        }

        var sets = new List<Guid>();
        foreach (var set in item.ItemSetGroups)
        {
            if (!lookup.Sets.ContainsKey(set.GetId()))
            {
                return null;
            }

            sets.Add(set.GetId());
        }

        return new AuctionItem(definition.Group, definition.Number, item.Level, item.Durability, item.HasSkill, item.SocketCount, item.PetExperience, options, sets);
    }

    /// <summary>
    /// Reads the data from its JSON in <c>tbt.auction_listing</c>.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The data.</returns>
    public static AuctionItem FromJson(string json)
        => JsonSerializer.Deserialize<AuctionItem>(json) ?? throw new JsonException("An item of the leilão is empty.");

    /// <summary>
    /// Writes the data as JSON, for <c>tbt.auction_listing</c>.
    /// </summary>
    /// <returns>The JSON.</returns>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Creates the item again, in a persistence context.
    /// </summary>
    /// <param name="context">The persistence context, of the player which gets the item.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>The item, not in a storage yet; <c>null</c>, if the configuration lost something of it.</returns>
    public Item? Create(IContext context, GameConfiguration configuration)
    {
        var lookup = LookupOf(configuration);
        if (configuration.Items.FirstOrDefault(d => d.Group == this.Group && d.Number == this.Number) is not { } definition
            || this.Options.Any(o => !lookup.Options.ContainsKey(o.Option))
            || this.Sets.Any(s => !lookup.Sets.ContainsKey(s)))
        {
            return null;
        }

        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = this.Level;
        item.Durability = this.Durability;
        item.HasSkill = this.HasSkill;
        item.SocketCount = this.SocketCount;
        item.PetExperience = this.PetExperience;
        foreach (var option in this.Options)
        {
            var link = context.CreateNew<ItemOptionLink>();
            link.ItemOption = lookup.Options[option.Option];
            link.Level = option.Level;
            link.Index = option.Index;
            item.ItemOptions.Add(link);
        }

        foreach (var set in this.Sets)
        {
            item.ItemSetGroups.Add(lookup.Sets[set]);
        }

        return item;
    }

    private static Lookup LookupOf(GameConfiguration configuration) => Lookups.GetValue(configuration, c => new Lookup(c));

    /// <summary>
    /// The options and sets of a configuration, by their identifiers. The configuration doesn't change
    /// while the server runs, so it's built once.
    /// </summary>
    private sealed class Lookup
    {
        public Lookup(GameConfiguration configuration)
        {
            var options = configuration.ItemOptions.SelectMany(d => d.PossibleOptions)
                .Concat(configuration.Items.SelectMany(i => i.PossibleItemOptions).SelectMany(d => d.PossibleOptions))
                .Concat(configuration.ItemSetGroups.SelectMany(g => g.Options?.PossibleOptions ?? Enumerable.Empty<IncreasableItemOption>()))
                .Concat(configuration.ItemSetGroups.SelectMany(g => g.Items).Select(i => i.BonusOption).OfType<IncreasableItemOption>());
            foreach (var option in options)
            {
                // An object without an identifier couldn't be found again.
                if (option.GetId() != Guid.Empty)
                {
                    this.Options.TryAdd(option.GetId(), option);
                }
            }

            foreach (var set in configuration.ItemSetGroups.SelectMany(g => g.Items))
            {
                if (set.GetId() != Guid.Empty)
                {
                    this.Sets.TryAdd(set.GetId(), set);
                }
            }
        }

        public Dictionary<Guid, IncreasableItemOption> Options { get; } = new();

        public Dictionary<Guid, ItemOfItemSet> Sets { get; } = new();
    }
}
