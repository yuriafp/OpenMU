// <copyright file="ActiveStarterKit.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

using System.Threading;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Tbt.PeriodItems;

/// <summary>
/// The starter kit of a character which is still in its inventory, as cached by the <see cref="StarterKitStore"/>.
/// </summary>
internal sealed class ActiveStarterKit
{
    private int _expiring;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveStarterKit"/> class.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <param name="expiresAt">The time of expiry, in UTC.</param>
    /// <param name="itemIds">The identifiers of the items.</param>
    public ActiveStarterKit(Guid characterId, DateTime expiresAt, IEnumerable<Guid> itemIds)
    {
        this.CharacterId = characterId;
        this.ExpiresAt = expiresAt;
        this.ItemIds = itemIds.ToHashSet();
    }

    /// <summary>
    /// Gets the character identifier.
    /// </summary>
    public Guid CharacterId { get; }

    /// <summary>
    /// Gets the time of expiry, in UTC.
    /// </summary>
    public DateTime ExpiresAt { get; }

    /// <summary>
    /// Gets the identifiers of the items. A sold item stays in it.
    /// </summary>
    public IReadOnlySet<Guid> ItemIds { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the player got the warning of the last hour.
    /// </summary>
    public bool LastHourWarned { get; set; }

    /// <summary>
    /// Determines whether the kit is expired.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><c>true</c>, if the kit is expired.</returns>
    public bool IsExpired(DateTime utcNow) => utcNow >= this.ExpiresAt;

    /// <summary>
    /// Gets the items of this kit among the items, at their slots, with the expiry of the kit, for the game client.
    /// </summary>
    /// <param name="items">The items, e.g. the inventory.</param>
    /// <returns>The period items.</returns>
    public IReadOnlyList<PeriodItem> GetPeriodItems(IEnumerable<Item> items)
    {
        var periodItems = new List<PeriodItem>();
        foreach (var item in items)
        {
            if (item.Definition is { } definition && this.ItemIds.Contains(item.GetId()))
            {
                periodItems.Add(PeriodItem.Of(definition, item.ItemSlot, this.ExpiresAt));
            }
        }

        return periodItems;
    }

    /// <summary>
    /// Starts the removal of the expired kit, once: the periodic check and the entry may both find it.
    /// </summary>
    /// <returns><c>true</c>, if the caller removes the kit.</returns>
    public bool TryStartExpiry() => Interlocked.Exchange(ref this._expiring, 1) == 0;

    /// <summary>
    /// Allows another removal, after this one failed.
    /// </summary>
    public void CancelExpiry() => Interlocked.Exchange(ref this._expiring, 0);
}
