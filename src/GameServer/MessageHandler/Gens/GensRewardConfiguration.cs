// <copyright file="GensRewardConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

/// <summary>
/// Configuration for the gens reward system.
/// </summary>
public class GensRewardConfiguration
{
    /// <summary>
    /// Gets or sets the minimum contribution required to claim a reward.
    /// </summary>
    [Display(Name = "Minimum Contribution", Description = "Minimum contribution points needed to claim a reward.")]
    public int MinimumContribution { get; set; } = 10;

    /// <summary>
    /// Gets or sets the contribution points awarded per opposing faction kill.
    /// </summary>
    [Display(Name = "Points Per Kill", Description = "Contribution points awarded per opposing faction kill.")]
    public int PointsPerKill { get; set; } = 3;

    /// <summary>
    /// Gets or sets the reward tiers. Evaluated from highest rank first.
    /// </summary>
    [Display(Name = "Reward Tiers", Description = "Reward tiers by minimum contribution.")]
    public IList<GensRewardTier> RewardTiers { get; set; } = new List<GensRewardTier>
    {
        new(10000, 10_000_000, 1),
        new(5000, 5_000_000, 2),
        new(2500, 2_500_000, 3),
        new(1000, 1_000_000, 4),
        new(500, 500_000, 5),
        new(200, 200_000, 6),
        new(100, 100_000, 7),
        new(50, 50_000, 8),
        new(10, 10_000, 9),
        new(1, 1_000, 10),
    };
}

/// <summary>
/// A single reward tier for the gens system.
/// </summary>
public class GensRewardTier
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GensRewardTier"/> class.
    /// </summary>
    public GensRewardTier()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GensRewardTier"/> class.
    /// </summary>
    public GensRewardTier(int minContribution, int zenReward, int rank)
    {
        this.MinimumContribution = minContribution;
        this.ZenReward = zenReward;
        this.Rank = rank;
    }

    /// <summary>
    /// Gets or sets the minimum contribution for this tier.
    /// </summary>
    [Display(Name = "Min Contribution", Description = "Minimum contribution points for this tier.")]
    public int MinimumContribution { get; set; }

    /// <summary>
    /// Gets or sets the zen reward amount.
    /// </summary>
    [Display(Name = "Zen Reward", Description = "Zen awarded when claiming reward at this tier.")]
    public int ZenReward { get; set; }

    /// <summary>
    /// Gets or sets the rank number for this tier.
    /// </summary>
    [Display(Name = "Rank", Description = "Rank number for this contribution tier (1=highest).")]
    public int Rank { get; set; }
}
