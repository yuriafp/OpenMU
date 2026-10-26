// <copyright file="TestGameContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tbt.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A game context for the tests which only need players, e.g. as keys.
/// </summary>
internal static class TestGameContext
{
    /// <summary>
    /// Creates a game context without plugins and with an empty configuration.
    /// </summary>
    /// <returns>The game context.</returns>
    public static IGameServerContext Create()
    {
        var manager = new PlugInManager(null, new NullLoggerFactory(), null, null);
        var gameContext = new Mock<IGameServerContext>();
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new Mock<IPersistenceContextProvider>().Object);
        gameContext.Setup(c => c.Configuration).Returns(new GameConfiguration());
        gameContext.Setup(c => c.PlugInManager).Returns(manager);
        gameContext.Setup(c => c.FeaturePlugIns).Returns(new FeaturePlugInContainer(manager));
        gameContext.Setup(c => c.LoggerFactory).Returns(new NullLoggerFactory());
        return gameContext.Object;
    }
}
