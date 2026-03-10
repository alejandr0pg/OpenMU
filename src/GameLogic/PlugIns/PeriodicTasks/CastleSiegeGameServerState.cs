// <copyright file="CastleSiegeGameServerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The state of a game server state for a castle siege event.
/// </summary>
public class CastleSiegeGameServerState : PeriodicTaskGameServerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeGameServerState"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    public CastleSiegeGameServerState(IGameContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Description => "Castle Siege";
}
