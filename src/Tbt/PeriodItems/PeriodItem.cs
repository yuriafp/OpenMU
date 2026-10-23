// <copyright file="PeriodItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.PeriodItems;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// An item with an expiry, as the game client gets it (<see cref="PeriodItemRef"/>).
/// </summary>
/// <param name="ItemCode">The item code: the group times <see cref="ItemsPerGroup"/>, plus the number.</param>
/// <param name="Slot">The slot of the item in the inventory, as the server numbers them: 0 to 11 equipped, then the bag.</param>
/// <param name="ExpireTime">The expiry, as Unix time in seconds (UTC).</param>
internal readonly record struct PeriodItem(ushort ItemCode, ushort Slot, int ExpireTime)
{
    /// <summary>
    /// The items per group in the item code. It isn't configuration: the client numbers its items so.
    /// </summary>
    internal const int ItemsPerGroup = 512;

    /// <summary>
    /// Creates the period item of an item in the inventory.
    /// </summary>
    /// <param name="definition">The definition of the item.</param>
    /// <param name="slot">The slot of the item in the inventory.</param>
    /// <param name="expiresAt">The time of expiry, in UTC.</param>
    /// <returns>The period item.</returns>
    public static PeriodItem Of(ItemDefinition definition, byte slot, DateTime expiresAt)
    {
        // The difference ignores the kind of the time, so a time without it counts as UTC, too.
        var expireTime = (int)((expiresAt - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond);
        return new PeriodItem((ushort)((definition.Group * ItemsPerGroup) + definition.Number), slot, expireTime);
    }
}
