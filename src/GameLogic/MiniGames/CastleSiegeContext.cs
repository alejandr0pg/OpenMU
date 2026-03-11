// <copyright file="CastleSiegeContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Collections.Concurrent;
using System.Threading;
using Nito.Disposables.Internals;

/// <summary>
/// The context of a castle siege game.
/// </summary>
/// <remarks>
/// Castle Siege is a guild-based PvP event:
///   Attacking guilds try to take the castle from the defending guild.
///   Uses the Land of Trials map (map 31).
///   Duration is 30 minutes.
///   The attacking guild that controls the Crown Switch when time expires wins.
///   If no attacker controls it, defenders win.
///   Guilds register before the event; players are split into attackers vs defenders.
///   All players may fight each other (PvP enabled).
/// </remarks>
public sealed class CastleSiegeContext : MiniGameContext
{
    private const int MonsterKillPoints = 1;
    private const int PlayerKillPoints = 3;
    private const int CrownControlPoints = 50;

    private readonly ConcurrentDictionary<string, CastleSiegePlayerState> _gameStates = new();

    private IReadOnlyCollection<(string Name, int Score, int BonusExp, int BonusMoney)>? _highScoreTable;
    private TimeSpan _remainingTime;

    private int _attackerScore;
    private int _defenderScore;
    private bool _crownCaptured;
    private uint? _crownHolderGuildId;
    private Player? _winner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeContext"/> class.
    /// </summary>
    /// <param name="key">The key of this context.</param>
    /// <param name="definition">The definition of the mini game.</param>
    /// <param name="gameContext">The game context.</param>
    /// <param name="mapInitializer">The map initializer.</param>
    public CastleSiegeContext(
        MiniGameMapKey key,
        MiniGameDefinition definition,
        IGameContext gameContext,
        IMapInitializer mapInitializer)
        : base(key, definition, gameContext, mapInitializer)
    {
    }

    /// <inheritdoc />
    public override bool AllowPlayerKilling => true;

    /// <inheritdoc />
    protected override TimeSpan RemainingTime => this._remainingTime;

    /// <inheritdoc />
    protected override Player? Winner => this._winner;

    /// <summary>
    /// Attempts to capture the crown switch for an attacking guild.
    /// </summary>
    /// <param name="player">The player capturing the crown.</param>
    public void TryCaptureSwitch(Player player)
    {
        if (player.GuildStatus is not { } guildStatus || guildStatus.GuildId == 0)
        {
            return;
        }

        if (!this._gameStates.TryGetValue(player.Name, out var state))
        {
            return;
        }

        // Only attackers can capture
        if (state.Team != CastleSiegeTeam.Attacker)
        {
            return;
        }

        this._crownCaptured = true;
        this._crownHolderGuildId = guildStatus.GuildId;
        state.AddScore(CrownControlPoints);
        Interlocked.Add(ref this._attackerScore, CrownControlPoints);
    }

    /// <inheritdoc />
    protected override void OnMonsterDied(object? sender, DeathInformation e)
    {
        base.OnMonsterDied(sender, e);

        if (this._gameStates.TryGetValue(e.KillerName, out var state))
        {
            state.AddScore(MonsterKillPoints);
            AddTeamScore(state.Team, MonsterKillPoints);
        }
    }

    /// <inheritdoc />
    protected override void OnPlayerDied(object? sender, DeathInformation e)
    {
        base.OnPlayerDied(sender, e);

        if (this._gameStates.TryGetValue(e.KillerName, out var killerState))
        {
            killerState.AddScore(PlayerKillPoints);
            AddTeamScore(killerState.Team, PlayerKillPoints);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnGameStartAsync(ICollection<Player> players)
    {
        this.AssignTeams(players);
        await base.OnGameStartAsync(players).ConfigureAwait(false);
        _ = Task.Run(
            async () => await this.EventLoopAsync(this.GameEndedToken).ConfigureAwait(false),
            this.GameEndedToken);
    }

    /// <inheritdoc />
    protected override async ValueTask GameEndedAsync(ICollection<Player> finishers)
    {
        this.DetermineWinner(finishers);

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
            var (bonusScore, givenMoney) = await this.GiveRewardsAndGetBonusScoreAsync(
                state.Player, rank).ConfigureAwait(false);
            state.AddScore(bonusScore);
            scoreList.Add((
                state.Player.Name,
                state.Score,
                this.Definition.Rewards
                    .FirstOrDefault(r =>
                        r.RewardType == MiniGameRewardType.Experience
                        && (r.Rank is null || r.Rank == rank))
                    ?.RewardAmount ?? 0,
                givenMoney));
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

    private void AddTeamScore(CastleSiegeTeam team, int points)
    {
        if (team == CastleSiegeTeam.Attacker)
        {
            Interlocked.Add(ref this._attackerScore, points);
        }
        else
        {
            Interlocked.Add(ref this._defenderScore, points);
        }
    }

    private void AssignTeams(ICollection<Player> players)
    {
        // First guild to register is the defender; all others are attackers.
        // If no guild info, player is assigned as attacker.
        uint defenderGuildId = 0;
        foreach (var player in players)
        {
            var guildId = player.GuildStatus?.GuildId ?? 0;
            CastleSiegeTeam team;

            if (defenderGuildId == 0 && guildId != 0)
            {
                defenderGuildId = guildId;
                team = CastleSiegeTeam.Defender;
            }
            else if (guildId != 0 && guildId == defenderGuildId)
            {
                team = CastleSiegeTeam.Defender;
            }
            else
            {
                team = CastleSiegeTeam.Attacker;
            }

            this._gameStates.TryAdd(player.Name, new CastleSiegePlayerState(player, team));
        }
    }

    private void DetermineWinner(ICollection<Player> finishers)
    {
        // If crown was captured by attackers, attacking guild wins.
        // Otherwise, defenders win.
        CastleSiegeTeam winningTeam;
        if (this._crownCaptured && this._crownHolderGuildId is not null)
        {
            winningTeam = CastleSiegeTeam.Attacker;
        }
        else
        {
            winningTeam = CastleSiegeTeam.Defender;
        }

        // Winner is the top scorer of the winning team.
        this._winner = finishers
            .Select(f => this._gameStates.GetValueOrDefault(f.Name))
            .WhereNotNull()
            .Where(s => s.Team == winningTeam)
            .OrderByDescending(s => s.Score)
            .FirstOrDefault()?.Player;
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
            int tickCount = 0;

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                this._remainingTime = ending.Subtract(DateTime.UtcNow);
                tickCount++;

                if (tickCount % 5 == 0)
                {
                    await this.BroadcastStateAsync().ConfigureAwait(false);
                }
            }

            this._remainingTime = TimeSpan.Zero;
            await this.BroadcastStateAsync(playState: 2).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the game ends.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Unexpected error during castle siege event loop: {0}", ex.Message);
        }
    }

    private async ValueTask BroadcastStateAsync(byte playState = 1)
    {
        var remainSec = (ushort)Math.Max(0, this._remainingTime.TotalSeconds);
        byte crownHolder = this._crownCaptured ? (byte)1 : (byte)2; // 1=Attackers, 2=Defenders

        await this.ForEachPlayerAsync(player =>
            player.InvokeViewPlugInAsync<ICastleSiegeStateViewPlugin>(
                p => p.UpdateStateAsync(playState, remainSec, crownHolder)).AsTask())
            .ConfigureAwait(false);
    }
}
