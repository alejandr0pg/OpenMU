// <copyright file="CastleSiegeStartPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables the start of the castle siege.
/// </summary>
[PlugIn]
[Display(Name = nameof(CastleSiegeStartPlugIn), Description = "Castle Siege event")]
[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public sealed class CastleSiegeStartPlugIn
    : MiniGameStartBasePlugIn<CastleSiegeStartConfiguration, CastleSiegeGameServerState>
{
    /// <inheritdoc />
    public override MiniGameType Key => MiniGameType.CastleSiege;

    /// <inheritdoc />
    public override object CreateDefaultConfig()
    {
        return CastleSiegeStartConfiguration.Default;
    }

    /// <inheritdoc />
    protected override CastleSiegeGameServerState CreateState(IGameContext gameContext)
    {
        return new CastleSiegeGameServerState(gameContext);
    }
}
