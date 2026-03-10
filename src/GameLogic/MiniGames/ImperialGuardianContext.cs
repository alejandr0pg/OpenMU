// <copyright file="ImperialGuardianContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Collections.Concurrent;
using System.Threading;
using Nito.Disposables.Internals;

/// <summary>
/// The context of an imperial guardian (fortress) game.
/// </summary>
/// <remarks>
/// Imperial Guardian is a wave-based PvE event:
///   Party of up to 5 players enters via NPC and fights waves of monsters
///   across 4 fortress stages. Score is based on monster kills
///   (game level points per kill). Ranked rewards at end.
///   Duration is 20 minutes with 4 difficulty levels.
/// </remarks>
public sealed class ImperialGuardianContext : MiniGameContext
{
    private readonly ConcurrentDictionary<string, PlayerGameState> _gameStates = new();

    private IReadOnlyCollection<(string Name, int Score, int BonusMoney, int BonusExp)>? _highScoreTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImperialGuardianContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context, to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public ImperialGuardianContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <inheritdoc />
    protected override void OnMonsterDied(object? sender, DeathInformation e)
    {
        base.OnMonsterDied(sender, e);

        if (this._gameStates.TryGetValue(e.KillerName, out var state))
        {
            state.AddScore(this.Definition.GameLevel);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        foreach (var player in players)
        {
            this._gameStates.TryAdd(player.Name, new PlayerGameState(player));
        }

        await base.OnGameStartAsync(players).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        var sortedFinishers = finishers
            .Select(f => this._gameStates.GetValueOrDefault(f.Name))
            .WhereNotNull()
            .OrderByDescending(state => state.Score)
            .ToList();

        var scoreList = new List<(string Name, int Score, int BonusMoney, int BonusExp)>();
        int rank = 0;
        foreach (var state in sortedFinishers)
        {
            rank++;
            state.Rank = rank;
            var (bonusScore, givenMoney) = await this.GiveRewardsAndGetBonusScoreAsync(state.Player, rank).ConfigureAwait(false);
            state.AddScore(bonusScore);
            scoreList.Add((
                state.Player.Name,
                state.Score,
                givenMoney,
                this.Definition.Rewards.FirstOrDefault(r => r.RewardType == MiniGameRewardType.Experience && (r.Rank is null || r.Rank == rank))?.RewardAmount ?? 0));
        }

        this._highScoreTable = scoreList.AsReadOnly();

        await this.SaveRankingAsync(sortedFinishers.Select(s => (s.Rank, s.Player.SelectedCharacter!, s.Score))).ConfigureAwait(false);
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask ShowScoreAsync(Player player)
    {
        if (this._highScoreTable is { } table
            && this._gameStates.TryGetValue(player.Name, out var state))
        {
            await player.InvokeViewPlugInAsync<IMiniGameScoreTableViewPlugin>(p => p.ShowScoreTableAsync((byte)state.Rank, table)).ConfigureAwait(false);
        }
    }

    private sealed class PlayerGameState
    {
        private int _score;

        public PlayerGameState(Player player)
        {
            if (player.SelectedCharacter?.CharacterClass is null)
            {
                throw new InvalidOperationException($"The player '{player}' is in the wrong state");
            }

            this.Player = player;
        }

        public Player Player { get; }

        public int Score => this._score;

        public int Rank { get; set; }

        public void AddScore(int value)
        {
            Interlocked.Add(ref this._score, value);
        }
    }
}
