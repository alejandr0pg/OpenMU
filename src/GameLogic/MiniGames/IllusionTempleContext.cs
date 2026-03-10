// <copyright file="IllusionTempleContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic.NPC;
using Nito.Disposables.Internals;

/// <summary>
/// The context of an illusion temple game.
/// </summary>
/// <remarks>
/// Illusion Temple is a team-based PvP event:
///   Players are split into two teams (Alliance and Illusion).
///   Each team earns points by killing opposing team members and monsters.
///   The team with the higher score at the end wins.
///   Winners receive better rewards (experience, money, items).
///   Duration is typically 15 minutes with 6 difficulty levels.
/// </remarks>
public sealed class IllusionTempleContext : MiniGameContext
{
    private const int MonsterKillPoints = 2;
    private const int PlayerKillPoints = 5;

    private readonly ConcurrentDictionary<string, PlayerGameState> _gameStates = new();

    private IReadOnlyCollection<(string Name, int Score, int BonusExp, int BonusMoney)>? _highScoreTable;
    private TimeSpan _remainingTime;

    private int _team1Score;
    private int _team2Score;

    /// <summary>
    /// Initializes a new instance of the <see cref="IllusionTempleContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context, to which this game belongs.</param>
    /// <param name="mapInitializer">The map initializer, which is used when the event starts.</param>
    public IllusionTempleContext(MiniGameMapKey key, MiniGameDefinition definition, IGameContext gameContext, IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <inheritdoc />
    public override bool AllowPlayerKilling => true;

    /// <inheritdoc />
    protected override TimeSpan RemainingTime => this._remainingTime;

    /// <inheritdoc />
    protected override void OnMonsterDied(object? sender, DeathInformation e)
    {
        base.OnMonsterDied(sender, e);

        if (this._gameStates.TryGetValue(e.KillerName, out var state))
        {
            state.AddScore(MonsterKillPoints);
            if (state.Team == 1)
            {
                Interlocked.Add(ref this._team1Score, MonsterKillPoints);
            }
            else
            {
                Interlocked.Add(ref this._team2Score, MonsterKillPoints);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPlayerDied(object? sender, DeathInformation e)
    {
        base.OnPlayerDied(sender, e);

        if (this._gameStates.TryGetValue(e.KillerName, out var killerState))
        {
            killerState.AddScore(PlayerKillPoints);
            if (killerState.Team == 1)
            {
                Interlocked.Add(ref this._team1Score, PlayerKillPoints);
            }
            else
            {
                Interlocked.Add(ref this._team2Score, PlayerKillPoints);
            }
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        var playerList = players.ToList();
        var shuffled = playerList.OrderBy(_ => Rand.NextInt(0, 1000)).ToList();

        byte team = 1;
        foreach (var player in shuffled)
        {
            this._gameStates.TryAdd(player.Name, new PlayerGameState(player, team));
            team = team == 1 ? (byte)2 : (byte)1;
        }

        await base.OnGameStartAsync(players).ConfigureAwait(false);

        _ = Task.Run(async () => await this.EventLoopAsync(this.GameEndedToken).ConfigureAwait(false), this.GameEndedToken);
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        var winningTeam = this._team1Score >= this._team2Score ? (byte)1 : (byte)2;

        var sortedFinishers = finishers
            .Select(f => this._gameStates.GetValueOrDefault(f.Name))
            .WhereNotNull()
            .OrderByDescending(s => s.Score)
            .ToList();

        var scoreList = new List<(string Name, int Score, int BonusExp, int BonusMoney)>();
        int rank = 0;
        foreach (var state in sortedFinishers)
        {
            rank++;
            state.Rank = rank;
            state.IsWinningTeam = state.Team == winningTeam;
            var (bonusScore, givenMoney) = await this.GiveRewardsAndGetBonusScoreAsync(state.Player, rank).ConfigureAwait(false);
            state.AddScore(bonusScore);
            scoreList.Add((
                state.Player.Name,
                state.Score,
                this.Definition.Rewards.FirstOrDefault(r => r.RewardType == MiniGameRewardType.Experience && (r.Rank is null || r.Rank == rank))?.RewardAmount ?? 0,
                givenMoney));
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

    private async ValueTask EventLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var timerInterval = TimeSpan.FromSeconds(1);
            using var timer = new PeriodicTimer(timerInterval);
            var maximumGameDuration = this.Definition.GameDuration;
            this._remainingTime = maximumGameDuration;
            var ending = DateTime.UtcNow.Add(maximumGameDuration);

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                this._remainingTime = ending.Subtract(DateTime.UtcNow);
            }

            this._remainingTime = TimeSpan.Zero;
        }
        catch (OperationCanceledException)
        {
            // Expected when the game ends.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Unexpected error during illusion temple event loop: {0}", ex.Message);
        }
    }

    private sealed class PlayerGameState
    {
        private int _score;

        public PlayerGameState(Player player, byte team)
        {
            if (player.SelectedCharacter?.CharacterClass is null)
            {
                throw new InvalidOperationException($"The player '{player}' is in the wrong state");
            }

            this.Player = player;
            this.Team = team;
        }

        public Player Player { get; }

        public byte Team { get; }

        public int Score => this._score;

        public int Rank { get; set; }

        public bool IsWinningTeam { get; set; }

        public void AddScore(int value)
        {
            Interlocked.Add(ref this._score, value);
        }
    }
}
