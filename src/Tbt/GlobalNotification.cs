// <copyright file="GlobalNotification.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The messages of our events to all players, whose texts can be changed in the admin panel.
/// </summary>
internal static class GlobalNotification
{
    /// <summary>
    /// Sends a message to all, with the default one when the configured one has a bad format, e.g. a
    /// <c>{3}</c> for two arguments. An empty message isn't sent.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="message">The configured message.</param>
    /// <param name="defaultMessage">The default message.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="arguments">The arguments of the message.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SendAsync(IGameContext gameContext, LocalizedString message, LocalizedString defaultMessage, ILogger logger, params object?[] arguments)
    {
        if (string.IsNullOrWhiteSpace(message.ValueInNeutralLanguage))
        {
            return;
        }

        try
        {
            await gameContext.SendGlobalNotificationAsync(message, arguments).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "The message has a bad format: {Message}", message.ValueInNeutralLanguage);
            await gameContext.SendGlobalNotificationAsync(defaultMessage, arguments).ConfigureAwait(false);
        }
    }
}
