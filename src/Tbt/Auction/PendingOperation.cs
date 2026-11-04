// <copyright file="PendingOperation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// An operation of a listing which isn't settled yet.
/// </summary>
/// <param name="Id">The identifier of the listing.</param>
/// <param name="Kind">The operation: <see cref="AuctionStore.ListKind"/>, <see cref="AuctionStore.BuyKind"/> or <see cref="AuctionStore.ReturnKind"/>.</param>
/// <param name="Session">The stay in the world which wrote it.</param>
/// <param name="ItemSaved">Whether its item is in the saved data of OpenMU.</param>
internal sealed record PendingOperation(long Id, string Kind, Guid Session, bool ItemSaved);
