// <copyright file="TbtPlayerExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Extensions for the players, shared by our plugins.
/// </summary>
internal static class TbtPlayerExtensions
{
    /// <summary>
    /// The states in which a player is in the world. A player leaves the world when it changes from
    /// one of them to another state, e.g. back to the character selection or disconnected.
    /// </summary>
    private static readonly State[] InWorldStates =
    [
        PlayerState.EnteredWorld,
        PlayerState.ChangingMap,
        PlayerState.Dead,
        PlayerState.NpcDialogOpened,
        PlayerState.PartyRequest,
        PlayerState.GuildRequest,
        PlayerState.TradeRequested,
        PlayerState.TradeOpened,
        PlayerState.TradeButtonPressed,
    ];

    /// <summary>
    /// Determines whether the player is a human: not a bot and not a template. Every new plugin
    /// ignores the bots (PLANO-SERVIDOR.md, 9.1).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player is a human.</returns>
    public static bool IsHuman(this Player player)
        => player.Account is { IsBot: false, IsTemplate: false } && player.SelectedCharacter is not null;

    /// <summary>
    /// Determines whether the state change is the entry of a character into the world.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="currentState">The current state.</param>
    /// <returns><c>true</c>, if the character entered the world.</returns>
    public static bool IsEnteringWorld(State previousState, State currentState)
        => previousState == PlayerState.CharacterSelection && currentState == PlayerState.EnteredWorld;

    /// <summary>
    /// Determines whether the state change is the character leaving the world.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="currentState">The current state.</param>
    /// <returns><c>true</c>, if the character left the world.</returns>
    public static bool IsLeavingWorld(State previousState, State currentState)
        => InWorldStates.Contains(previousState) && !InWorldStates.Contains(currentState);

    /// <summary>
    /// Shows a message to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="message">The message.</param>
    /// <param name="messageType">The type of the message.</param>
    /// <returns>The task.</returns>
    public static ValueTask ShowMessageAsync(this Player player, string message, MessageType messageType = MessageType.BlueNormal)
        => player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, messageType));
}
