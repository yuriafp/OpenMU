// <copyright file="PeriodItemConnectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.PeriodItems;

using MUnique.OpenMU.Network;

/// <summary>
/// Sends the items with an expiry to the game client, like the generated <c>ConnectionExtensions</c> of OpenMU.
/// </summary>
internal static class PeriodItemConnectionExtensions
{
    /// <summary>
    /// Sends the classic period item list: a <see cref="PeriodItemCountRef"/> and a <see cref="PeriodItemRef"/> per item.
    /// Nothing is sent without items.
    /// </summary>
    /// <param name="connection">The connection, or <c>null</c> for a player without a game client.</param>
    /// <param name="items">The items, up to 255.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SendPeriodItemsAsync(this IConnection? connection, IReadOnlyList<PeriodItem> items)
    {
        if (connection is null || items.Count == 0)
        {
            return;
        }

        var count = Math.Min(items.Count, byte.MaxValue);

        // Written at once, so no other packet comes between the count and its items.
        int WritePackets()
        {
            var length = PeriodItemCountRef.Length + (count * PeriodItemRef.Length);
            var span = connection.Output.GetSpan(length)[..length];
            var countPacket = new PeriodItemCountRef(span[..PeriodItemCountRef.Length]);
            countPacket.Count = (byte)count;
            for (var i = 0; i < count; i++)
            {
                var packet = new PeriodItemRef(span.Slice(PeriodItemCountRef.Length + (i * PeriodItemRef.Length), PeriodItemRef.Length));
                packet.ItemCode = items[i].ItemCode;
                packet.Slot = items[i].Slot;
                packet.ExpireTime = items[i].ExpireTime;
            }

            return length;
        }

        await connection.SendAsync(WritePackets).ConfigureAwait(false);
    }
}
