// <copyright file="WorkshopCraftingHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Workshop;

using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Tbt.Milestones;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// The mix of the Oficina, the crafting of the NPC Oficina: one item of the groups 0 to 11, or one
/// wing, up to the configured level, and the jewels for the chosen option, which the client sends in
/// the socket slot byte of the mix request: 0 is the Luck, and 1 to 6 is the number of the excellent
/// option, or for a wing 1 to 4 the number of the wing option.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The Luck takes Jewels of Soul; an excellent option takes Jewels of Creation, more for every
/// option the item already has; a wing option takes the Jewels of Creation of its wing, the same for
/// every option. Anything else in the mix, or the wrong jewels, consumes nothing.</item>
/// <item>A failure consumes the jewels and the zen and keeps the item as it was.</item>
/// <item>Ancient and socket items don't get options here.</item>
/// </list>
/// The crafting refers to this class by its assembly qualified name,
/// <c>MUnique.OpenMU.Tbt.Workshop.WorkshopCraftingHandler, MUnique.OpenMU.Tbt</c>.
/// </remarks>
public class WorkshopCraftingHandler : IItemCraftingHandler
{
    /// <summary>The socket slot byte of the mix request which chooses the Luck.</summary>
    private const byte LuckChoice = 0;

    /// <summary>
    /// The item group of the third wings, which <see cref="WorkshopConfiguration.ThirdWingNumbers"/> numbers.
    /// Not configuration: every third wing of the item data is in this group (also the capes of the
    /// third tier, unlike the Cape of Lord of the group 13), so only the numbers can differ.
    /// </summary>
    private const byte ThirdWingGroup = 12;

    private static readonly (byte Group, short Number) JewelOfSoul = (14, 14);

    private static readonly (byte Group, short Number) JewelOfCreation = (14, 22);

    /// <inheritdoc />
    public async ValueTask<(CraftingResult Result, Item? Item)> DoMixAsync(Player player, byte socketSlot)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<WorkshopPlugIn>() is not { } plugIn || player.TemporaryStorage is not { } storage)
        {
            return (CraftingResult.IncorrectMixItems, null);
        }

        var configuration = plugIn.Configuration ??= new WorkshopConfiguration();
        if (GetMix(storage.Items.ToList(), socketSlot, configuration) is not { } mix)
        {
            return (CraftingResult.IncorrectMixItems, null);
        }

        if (mix.Error is { } error)
        {
            return (error, null);
        }

        if (!player.TryRemoveMoney(mix.Price))
        {
            return (CraftingResult.NotEnoughMoney, null);
        }

        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        var destroyedPlugIn = player.GameContext.PlugInManager.GetPlugInPoint<IItemDestroyedPlugIn>();
        foreach (var jewel in mix.Jewels)
        {
            await storage.RemoveItemAsync(jewel).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(jewel).ConfigureAwait(false);
            destroyedPlugIn?.ItemDestroyed(jewel);
        }

        var success = Rand.NextRandomBool(mix.SuccessRate);
        if (success)
        {
            var optionLink = player.PersistenceContext.CreateNew<ItemOptionLink>();
            optionLink.ItemOption = mix.Option;
            mix.Item.ItemOptions.Add(optionLink);
        }

        // As in the mixes of OpenMU: the backup of the inventory has the consumed jewels, so it must not be restored.
        player.BackupInventory = null;

        var itemName = mix.Item.Definition!.Name.ValueInNeutralLanguage;
        player.Logger.LogInformation("Oficina: {Option} on {Item} with {Rate} %: {Success}.", mix.OptionName, itemName, mix.SuccessRate, success);
        ActivityLog.Write("oficina", player, new { item = itemName, level = mix.Item.Level, option = mix.OptionName, rate = mix.SuccessRate, zen = mix.Price, success });
        if (!success)
        {
            return (CraftingResult.Failed, null);
        }

        await MilestonePlugIn.WorkshopOptionAddedAsync(player, mix.Item).ConfigureAwait(false);
        return (CraftingResult.Success, mix.Item);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The option can't be inferred from the items, so old clients which don't send the mix type
    /// (and the option) never select the Oficina.
    /// </remarks>
    public CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRateByItems)
    {
        items = [];
        successRateByItems = 0;
        return CraftingResult.IncorrectMixItems;
    }

    /// <summary>
    /// Gets the mix for the items and the choice, or <c>null</c> when the items don't fit the Oficina.
    /// </summary>
    /// <param name="items">The items in the mix.</param>
    /// <param name="choice">The choice: 0 is the Luck, and 1 to 6 is the number of the excellent option, or of the wing option for a wing.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The mix, or <c>null</c>.</returns>
    internal static Mix? GetMix(IReadOnlyList<Item> items, byte choice, WorkshopConfiguration configuration)
    {
        var targets = items.Where(i => i.Definition is { } definition && (definition.Group <= 11 || IsWing(definition))).ToList();
        var souls = items.Where(i => Is(i, JewelOfSoul)).ToList();
        var creations = items.Where(i => Is(i, JewelOfCreation)).ToList();
        if (targets.Count != 1 || targets.Count + souls.Count + creations.Count != items.Count)
        {
            return null;
        }

        var item = targets[0];
        var definition = item.Definition!;
        if (item.ItemSetGroups.Count > 0 || item.SocketCount > 0 || definition.MaximumSockets > 0)
        {
            return null;
        }

        CraftingResult? error = item.Level > configuration.MaximumItemLevel ? CraftingResult.InvalidItemLevel : null;
        if (choice == LuckChoice)
        {
            var luck = definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).FirstOrDefault(o => o.OptionType == ItemOptionTypes.Luck);
            if (luck is null || item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck) || creations.Count > 0)
            {
                return null;
            }

            error ??= GetJewelError(souls.Count, configuration.LuckJewelsOfSoul);
            return new Mix(item, luck, "Luck", souls, configuration.LuckZen, configuration.LuckSuccessRate, error);
        }

        if (souls.Count > 0)
        {
            return null;
        }

        return IsWing(definition)
            ? GetWingOptionMix(item, choice, creations, configuration, error)
            : GetExcellentOptionMix(item, choice, creations, configuration, error);
    }

    private static Mix? GetExcellentOptionMix(Item item, byte choice, IReadOnlyList<Item> creations, WorkshopConfiguration configuration, CraftingResult? error)
    {
        if (GetNewOption(item, ItemOptionTypes.Excellent, choice, configuration.MaximumExcellentOptions) is not { } newOption)
        {
            return null;
        }

        var optionCount = newOption.ExistingOptions + 1;
        error ??= GetJewelError(creations.Count, optionCount * configuration.JewelsOfCreationPerOption);
        return new Mix(item, newOption.Option, $"Excellent {choice}", creations, optionCount * configuration.ExcellentZenPerOption, configuration.ExcellentSuccessRate, error);
    }

    private static Mix? GetWingOptionMix(Item item, byte choice, IReadOnlyList<Item> creations, WorkshopConfiguration configuration, CraftingResult? error)
    {
        if (GetNewOption(item, ItemOptionTypes.Wing, choice, configuration.MaximumWingOptions) is not { } newOption)
        {
            return null;
        }

        var definition = item.Definition!;
        var (jewels, price) = definition.Group == ThirdWingGroup && configuration.ThirdWingNumbers.Contains(definition.Number)
            ? (configuration.ThirdWingJewelsOfCreation, configuration.ThirdWingZen)
            : (configuration.SecondWingJewelsOfCreation, configuration.SecondWingZen);
        error ??= GetJewelError(creations.Count, jewels);
        return new Mix(item, newOption.Option, $"Wing {choice}", creations, price, configuration.WingSuccessRate, error);
    }

    /// <summary>
    /// Gets the option of the type and number which the item can still get, with the number of
    /// options of that type it has, or <c>null</c> when the item has it already or has the maximum.
    /// </summary>
    private static (IncreasableItemOption Option, int ExistingOptions)? GetNewOption(Item item, ItemOptionType optionType, byte number, int maximumOptions)
    {
        var option = item.Definition!.PossibleItemOptions.SelectMany(o => o.PossibleOptions)
            .FirstOrDefault(o => o.OptionType == optionType && o.Number == number);
        var existingOptions = item.ItemOptions.Where(o => o.ItemOption?.OptionType == optionType).ToList();
        if (option is null
            || existingOptions.Any(o => o.ItemOption!.Number == number)
            || existingOptions.Count >= maximumOptions)
        {
            return null;
        }

        return (option, existingOptions.Count);
    }

    private static CraftingResult? GetJewelError(int count, int required) => count switch
    {
        _ when count < required => CraftingResult.LackingMixItems,
        _ when count > required => CraftingResult.IncorrectMixItems,
        _ => null,
    };

    private static bool Is(Item item, (byte Group, short Number) id)
        => item.Definition is { } definition && definition.Group == id.Group && definition.Number == id.Number;

    /// <summary>
    /// Whether the item is worn in the wings slot: the wings and the capes, also the Cape of Lord of the group 13.
    /// </summary>
    private static bool IsWing(ItemDefinition definition)
        => definition.ItemSlot?.ItemSlots.Contains(InventoryConstants.WingsSlot) == true;

    /// <summary>
    /// A mix of the Oficina.
    /// </summary>
    /// <param name="Item">The item which gets the option.</param>
    /// <param name="Option">The option.</param>
    /// <param name="OptionName">The name of the option, for the log and the registro.</param>
    /// <param name="Jewels">The jewels which the mix consumes.</param>
    /// <param name="Price">The price in zen.</param>
    /// <param name="SuccessRate">The success rate, in percent.</param>
    /// <param name="Error">The error which stops the mix, or <c>null</c>.</param>
    internal sealed record Mix(Item Item, IncreasableItemOption Option, string OptionName, IReadOnlyList<Item> Jewels, int Price, int SuccessRate, CraftingResult? Error);
}
