// <copyright file="RaklionInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the raklion boss event.
/// </summary>
internal class RaklionInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RaklionInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public RaklionInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var raklion = this.CreateRaklionDefinition(1, RaklionBoss.Number);
        raklion.MinimumCharacterLevel = 280;
        raklion.MaximumCharacterLevel = 400;
        raklion.MinimumSpecialCharacterLevel = 280;
        raklion.MaximumSpecialCharacterLevel = 400;
    }

    private MiniGameDefinition CreateRaklionDefinition(byte level, short mapNumber)
    {
        var raklion = this.Context.CreateNew<MiniGameDefinition>();
        raklion.SetGuid((short)MiniGameType.Raklion, level);
        this.GameConfiguration.MiniGameDefinitions.Add(raklion);
        raklion.Name = $"Raklion {level}";
        raklion.Description =
            $"Event definition for raklion boss event, level {level}.";
        raklion.EnterDuration = TimeSpan.FromMinutes(2);
        raklion.GameDuration = TimeSpan.FromMinutes(30);
        raklion.ExitDuration = TimeSpan.FromMinutes(1);
        raklion.MaximumPlayerCount = 15;
        raklion.Entrance = this.GameConfiguration.Maps
            .First(m => m.Number == mapNumber).ExitGates.First();
        raklion.Type = MiniGameType.Raklion;
        raklion.GameLevel = level;
        raklion.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        raklion.SaveRankingStatistics = true;
        raklion.AllowParty = true;
        raklion.EntranceFee = 500_000;

        this.CreateRewards(level, raklion);

        return raklion;
    }

    private void CreateRewards(byte level, MiniGameDefinition raklion)
    {
        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = 500_000;
        raklion.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType =
            MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = 300;
        raklion.Rewards.Add(remainingSecondsExpReward);

        var moneyReward = this.Context.CreateNew<MiniGameReward>();
        moneyReward.RewardType = MiniGameRewardType.Money;
        moneyReward.RewardAmount = 500_000;
        raklion.Rewards.Add(moneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description =
            $"Rewarded jewels for Raklion {level}";
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
        raklion.Rewards.Add(rank1JewelReward);

        // Rank 2 gets jewel drop
        var rank2JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank2JewelReward.ItemReward = jewelDropItemGroup;
        rank2JewelReward.RewardAmount = 1;
        rank2JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank2JewelReward.Rank = 2;
        raklion.Rewards.Add(rank2JewelReward);

        // Rank 3 gets jewel drop
        var rank3JewelReward = this.Context.CreateNew<MiniGameReward>();
        rank3JewelReward.ItemReward = jewelDropItemGroup;
        rank3JewelReward.RewardAmount = 1;
        rank3JewelReward.RewardType = MiniGameRewardType.ItemDrop;
        rank3JewelReward.Rank = 3;
        raklion.Rewards.Add(rank3JewelReward);
    }
}
