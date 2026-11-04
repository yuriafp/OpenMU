// <copyright file="JewelBankPacketHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Auction;

/// <summary>
/// The requests of the window of the Banqueira (<see cref="JewelBankRequest"/>), from a player who
/// talks to her, and of the window of Otto (<see cref="AuctionRequest"/>, C1 FB 05). The head code
/// 0xFB is free in OpenMU and MuMain; of our client's packets with it, only C1 FB 03 and C1 FB 05 go
/// to the server, so a later one gets its sub-code here.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Pedidos da Banqueira", Description = "Handles the requests of our client with the head code FB: of the window of the Banqueira (C1 FB 03: keep the jewels of a kind or all, take out a jewel or a pack; needs the plugin 'TBT: Banqueira') and of Otto (C1 FB 05; needs the plugin 'TBT: Leilão do Otto').")]
[Guid("A4E81C37-6B2D-4F09-9E5A-D17C3B8F2E60")]
public class JewelBankPacketHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => JewelBankRequest.Code;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        // OpenMU has one handler per head code: the requests of Otto come here, too.
        if (packet.Length > 3 && packet.Span[3] == AuctionRequest.SubCode)
        {
            await HandleAuctionAsync(player, packet).ConfigureAwait(false);
            return;
        }

        if (JewelBankRequest.Read(packet.Span) is not { } request
            || JewelBankPlugIn.GetActiveConfiguration(player) is not { } configuration
            || !player.IsHuman()
            || player.PlayerState.CurrentState != PlayerState.NpcDialogOpened
            || player.OpenedNpc?.Definition.Number != configuration.NpcNumber)
        {
            return;
        }

        try
        {
            await JewelBanker.HandleAsync(player, request, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't handle the request {Request} of {Character} at the Banqueira.", request, player.Name);
            await player.ShowMessageAsync("A Banqueira não pode atender agora. Tente de novo mais tarde.").ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleAuctionAsync(Player player, Memory<byte> packet)
    {
        if (AuctionRequest.Read(packet.Span) is not { } request
            || AuctionPlugIn.GetActiveConfiguration(player) is not { } configuration
            || !player.IsHuman()
            || player.PlayerState.CurrentState != PlayerState.NpcDialogOpened
            || player.OpenedNpc?.Definition.Number != configuration.NpcNumber)
        {
            return;
        }

        try
        {
            await Auctioneer.HandleAsync(player, request, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't handle the request {Request} of {Character} at Otto.", request, player.Name);
            await player.ShowMessageAsync(Auctioneer.Unavailable).ConfigureAwait(false);
            await Auctioneer.RefuseAsync(player, request.Action).ConfigureAwait(false);
        }
    }
}
