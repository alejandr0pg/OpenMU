// <copyright file="IllusionTempleInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the illusion temple event.
/// </summary>
internal class IllusionTempleInitializer : InitializerBase
{
    /// <summary>
    /// Reward table: (GameLevel, ExperienceBase, ExperiencePerRemainingSecond, (WinnersMoney, LosersMoney), EntranceFee).
    /// </summary>
    private static readonly List<(int GameLevel, int ExperienceBase, int ExperiencePerSecond, (int Winners, int Losers) Money, int EntranceFee)> RewardTable = new()
    {
        (1, 50_000, 120, (80_000, 30_000), 50_000),
        (2, 80_000, 150, (120_000, 50_000), 80_000),
        (3, 120_000, 180, (180_000, 80_000), 120_000),
        (4, 160_000, 210, (250_000, 100_000), 180_000),
        (5, 200_000, 240, (350_000, 150_000), 250_000),
        (6, 250_000, 280, (500_000, 200_000), 400_000),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="IllusionTempleInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public IllusionTempleInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var it1 = this.CreateIllusionTempleDefinition(1, IllusionTemple1.Number);
        it1.MinimumCharacterLevel = 220;
        it1.MaximumCharacterLevel = 270;
        it1.MinimumSpecialCharacterLevel = 220;
        it1.MaximumSpecialCharacterLevel = 270;

        var it2 = this.CreateIllusionTempleDefinition(2, IllusionTemple2.Number);
        it2.MinimumCharacterLevel = 271;
        it2.MaximumCharacterLevel = 320;
        it2.MinimumSpecialCharacterLevel = 271;
        it2.MaximumSpecialCharacterLevel = 320;

        var it3 = this.CreateIllusionTempleDefinition(3, IllusionTemple3.Number);
        it3.MinimumCharacterLevel = 321;
        it3.MaximumCharacterLevel = 350;
        it3.MinimumSpecialCharacterLevel = 321;
        it3.MaximumSpecialCharacterLevel = 350;

        var it4 = this.CreateIllusionTempleDefinition(4, IllusionTemple4.Number);
        it4.MinimumCharacterLevel = 351;
        it4.MaximumCharacterLevel = 380;
        it4.MinimumSpecialCharacterLevel = 351;
        it4.MaximumSpecialCharacterLevel = 380;

        var it5 = this.CreateIllusionTempleDefinition(5, IllusionTemple5.Number);
        it5.MinimumCharacterLevel = 381;
        it5.MaximumCharacterLevel = 400;
        it5.MinimumSpecialCharacterLevel = 381;
        it5.MaximumSpecialCharacterLevel = 400;

        var it6 = this.CreateIllusionTempleDefinition(6, IllusionTemple6.Number);
        it6.RequiresMasterClass = true;
        it6.MinimumCharacterLevel = 400;
        it6.MaximumCharacterLevel = 400;
        it6.MinimumSpecialCharacterLevel = 400;
        it6.MaximumSpecialCharacterLevel = 400;
    }

    /// <summary>
    /// Creates a new <see cref="MiniGameDefinition"/> for an illusion temple event.
    /// </summary>
    /// <param name="level">The level of the event.</param>
    /// <param name="mapNumber">The map number.</param>
    /// <returns>The created <see cref="MiniGameDefinition"/>.</returns>
    protected MiniGameDefinition CreateIllusionTempleDefinition(byte level, short mapNumber)
    {
        var illusionTemple = this.Context.CreateNew<MiniGameDefinition>();
        illusionTemple.SetGuid((short)MiniGameType.IllusionTemple, level);
        this.GameConfiguration.MiniGameDefinitions.Add(illusionTemple);
        illusionTemple.Name = $"Illusion Temple {level}";
        illusionTemple.Description = $"Event definition for illusion temple event, level {level}.";
        illusionTemple.EnterDuration = TimeSpan.FromMinutes(2);
        illusionTemple.GameDuration = TimeSpan.FromMinutes(15);
        illusionTemple.ExitDuration = TimeSpan.FromMinutes(1);
        illusionTemple.MaximumPlayerCount = 10;
        illusionTemple.Entrance = this.GameConfiguration.Maps.First(m => m.Number == mapNumber).ExitGates.First();
        illusionTemple.Type = MiniGameType.IllusionTemple;
        illusionTemple.TicketItem = this.GameConfiguration.Items.Single(item => item is { Group: 13, Number: 50 });
        illusionTemple.TicketItemLevel = level;
        illusionTemple.GameLevel = level;
        illusionTemple.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        illusionTemple.SaveRankingStatistics = true;
        illusionTemple.AllowParty = true;

        var rewardEntry = RewardTable.First(r => r.GameLevel == level);
        illusionTemple.EntranceFee = rewardEntry.EntranceFee;

        this.CreateRewards(level, illusionTemple);

        return illusionTemple;
    }

    private void CreateRewards(byte level, MiniGameDefinition illusionTemple)
    {
        var rewardEntry = RewardTable.First(r => r.GameLevel == level);

        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = rewardEntry.ExperienceBase;
        illusionTemple.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType = MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = rewardEntry.ExperiencePerSecond;
        illusionTemple.Rewards.Add(remainingSecondsExpReward);

        var winnersMoneyReward = this.Context.CreateNew<MiniGameReward>();
        winnersMoneyReward.RewardType = MiniGameRewardType.Money;
        winnersMoneyReward.RewardAmount = rewardEntry.Money.Winners;
        winnersMoneyReward.RequiredSuccess = MiniGameSuccessFlags.WinnerOrInWinningParty;
        illusionTemple.Rewards.Add(winnersMoneyReward);

        var losersMoneyReward = this.Context.CreateNew<MiniGameReward>();
        losersMoneyReward.RewardType = MiniGameRewardType.Money;
        losersMoneyReward.RewardAmount = rewardEntry.Money.Losers;
        losersMoneyReward.RequiredSuccess = MiniGameSuccessFlags.Loser;
        illusionTemple.Rewards.Add(losersMoneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description = $"Rewarded jewels for Illusion Temple {level}";
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Chaos"));
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Bless"));
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Soul"));
        jewelDropItemGroup.Chance = 0.8;
        this.GameConfiguration.DropItemGroups.Add(jewelDropItemGroup);

        var jewelReward = this.Context.CreateNew<MiniGameReward>();
        jewelReward.ItemReward = jewelDropItemGroup;
        jewelReward.RewardAmount = 1;
        jewelReward.RewardType = MiniGameRewardType.ItemDrop;
        jewelReward.RequiredSuccess = MiniGameSuccessFlags.WinnerOrInWinningParty | MiniGameSuccessFlags.Alive;
        illusionTemple.Rewards.Add(jewelReward);

        // Score rewards
        var winnerAliveScore = this.Context.CreateNew<MiniGameReward>();
        winnerAliveScore.RequiredSuccess = MiniGameSuccessFlags.WinnerOrInWinningParty | MiniGameSuccessFlags.Alive;
        winnerAliveScore.RewardAmount = 500;
        winnerAliveScore.RewardType = MiniGameRewardType.Score;
        illusionTemple.Rewards.Add(winnerAliveScore);

        var winnerDeadScore = this.Context.CreateNew<MiniGameReward>();
        winnerDeadScore.RequiredSuccess = MiniGameSuccessFlags.WinnerOrInWinningParty | MiniGameSuccessFlags.Dead;
        winnerDeadScore.RewardAmount = 250;
        winnerDeadScore.RewardType = MiniGameRewardType.Score;
        illusionTemple.Rewards.Add(winnerDeadScore);

        var loserAliveScore = this.Context.CreateNew<MiniGameReward>();
        loserAliveScore.RequiredSuccess = MiniGameSuccessFlags.Loser | MiniGameSuccessFlags.Alive;
        loserAliveScore.RewardAmount = 200;
        loserAliveScore.RewardType = MiniGameRewardType.Score;
        illusionTemple.Rewards.Add(loserAliveScore);

        var loserDeadScore = this.Context.CreateNew<MiniGameReward>();
        loserDeadScore.RequiredSuccess = MiniGameSuccessFlags.Loser | MiniGameSuccessFlags.Dead;
        loserDeadScore.RewardAmount = 100;
        loserDeadScore.RewardType = MiniGameRewardType.Score;
        illusionTemple.Rewards.Add(loserDeadScore);
    }
}
