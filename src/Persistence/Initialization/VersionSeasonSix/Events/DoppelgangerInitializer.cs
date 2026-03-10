// <copyright file="DoppelgangerInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the doppelganger event.
/// </summary>
internal class DoppelgangerInitializer : InitializerBase
{
    /// <summary>
    /// Reward table: (GameLevel, ExperienceBase, ExperiencePerSecond, (WinnersMoney, LosersMoney), EntranceFee).
    /// </summary>
    private static readonly List<(int GameLevel, int ExperienceBase, int ExperiencePerSecond, int Money, int EntranceFee)> RewardTable = new()
    {
        (1, 80_000, 100, 100_000, 50_000),
        (2, 150_000, 150, 200_000, 100_000),
        (3, 250_000, 200, 350_000, 200_000),
        (4, 400_000, 280, 500_000, 350_000),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DoppelgangerInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public DoppelgangerInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var dg1 = this.CreateDoppelgangerDefinition(1, Doppelgaenger1.Number);
        dg1.MinimumCharacterLevel = 20;
        dg1.MaximumCharacterLevel = 150;
        dg1.MinimumSpecialCharacterLevel = 20;
        dg1.MaximumSpecialCharacterLevel = 150;

        var dg2 = this.CreateDoppelgangerDefinition(2, Doppelgaenger2.Number);
        dg2.MinimumCharacterLevel = 151;
        dg2.MaximumCharacterLevel = 260;
        dg2.MinimumSpecialCharacterLevel = 151;
        dg2.MaximumSpecialCharacterLevel = 260;

        var dg3 = this.CreateDoppelgangerDefinition(3, Doppelgaenger3.Number);
        dg3.MinimumCharacterLevel = 261;
        dg3.MaximumCharacterLevel = 350;
        dg3.MinimumSpecialCharacterLevel = 261;
        dg3.MaximumSpecialCharacterLevel = 350;

        var dg4 = this.CreateDoppelgangerDefinition(4, Doppelgaenger4.Number);
        dg4.MinimumCharacterLevel = 351;
        dg4.MaximumCharacterLevel = 400;
        dg4.MinimumSpecialCharacterLevel = 351;
        dg4.MaximumSpecialCharacterLevel = 400;
    }

    /// <summary>
    /// Creates a new <see cref="MiniGameDefinition"/> for a doppelganger event.
    /// </summary>
    /// <param name="level">The level of the event.</param>
    /// <param name="mapNumber">The map number.</param>
    /// <returns>The created <see cref="MiniGameDefinition"/>.</returns>
    protected MiniGameDefinition CreateDoppelgangerDefinition(byte level, short mapNumber)
    {
        var doppelganger = this.Context.CreateNew<MiniGameDefinition>();
        doppelganger.SetGuid((short)MiniGameType.Doppelganger, level);
        this.GameConfiguration.MiniGameDefinitions.Add(doppelganger);
        doppelganger.Name = $"Doppelganger {level}";
        doppelganger.Description = $"Event definition for doppelganger event, level {level}.";
        doppelganger.EnterDuration = TimeSpan.FromMinutes(2);
        doppelganger.GameDuration = TimeSpan.FromMinutes(20);
        doppelganger.ExitDuration = TimeSpan.FromMinutes(1);
        doppelganger.MaximumPlayerCount = 5;
        doppelganger.Entrance = this.GameConfiguration.Maps.First(m => m.Number == mapNumber).ExitGates.First();
        doppelganger.Type = MiniGameType.Doppelganger;
        doppelganger.GameLevel = level;
        doppelganger.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        doppelganger.SaveRankingStatistics = true;
        doppelganger.AllowParty = true;

        var rewardEntry = RewardTable.First(r => r.GameLevel == level);
        doppelganger.EntranceFee = rewardEntry.EntranceFee;

        this.CreateRewards(level, doppelganger);

        return doppelganger;
    }

    private void CreateRewards(byte level, MiniGameDefinition doppelganger)
    {
        var rewardEntry = RewardTable.First(r => r.GameLevel == level);

        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = rewardEntry.ExperienceBase;
        doppelganger.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType = MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = rewardEntry.ExperiencePerSecond;
        doppelganger.Rewards.Add(remainingSecondsExpReward);

        var moneyReward = this.Context.CreateNew<MiniGameReward>();
        moneyReward.RewardType = MiniGameRewardType.Money;
        moneyReward.RewardAmount = rewardEntry.Money;
        doppelganger.Rewards.Add(moneyReward);

        var jewelDropItemGroup = this.Context.CreateNew<DropItemGroup>();
        jewelDropItemGroup.Description = $"Rewarded jewels for Doppelganger {level}";
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
        doppelganger.Rewards.Add(jewelReward);
    }
}
