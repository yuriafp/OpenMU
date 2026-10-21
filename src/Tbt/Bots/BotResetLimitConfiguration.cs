// <copyright file="BotResetLimitConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Bots;

/// <summary>
/// The configuration of the <see cref="BotResetLimitPlugIn"/>.
/// </summary>
public class BotResetLimitConfiguration
{
    /// <summary>
    /// Gets or sets the number of resets a bot stays at least behind the most advanced human.
    /// </summary>
    [Display(Name = "Minimum Distance", Description = "The number of resets a bot stays at least behind the most advanced human.")]
    [Range(0, 100)]
    public int MinimumDistance { get; set; } = 1;

    /// <summary>
    /// Gets or sets the days in which an account must have played to count as the most advanced human.
    /// </summary>
    [Display(Name = "Active Days", Description = "The days in which an account must have played to count as the most advanced human.")]
    [Range(1, 365)]
    public int ActiveDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the comma separated login names of the accounts which don't count, e.g. test accounts.
    /// Bots and game masters never count.
    /// </summary>
    [Display(Name = "Excluded Accounts", Description = "Comma separated login names of the accounts which don't count, e.g. test accounts. Bots and game masters never count.")]
    public string ExcludedAccounts { get; set; } = string.Empty;

    /// <summary>
    /// Gets the login names of the <see cref="ExcludedAccounts"/>.
    /// </summary>
    /// <returns>The login names.</returns>
    /// <remarks>Deliberately a method: a get-only property would end up in the serialized plugin configuration JSON.</remarks>
    public IReadOnlyCollection<string> GetExcludedAccounts()
        => this.ExcludedAccounts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
