// <copyright file="IBotResetCheckPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called before a bot performs a character reset. It can deny the reset,
/// e.g. to keep the bots from getting ahead of the human players.
/// </summary>
[Guid("3F1A7C52-9B4E-4D06-A8E3-6C2B5D9F1E47")]
[PlugInPoint("Bot reset check", "Plugins which are called before a bot performs a character reset. They can deny the reset.")]
public interface IBotResetCheckPlugIn
{
    /// <summary>
    /// Is called before a bot performs a character reset. Called frequently, so it must be cheap.
    /// </summary>
    /// <param name="bot">The bot player.</param>
    /// <param name="nextResetCount">The reset count the bot would reach.</param>
    /// <param name="eventArgs">The <see cref="CancelEventArgs"/> instance containing the event data. Set <see cref="CancelEventArgs.Cancel"/> to deny the reset.</param>
    void CheckBotReset(Player bot, int nextResetCount, CancelEventArgs eventArgs);
}
