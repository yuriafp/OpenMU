// <copyright file="StarterKitChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.StarterKit;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The command <c>/kit</c>, which shows when the starter kit of the character expires. The client
/// shows it on the items, too (see <see cref="StarterKitPlugIn"/>).
/// </summary>
[PlugIn]
[Display(Name = "TBT: Comando /kit", Description = "Shows the player when the starter kit of the character expires.")]
[Guid("55DF2756-6437-40E0-B7C6-7560C1947106")]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class StarterKitChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/kit";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        var message = player.SelectedCharacter is { } character && StarterKitStore.GetActive(character.Id) is { } kit
            ? StarterKitPlugIn.FormatRemaining(kit, DateTime.UtcNow)
            : "Você não tem um kit iniciante ativo.";
        await player.ShowMessageAsync(message).ConfigureAwait(false);
    }
}
