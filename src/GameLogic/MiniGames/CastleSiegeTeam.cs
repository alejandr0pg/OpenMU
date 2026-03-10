// <copyright file="CastleSiegeTeam.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

/// <summary>
/// Defines the team in a castle siege event.
/// </summary>
public enum CastleSiegeTeam : byte
{
    /// <summary>
    /// The defending guild team.
    /// </summary>
    Defender = 1,

    /// <summary>
    /// The attacking guilds team.
    /// </summary>
    Attacker = 2,
}
