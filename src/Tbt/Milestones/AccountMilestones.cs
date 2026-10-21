// <copyright file="AccountMilestones.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Milestones;

using System.Threading;

/// <summary>
/// The cached milestones of an account. A milestone is reserved here before it's recorded, so two
/// checks running at the same time never give it twice.
/// </summary>
internal sealed class AccountMilestones
{
    private readonly HashSet<string> _milestones;

    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountMilestones"/> class.
    /// </summary>
    /// <param name="milestones">The milestones the account has.</param>
    public AccountMilestones(IEnumerable<string> milestones)
    {
        this._milestones = new HashSet<string>(milestones);
    }

    /// <summary>
    /// Determines whether the account has the milestone, or it's being given right now.
    /// </summary>
    /// <param name="milestone">The milestone.</param>
    /// <returns><c>true</c>, if the account has the milestone.</returns>
    public bool Has(string milestone)
    {
        lock (this._lock)
        {
            return this._milestones.Contains(milestone);
        }
    }

    /// <summary>
    /// Reserves the milestone for being given.
    /// </summary>
    /// <param name="milestone">The milestone.</param>
    /// <returns><c>true</c>, if it was reserved; <c>false</c>, if the account has it already.</returns>
    public bool TryReserve(string milestone)
    {
        lock (this._lock)
        {
            return this._milestones.Add(milestone);
        }
    }

    /// <summary>
    /// Releases a reserved milestone which couldn't be given, so it's tried again later.
    /// </summary>
    /// <param name="milestone">The milestone.</param>
    public void Release(string milestone)
    {
        lock (this._lock)
        {
            this._milestones.Remove(milestone);
        }
    }
}
