// <copyright file="CrywolfContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic.NPC;
using Nito.Disposables.Internals;

/// <summary>
/// The context of a crywolf defense event.
/// </summary>
/// <remarks>
/// Crywolf is a server-wide defense event on map 34 where players
/// defend the statue of the Holy Grail against Balgass's army.
/// Phases: Preparation, Battle (30 min), Result.
/// No ticket required, no entrance fee.
/// If the statue survives the battle phase, the event succeeds.
/// If the statue is destroyed, the event fails.
/// Players earn score by killing monsters (score = game level per kill).
/// Rewards: experience and money for participants based on ranking.
/// </remarks>
public sealed class CrywolfContext : MiniGameContext
{
    /// <summary>
    /// Wolf Status (statue) monster number on map 34.
    /// </summary>
    private const short WolfStatueNumber = 204;

    private readonly ConcurrentDictionary<string, PlayerGameState> _gameStates = new();

    private IReadOnlyCollection<(string Name, int Score, int BonusMoney, int BonusExp)>? _highScoreTable;
    private TimeSpan _remainingTime;

    private bool _statueDestroyed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrywolfContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context.</param>
    /// <param name="mapInitializer">The map initializer.</param>
    public CrywolfContext(
        MiniGameMapKey key,
        MiniGameDefinition definition,
        IGameContext gameContext,
        IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the statue was destroyed.
    /// </summary>
    public bool IsStatueDestroyed => this._statueDestroyed;

    /// <inheritdoc />
    protected override TimeSpan RemainingTime => this._remainingTime;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "VSTHRD100:Avoid async void methods",
        Justification = "Catching all Exceptions.")]
    protected override async void OnDestructibleDied(object? sender, DeathInformation e)
    {
        try
        {
            base.OnDestructibleDied(sender, e);

            if (sender is Destructible { Definition.Number: WolfStatueNumber })
            {
                this._statueDestroyed = true;
                this.FinishEvent();
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Unexpected error in OnDestructibleDied.");
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        foreach (var player in players)
        {
            this._gameStates.TryAdd(player.Name, new PlayerGameState(player));
        }

        _ = Task.Run(
            async () => await this.EventLoopAsync(this.GameEndedToken).ConfigureAwait(false),
            this.GameEndedToken);

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
            var (bonusScore, givenMoney) = await this.GiveRewardsAndGetBonusScoreAsync(
                state.Player, rank).ConfigureAwait(false);
            state.AddScore(bonusScore);
            scoreList.Add((
                state.Player.Name,
                state.Score,
                givenMoney,
                this.Definition.Rewards
                    .FirstOrDefault(r =>
                        r.RewardType == MiniGameRewardType.Experience
                        && (r.Rank is null || r.Rank == rank))
                    ?.RewardAmount ?? 0));
        }

        this._highScoreTable = scoreList.AsReadOnly();

        await this.SaveRankingAsync(
            sortedFinishers.Select(s => (s.Rank, s.Player.SelectedCharacter!, s.Score)))
            .ConfigureAwait(false);
        await base.GameEndedAsync(finishers).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask ShowScoreAsync(Player player)
    {
        if (this._highScoreTable is { } table
            && this._gameStates.TryGetValue(player.Name, out var state))
        {
            await player.InvokeViewPlugInAsync<IMiniGameScoreTableViewPlugin>(
                p => p.ShowScoreTableAsync((byte)state.Rank, table)).ConfigureAwait(false);
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
            this.Logger.LogError(ex, "Unexpected error during crywolf event loop: {0}", ex.Message);
        }
    }

    private sealed class PlayerGameState
    {
        private int _score;

        public PlayerGameState(Player player)
        {
            if (player.SelectedCharacter?.CharacterClass is null)
            {
                throw new InvalidOperationException(
                    $"The player '{player}' is in the wrong state");
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
