// <copyright file="BotResetLimitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Bots;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tbt.Registro;

/// <summary>
/// Bots never get ahead of the humans (PLANO-SERVIDOR.md, 5.7, item 6 of 9.5): a bot only resets
/// while it stays at least <see cref="BotResetLimitConfiguration.MinimumDistance"/> resets behind
/// the most advanced human (see <see cref="HumanProgress"/>). While that isn't known, e.g. before
/// any human entered the world with our registro, no bot resets.
/// </summary>
[PlugIn]
[Display(Name = "TBT: Bots atrás dos humanos", Description = "A bot only resets while it stays behind the most advanced human of the accounts which played recently.")]
[Guid("6D1B9E24-8C47-4F3A-B5E2-9A7C3D0F4B81")]
public class BotResetLimitPlugIn : IBotResetCheckPlugIn, ISupportCustomConfiguration<BotResetLimitConfiguration>, ISupportDefaultCustomConfiguration
{
    private ILogger? _logger;

    /// <inheritdoc />
    public BotResetLimitConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BotResetLimitConfiguration();

    /// <inheritdoc />
    public void CheckBotReset(Player bot, int nextResetCount, CancelEventArgs eventArgs)
    {
        try
        {
            var configuration = this.Configuration ??= new BotResetLimitConfiguration();
            this._logger ??= bot.GameContext.LoggerFactory.CreateLogger(this.GetType().Name);
            var humanResets = HumanProgress.GetMaximumResets(configuration.ActiveDays, configuration.GetExcludedAccounts(), this._logger);
            if (humanResets is not { } maximum || nextResetCount > maximum - configuration.MinimumDistance)
            {
                eventArgs.Cancel = true;
            }
        }
        catch (Exception ex)
        {
            bot.Logger.LogWarning(ex, "Couldn't check the reset of the bot {Character}; it doesn't reset.", bot.Name);
            eventArgs.Cancel = true;
        }
    }
}
