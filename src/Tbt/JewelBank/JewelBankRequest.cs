// <copyright file="JewelBankRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.JewelBank;

/// <summary>
/// Is sent by the client when: the player clicks a button of the window of the Banqueira.
/// Causes reaction on server side: the Banqueira keeps or gives out jewels, and answers with
/// <see cref="JewelBankContentsRef"/>.
/// </summary>
/// <remarks>
/// C1 08 FB 03, a packet of our MuMain client: the action (byte 4), the index of the jewel in
/// <see cref="BankJewel.All"/> (byte 5) and, for a withdrawal, the amount (byte 6).
/// </remarks>
/// <param name="Action">The action.</param>
/// <param name="JewelIndex">The index of the jewel; any value for <see cref="JewelBankAction.DepositAll"/>.</param>
/// <param name="Amount">The amount of a withdrawal: 1, 10 or 30.</param>
internal readonly record struct JewelBankRequest(JewelBankAction Action, byte JewelIndex, byte Amount)
{
    /// <summary>
    /// Gets the operation code of this data packet.
    /// </summary>
    public static byte Code => 0xFB;

    /// <summary>
    /// Gets the operation sub-code of this data packet.
    /// </summary>
    public static byte SubCode => 0x03;

    /// <summary>
    /// Gets the length of this data packet.
    /// </summary>
    public static int Length => 8;

    /// <summary>
    /// Reads the request.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The request; <c>null</c>, if the packet isn't one.</returns>
    public static JewelBankRequest? Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < Length || packet[0] != 0xC1 || packet[2] != Code || packet[3] != SubCode)
        {
            return null;
        }

        return new JewelBankRequest((JewelBankAction)packet[4], packet[5], packet[6]);
    }
}
