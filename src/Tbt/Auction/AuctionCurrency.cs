// <copyright file="AuctionCurrency.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

using MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// The currencies of the leilão: zen, or a jewel which the Banqueira keeps. In the packets, a currency
/// is a byte: 0 for zen, then the index of the jewel in <see cref="BankJewel.All"/> plus one. In
/// <c>tbt.auction_listing</c>, it's <see cref="ZenName"/> or the name of the jewel.
/// </summary>
internal static class AuctionCurrency
{
    /// <summary>The name of zen in <c>tbt.auction_listing</c>.</summary>
    internal const string ZenName = "zen";

    /// <summary>The code of zen in the packets.</summary>
    internal const byte Zen = 0;

    /// <summary>The code of the filter of the window which shows all currencies.</summary>
    internal const byte Any = 0xFF;

    /// <summary>
    /// Gets the name of a currency.
    /// </summary>
    /// <param name="code">The code of the currency.</param>
    /// <returns>The name; <c>null</c> for an unknown code.</returns>
    public static string? NameOf(byte code)
        => code == Zen ? ZenName : code <= BankJewel.All.Count ? BankJewel.All[code - 1].Name : null;

    /// <summary>
    /// Gets the code of a currency.
    /// </summary>
    /// <param name="name">The name of the currency.</param>
    /// <returns>The code; <c>null</c> for a name which this version doesn't know.</returns>
    public static byte? CodeOf(string name)
    {
        if (name == ZenName)
        {
            return Zen;
        }

        for (var i = 0; i < BankJewel.All.Count; i++)
        {
            if (BankJewel.All[i].Name == name)
            {
                return (byte)(i + 1);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the jewel of a currency.
    /// </summary>
    /// <param name="name">The name of the currency.</param>
    /// <returns>The jewel; <c>null</c> for zen.</returns>
    public static BankJewel? JewelOf(string name) => name == ZenName ? null : BankJewel.ByName(name);

    /// <summary>
    /// Writes a price for a message to the player, like "1.000.000 zen" or "10 Bless".
    /// </summary>
    /// <param name="name">The name of the currency.</param>
    /// <param name="price">The price.</param>
    /// <returns>The text.</returns>
    public static string Describe(string name, long price) => name switch
    {
        ZenName => $"{price:N0} zen",
        "LowerRefineStone" => $"{price:N0} Pedra de Refino Menor",
        "HigherRefineStone" => $"{price:N0} Pedra de Refino Maior",
        _ => $"{price:N0} {name}",
    };
}
