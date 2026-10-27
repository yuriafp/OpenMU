// <copyright file="TreasureChest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Treasure;

using System.Threading;

/// <summary>
/// A chest of a round. Who claims it first, by opening it or by the end of the round, gets it; the
/// others only see it go.
/// </summary>
internal sealed class TreasureChest
{
    private int _claimed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreasureChest"/> class.
    /// </summary>
    /// <param name="isGolden">If set to <c>true</c>, the chest is a golden one.</param>
    public TreasureChest(bool isGolden)
    {
        this.IsGolden = isGolden;
    }

    /// <summary>
    /// Gets a value indicating whether the chest is a golden one.
    /// </summary>
    public bool IsGolden { get; }

    /// <summary>
    /// Claims the chest.
    /// </summary>
    /// <returns><c>true</c> for the first claim only.</returns>
    public bool TryClaim() => Interlocked.Exchange(ref this._claimed, 1) == 0;
}
