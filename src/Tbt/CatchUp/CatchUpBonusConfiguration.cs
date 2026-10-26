// <copyright file="CatchUpBonusConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.CatchUp;

/// <summary>
/// The configuration of the <see cref="CatchUpBonusPlugIn"/>.
/// </summary>
public class CatchUpBonusConfiguration
{
    /// <summary>
    /// Gets or sets the experience bonus, in percent, for each reset behind the most advanced human.
    /// </summary>
    [Display(Name = "Percent Per Reset", Description = "The experience bonus, in percent, for each reset behind the most advanced human.")]
    [Range(0, 100)]
    public int PercentPerReset { get; set; } = 10;

    /// <summary>
    /// Gets or sets the highest experience bonus, in percent.
    /// </summary>
    [Display(Name = "Maximum Percent", Description = "The highest experience bonus, in percent.")]
    [Range(0, 500)]
    public int MaximumPercent { get; set; } = 50;

    /// <summary>
    /// Gets or sets the days in which an account must have played to count as the most advanced human.
    /// </summary>
    [Display(Name = "Active Days", Description = "The days in which an account must have played to count as the most advanced human.")]
    [Range(1, 365)]
    public int ActiveDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the comma separated login names of the accounts which don't count as the most
    /// advanced human, e.g. test accounts. Bots and game masters never count.
    /// </summary>
    [Display(Name = "Excluded Accounts", Description = "Comma separated login names of the accounts which don't count as the most advanced human, e.g. test accounts. Bots and game masters never count.")]
    public string ExcludedAccounts { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message when the bonus starts or changes; <c>{0}</c> is the bonus in percent
    /// and <c>{1}</c> the resets of the most advanced human.
    /// </summary>
    [Display(Name = "Message", Description = "The message when the bonus starts or changes; {0} is the bonus in percent and {1} the resets of the most advanced human.")]
    public string Message { get; set; } = "Bônus de alcance: +{0}% de exp. O mais avançado está no reset {1}.";

    /// <summary>
    /// Gets or sets the message when the bonus ends during a stay in the world.
    /// </summary>
    [Display(Name = "End Message", Description = "The message when the bonus ends during a stay in the world.")]
    public string EndMessage { get; set; } = "Você alcançou o mais avançado: o bônus de alcance acabou.";

    /// <summary>
    /// Gets the login names of the <see cref="ExcludedAccounts"/>.
    /// </summary>
    /// <returns>The login names.</returns>
    /// <remarks>Deliberately a method: a get-only property would end up in the serialized plugin configuration JSON.</remarks>
    public IReadOnlyCollection<string> GetExcludedAccounts()
        => this.ExcludedAccounts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
