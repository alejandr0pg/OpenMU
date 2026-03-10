// <copyright file="KanturuInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the kanturu boss event.
/// </summary>
internal class KanturuInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KanturuInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public KanturuInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var kanturu = this.CreateKanturuDefinition(1, KanturuEvent.Number);
        kanturu.MinimumCharacterLevel = 260;
        kanturu.MaximumCharacterLevel = 400;
        kanturu.MinimumSpecialCharacterLevel = 260;
        kanturu.MaximumSpecialCharacterLevel = 400;
    }

    private MiniGameDefinition CreateKanturuDefinition(byte level, short mapNumber)
    {
        var kanturu = this.Context.CreateNew<MiniGameDefinition>();
        kanturu.SetGuid((short)MiniGameType.Kanturu, level);
        this.GameConfiguration.MiniGameDefinitions.Add(kanturu);
        kanturu.Name = $"Kanturu {level}";
        kanturu.Description =
            $"Event definition for kanturu boss event, level {level}.";
        kanturu.EnterDuration = TimeSpan.FromMinutes(2);
        kanturu.GameDuration = TimeSpan.FromMinutes(20);
        kanturu.ExitDuration = TimeSpan.FromMinutes(1);
        kanturu.MaximumPlayerCount = 15;
        kanturu.Entrance = this.GameConfiguration.Maps
            .First(m => m.Number == mapNumber).ExitGates.First();
        kanturu.Type = MiniGameType.Kanturu;
        kanturu.GameLevel = level;
        kanturu.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        kanturu.SaveRankingStatistics = true;
        kanturu.AllowParty = true;
        kanturu.EntranceFee = 300_000;

        this.CreateRewards(level, kanturu);

        return kanturu;
    }

    private void CreateRewards(byte level, MiniGameDefinition kanturu)
    {
        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = 400_000;
        kanturu.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType =
            MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = 250;
        kanturu.Rewards.Add(remainingSecondsExpReward);

        var moneyReward = this.Context.CreateNew<MiniGameReward>();
        moneyReward.RewardType = MiniGameRewardType.Money;
        moneyReward.RewardAmount = 400_000;
        kanturu.Rewards.Add(moneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description =
            $"Rewarded jewels for Kanturu {level}";
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Bless"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Soul"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Life"));
        jewelDropItemGroup.PossibleItems.Add(
            this.GameConfiguration.Items.First(i => i.Name == "Jewel of Creation"));
        jewelDropItemGroup.Chance = 0.8;
        this.GameConfiguration.DropItemGroups.Add(jewelDropItemGroup);

        // Rank 1 gets jewel drop
        var rank1JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank1JewelReward.ItemReward = jewelDropItemGroup;
        rank1JewelReward.RewardAmount = 1;
        rank1JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank1JewelReward.Rank = 1;
        kanturu.Rewards.Add(rank1JewelReward);

        // Rank 2 gets jewel drop
        var rank2JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank2JewelReward.ItemReward = jewelDropItemGroup;
        rank2JewelReward.RewardAmount = 1;
        rank2JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank2JewelReward.Rank = 2;
        kanturu.Rewards.Add(rank2JewelReward);

        // Rank 3 gets jewel drop
        var rank3JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank3JewelReward.ItemReward = jewelDropItemGroup;
        rank3JewelReward.RewardAmount = 1;
        rank3JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank3JewelReward.Rank = 3;
        kanturu.Rewards.Add(rank3JewelReward);
    }
}
