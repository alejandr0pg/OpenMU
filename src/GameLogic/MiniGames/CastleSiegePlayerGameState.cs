// <copyright file="CastleSiegePlayerGameState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Threading;

/// <summary>
/// Tracks the state of a player in a castle siege event.
/// </summary>
internal sealed class CastleSiegePlayerState
{
    private int _score;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegePlayerState"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="team">The team assignment.</param>
    public CastleSiegePlayerState(Player player, CastleSiegeTeam team)
    {
        if (player.SelectedCharacter?.CharacterClass is null)
        {
            throw new InvalidOperationException(
                $"The player '{player}' is in the wrong state");
        }

        this.Player = player;
        this.Team = team;
    }

    /// <summary>
    /// Gets the player.
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// Gets the team.
    /// </summary>
    public CastleSiegeTeam Team { get; }

    /// <summary>
    /// Gets the score.
    /// </summary>
    public int Score => this._score;

    /// <summary>
    /// Gets or sets the rank.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Adds to the score.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void AddScore(int value)
    {
        Interlocked.Add(ref this._score, value);
    }
}
