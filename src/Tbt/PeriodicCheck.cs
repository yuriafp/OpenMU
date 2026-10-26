// <copyright file="PeriodicCheck.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Threading;

/// <summary>
/// The periodic check of a plugin on one game server, e.g. of the starter kits. All game servers of the
/// process share the plugin instance, so each one keeps its own interval and guard.
/// </summary>
internal sealed class PeriodicCheck
{
    private DateTime _nextCheckUtc = DateTime.UtcNow;

    private int _running;

    /// <summary>
    /// Starts a check, when it's due and none is running.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="interval">The interval of the checks.</param>
    /// <returns><c>true</c>, if the caller runs the check and calls <see cref="End"/> after it.</returns>
    public bool TryStart(DateTime utcNow, TimeSpan interval)
    {
        if (utcNow < this._nextCheckUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return false;
        }

        this._nextCheckUtc = utcNow + interval;
        return true;
    }

    /// <summary>
    /// Ends the running check.
    /// </summary>
    public void End() => Interlocked.Exchange(ref this._running, 0);

    /// <summary>
    /// Makes the next check due right away.
    /// </summary>
    public void ForceStart() => this._nextCheckUtc = DateTime.UtcNow;
}
