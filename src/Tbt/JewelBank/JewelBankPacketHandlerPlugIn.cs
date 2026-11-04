// <copyright file="JewelBankPacketHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The requests of the window of the Banqueira (<see cref="JewelBankRequest"/>), from a player who
/// talks to her. The head code 0xFB is free in OpenMU and MuMain; of our client's packets with it,
/// only C1 FB 03 goes to the server, so a later one gets its sub-code here.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Pedidos da Banqueira", Description = "Handles the requests of the window of the Banqueira in our client (C1 FB 03): keep the jewels of a kind or all, and take out a jewel or a pack. Needs the plugin 'TBT: Banqueira'.")]
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
}
