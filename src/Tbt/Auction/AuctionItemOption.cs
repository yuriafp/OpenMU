// <copyright file="AuctionItemOption.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// An option of an <see cref="AuctionItem"/>.
/// </summary>
/// <param name="Option">The identifier of the <see cref="DataModel.Configuration.Items.IncreasableItemOption"/>.</param>
/// <param name="Level">The level of the option.</param>
/// <param name="Index">The index of the option, like the position of a socket.</param>
internal sealed record AuctionItemOption(Guid Option, int Level, int Index);
