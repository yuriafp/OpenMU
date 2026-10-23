// <copyright file="StarterKitPendingEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

/// <summary>
/// A check of the starter kit at the entry which must be tried again, e.g. because the database didn't answer.
/// </summary>
/// <param name="CharacterId">The character which entered the world.</param>
/// <param name="Attempts">The failed attempts so far.</param>
/// <param name="NextAttemptUtc">The time of the next attempt, in UTC.</param>
internal sealed record StarterKitPendingEntry(Guid CharacterId, int Attempts, DateTime NextAttemptUtc)
{
    /// <summary>The longest wait between two attempts.</summary>
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the entry after one more failed attempt: the wait doubles from the first delay, up to 5 minutes.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="firstDelay">The wait after the first failure.</param>
    /// <returns>The entry for the next attempt.</returns>
    public StarterKitPendingEntry Failed(DateTime utcNow, TimeSpan firstDelay)
    {
        var delay = firstDelay * Math.Pow(2, Math.Min(this.Attempts, 10));
        return this with { Attempts = this.Attempts + 1, NextAttemptUtc = utcNow + (delay < MaximumDelay ? delay : MaximumDelay) };
    }
}
