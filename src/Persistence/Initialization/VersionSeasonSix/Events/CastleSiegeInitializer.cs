// <copyright file="CastleSiegeInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the castle siege event.
/// </summary>
internal class CastleSiegeInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public CastleSiegeInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var cs = this.CreateCastleSiegeDefinition(1, LandOfTrials.Number);
        cs.MinimumCharacterLevel = 200;
        cs.MaximumCharacterLevel = 400;
        cs.MinimumSpecialCharacterLevel = 200;
        cs.MaximumSpecialCharacterLevel = 400;
    }

    private MiniGameDefinition CreateCastleSiegeDefinition(byte level, short mapNumber)
    {
        var castleSiege = this.Context.CreateNew<MiniGameDefinition>();
        castleSiege.SetGuid((short)MiniGameType.CastleSiege, level);
        this.GameConfiguration.MiniGameDefinitions.Add(castleSiege);
        castleSiege.Name = $"Castle Siege {level}";
        castleSiege.Description =
            $"Event definition for castle siege event, level {level}.";
        castleSiege.EnterDuration = TimeSpan.FromMinutes(5);
        castleSiege.GameDuration = TimeSpan.FromMinutes(30);
        castleSiege.ExitDuration = TimeSpan.FromMinutes(2);
        castleSiege.MaximumPlayerCount = 100;
        castleSiege.Entrance = this.GameConfiguration.Maps
            .First(m => m.Number == mapNumber).ExitGates.First();
        castleSiege.Type = MiniGameType.CastleSiege;
        castleSiege.GameLevel = level;
        castleSiege.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        castleSiege.SaveRankingStatistics = true;
        castleSiege.AllowParty = true;
        castleSiege.EntranceFee = 0;

        this.CreateRewards(level, castleSiege);

        return castleSiege;
    }

    private void CreateRewards(byte level, MiniGameDefinition castleSiege)
    {
        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = 1_000_000;
        castleSiege.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType =
            MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = 500;
        castleSiege.Rewards.Add(remainingSecondsExpReward);

        var winnersMoneyReward = this.Context.CreateNew<MiniGameReward>();
        winnersMoneyReward.RewardType = MiniGameRewardType.Money;
        winnersMoneyReward.RewardAmount = 1_000_000;
        winnersMoneyReward.RequiredSuccess =
            MiniGameSuccessFlags.WinnerOrInWinningParty;
        castleSiege.Rewards.Add(winnersMoneyReward);

        var losersMoneyReward = this.Context.CreateNew<MiniGameReward>();
        losersMoneyReward.RewardType = MiniGameRewardType.Money;
        losersMoneyReward.RewardAmount = 200_000;
        losersMoneyReward.RequiredSuccess = MiniGameSuccessFlags.Loser;
        castleSiege.Rewards.Add(losersMoneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description =
            $"Rewarded jewels for Castle Siege {level}";
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Bless"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Soul"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Life"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Creation"));
        jewelDropItemGroup.Chance = 0.9;
        this.GameConfiguration.DropItemGroups.Add(jewelDropItemGroup);

        var rank1JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank1JewelReward.ItemReward = jewelDropItemGroup;
        rank1JewelReward.RewardAmount = 2;
        rank1JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank1JewelReward.Rank = 1;
        castleSiege.Rewards.Add(rank1JewelReward);

        var rank2JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank2JewelReward.ItemReward = jewelDropItemGroup;
        rank2JewelReward.RewardAmount = 1;
        rank2JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank2JewelReward.Rank = 2;
        castleSiege.Rewards.Add(rank2JewelReward);

        var rank3JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank3JewelReward.ItemReward = jewelDropItemGroup;
        rank3JewelReward.RewardAmount = 1;
        rank3JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank3JewelReward.Rank = 3;
        castleSiege.Rewards.Add(rank3JewelReward);
    }
}
