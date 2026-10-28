// <copyright file="ExperienceBonus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using System.Threading;
using MUnique.OpenMU.AttributeSystem;

/// <summary>
/// The experience bonus of a player during a stay in the world.
/// </summary>
internal sealed class ExperienceBonus
{
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperienceBonus"/> class.
    /// </summary>
    /// <param name="attributes">The attributes of the character, to which the multiplier is attached.</param>
    public ExperienceBonus(AttributeSystem attributes)
    {
        this.Attributes = attributes;
    }

    /// <summary>
    /// Gets the attributes of the character, to which the multiplier is attached.
    /// </summary>
    public AttributeSystem Attributes { get; }

    /// <summary>
    /// Gets the multiplier of the experience rate of the player.
    /// </summary>
    public SimpleElement Multiplier { get; } = new(1, AggregateType.Multiplicate);

    /// <summary>
    /// Gets the bonus, in percent.
    /// </summary>
    public int Percent { get; private set; }

    /// <summary>
    /// Sets the bonus.
    /// </summary>
    /// <param name="percent">The bonus, in percent.</param>
    /// <returns><c>true</c>, if it changed.</returns>
    public bool TrySetPercent(int percent)
    {
        lock (this._lock)
        {
            if (percent == this.Percent)
            {
                return false;
            }

            this.Multiplier.Value = 1 + (percent / 100f);
            this.Percent = percent;
            return true;
        }
    }
}
