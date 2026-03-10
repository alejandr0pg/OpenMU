// <copyright file="ImperialGuardianInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the imperial guardian (fortress) event.
/// </summary>
internal class ImperialGuardianInitializer : InitializerBase
{
    /// <summary>
    /// Reward table: (GameLevel, ExperienceBase, ExperiencePerSecond, Money, EntranceFee).
    /// </summary>
    private static readonly List<(int GameLevel, int ExperienceBase, int ExperiencePerSecond, int Money, int EntranceFee)> RewardTable = new()
    {
        (1, 120_000, 150, 150_000, 200_000),
        (2, 250_000, 200, 300_000, 400_000),
        (3, 400_000, 300, 500_000, 600_000),
        (4, 600_000, 400, 800_000, 800_000),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ImperialGuardianInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public ImperialGuardianInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var ig1 = this.CreateImperialGuardianDefinition(1, FortressOfImperialGuardian1.Number);
        ig1.MinimumCharacterLevel = 220;
        ig1.MaximumCharacterLevel = 270;
        ig1.MinimumSpecialCharacterLevel = 220;
        ig1.MaximumSpecialCharacterLevel = 270;

        var ig2 = this.CreateImperialGuardianDefinition(2, FortressOfImperialGuardian2.Number);
        ig2.MinimumCharacterLevel = 271;
        ig2.MaximumCharacterLevel = 320;
        ig2.MinimumSpecialCharacterLevel = 271;
        ig2.MaximumSpecialCharacterLevel = 320;

        var ig3 = this.CreateImperialGuardianDefinition(3, FortressOfImperialGuardian3.Number);
        ig3.MinimumCharacterLevel = 321;
        ig3.MaximumCharacterLevel = 380;
        ig3.MinimumSpecialCharacterLevel = 321;
        ig3.MaximumSpecialCharacterLevel = 380;

        var ig4 = this.CreateImperialGuardianDefinition(4, FortressOfImperialGuardian4.Number);
        ig4.MinimumCharacterLevel = 381;
        ig4.MaximumCharacterLevel = 400;
        ig4.MinimumSpecialCharacterLevel = 381;
        ig4.MaximumSpecialCharacterLevel = 400;
    }

    private MiniGameDefinition CreateImperialGuardianDefinition(byte level, short mapNumber)
    {
        var definition = this.Context.CreateNew<MiniGameDefinition>();
        definition.SetGuid((short)MiniGameType.ImperialGuardian, level);
        this.GameConfiguration.MiniGameDefinitions.Add(definition);
        definition.Name = $"Imperial Guardian {level}";
        definition.Description = $"Event definition for imperial guardian event, level {level}.";
        definition.EnterDuration = TimeSpan.FromMinutes(2);
        definition.GameDuration = TimeSpan.FromMinutes(20);
        definition.ExitDuration = TimeSpan.FromMinutes(1);
        definition.MaximumPlayerCount = 5;
        definition.Entrance = this.GameConfiguration.Maps.First(m => m.Number == mapNumber).ExitGates.First();
        definition.Type = MiniGameType.ImperialGuardian;
        definition.GameLevel = level;
        definition.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        definition.SaveRankingStatistics = true;
        definition.AllowParty = true;

        var ticketItem = this.GameConfiguration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 102);
        if (ticketItem is not null)
        {
            definition.TicketItem = ticketItem;
            definition.TicketItemLevel = level;
        }

        var rewardEntry = RewardTable.First(r => r.GameLevel == level);
        definition.EntranceFee = rewardEntry.EntranceFee;

        this.CreateRewards(level, definition);

        return definition;
    }

    private void CreateRewards(byte level, MiniGameDefinition definition)
    {
        var rewardEntry = RewardTable.First(r => r.GameLevel == level);

        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = rewardEntry.ExperienceBase;
        definition.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType = MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = rewardEntry.ExperiencePerSecond;
        definition.Rewards.Add(remainingSecondsExpReward);

        var moneyReward = this.Context.CreateNew<MiniGameReward>();
        moneyReward.RewardType = MiniGameRewardType.Money;
        moneyReward.RewardAmount = rewardEntry.Money;
        definition.Rewards.Add(moneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description = $"Rewarded jewels for Imperial Guardian {level}";
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Bless"));
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Soul"));
        jewelDropItemGroup.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name == "Jewel of Life"));
        jewelDropItemGroup.Chance = 0.7;
        this.GameConfiguration.DropItemGroups.Add(jewelDropItemGroup);

        var jewelReward = this.Context.CreateNew<MiniGameReward>();
        jewelReward.ItemReward = jewelDropItemGroup;
        jewelReward.RewardAmount = 1;
        jewelReward.RewardType = MiniGameRewardType.ItemDrop;
        jewelReward.Rank = 1;
        definition.Rewards.Add(jewelReward);
    }
}
