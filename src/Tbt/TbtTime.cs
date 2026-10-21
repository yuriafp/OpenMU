// <copyright file="TbtTime.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Globalization;

/// <summary>
/// The times shown to the players. The server runs in UTC (PLANO-SERVIDOR.md, 9.3), and the players
/// read the time of Brasília, which has no daylight saving time since 2019.
/// </summary>
internal static class TbtTime
{
    /// <summary>
    /// The offset of the shown times to UTC.
    /// </summary>
    public static readonly TimeSpan ShownOffset = TimeSpan.FromHours(-3);

    /// <summary>
    /// Formats a time of UTC as shown to the players, e.g. "19:30".
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The shown time.</returns>
    public static string Format(TimeOnly utc) => utc.Add(ShownOffset).ToString("HH:mm", CultureInfo.InvariantCulture);
}
