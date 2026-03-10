// <copyright file="CrywolfInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The initializer for the crywolf defense event.
/// </summary>
internal class CrywolfInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrywolfInitializer" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public CrywolfInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var crywolf = this.CreateCrywolfDefinition(1, CrywolfFortress.Number);
        crywolf.MinimumCharacterLevel = 10;
        crywolf.MaximumCharacterLevel = 400;
        crywolf.MinimumSpecialCharacterLevel = 10;
        crywolf.MaximumSpecialCharacterLevel = 400;
    }

    private MiniGameDefinition CreateCrywolfDefinition(byte level, short mapNumber)
    {
        var crywolf = this.Context.CreateNew<MiniGameDefinition>();
        crywolf.SetGuid((short)MiniGameType.Crywolf, level);
        this.GameConfiguration.MiniGameDefinitions.Add(crywolf);
        crywolf.Name = $"Crywolf {level}";
        crywolf.Description =
            $"Event definition for crywolf defense event, level {level}.";
        crywolf.EnterDuration = TimeSpan.FromMinutes(5);
        crywolf.GameDuration = TimeSpan.FromMinutes(30);
        crywolf.ExitDuration = TimeSpan.FromMinutes(1);
        crywolf.MaximumPlayerCount = 200;
        crywolf.Entrance = this.GameConfiguration.Maps
            .First(m => m.Number == mapNumber).ExitGates.First();
        crywolf.Type = MiniGameType.Crywolf;
        crywolf.GameLevel = level;
        crywolf.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        crywolf.SaveRankingStatistics = true;
        crywolf.AllowParty = true;
        crywolf.EntranceFee = 0;

        this.CreateRewards(level, crywolf);

        return crywolf;
    }

    private void CreateRewards(byte level, MiniGameDefinition crywolf)
    {
        var baseExpReward = this.Context.CreateNew<MiniGameReward>();
        baseExpReward.RewardType = MiniGameRewardType.Experience;
        baseExpReward.RewardAmount = 300_000;
        crywolf.Rewards.Add(baseExpReward);

        var remainingSecondsExpReward = this.Context.CreateNew<MiniGameReward>();
        remainingSecondsExpReward.RewardType =
            MiniGameRewardType.ExperiencePerRemainingSeconds;
        remainingSecondsExpReward.RewardAmount = 200;
        crywolf.Rewards.Add(remainingSecondsExpReward);

        var moneyReward = this.Context.CreateNew<MiniGameReward>();
        moneyReward.RewardType = MiniGameRewardType.Money;
        moneyReward.RewardAmount = 200_000;
        crywolf.Rewards.Add(moneyReward);
    }
}
