// <copyright file="WeeklyGoalChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.WeeklyGoal;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The command <c>/meta</c>, which opens the window of the goal of the week in our client (see
/// <see cref="WeeklyGoalPlugIn"/>).
/// </summary>
[PlugIn]
[Display(Name = "TBT: Comando /meta", Description = "Opens the window of the collective goal of the week, with its progress.")]
[Guid("8B1F4D63-2E97-4A05-B6C8-3D7E9A1F5C24")]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class WeeklyGoalChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/meta";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (player.GameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<WeeklyGoalPlugIn>().FirstOrDefault() is { } plugIn)
        {
            await plugIn.ShowAsync(player).ConfigureAwait(false);
        }
        else
        {
            await player.ShowMessageAsync(WeeklyGoalConfiguration.CreateDefault().NoGoalMessage).ConfigureAwait(false);
        }
    }
}
