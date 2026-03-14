// <copyright file="GensContributionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Awards gens contribution points when a player kills a member of the opposing faction.
/// </summary>
[PlugIn]
[Display(Name = "Gens Contribution", Description = "Awards contribution points for killing opposing faction members.")]
[Guid("F6A7B8C9-D0E1-4F5A-2B3C-4D5E6F7A8B9C")]
public class GensContributionPlugIn : IAttackableGotKilledPlugIn,
    ISupportCustomConfiguration<GensContributionPlugIn.GensContributionConfig>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public GensContributionConfig? Configuration { get; set; }

    /// <inheritdoc/>
    public object CreateDefaultConfig() => new GensContributionConfig();

    /// <inheritdoc/>
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killer is not Player killerPlayer || killed is not Player killedPlayer)
        {
            return ValueTask.CompletedTask;
        }

        var killerChar = killerPlayer.SelectedCharacter;
        var killedChar = killedPlayer.SelectedCharacter;
        if (killerChar is null || killedChar is null)
        {
            return ValueTask.CompletedTask;
        }

        if (killerChar.GensType == 0 || killedChar.GensType == 0)
        {
            return ValueTask.CompletedTask;
        }

        if (killerChar.GensType == killedChar.GensType)
        {
            return ValueTask.CompletedTask;
        }

        this.Configuration ??= new GensContributionConfig();
        killerChar.GensContribution += this.Configuration.PointsPerKill;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Configuration for the gens contribution plugin.
    /// </summary>
    public class GensContributionConfig
    {
        /// <summary>
        /// Gets or sets the contribution points per kill.
        /// </summary>
        [Display(Name = "Points Per Kill", Description = "Contribution points awarded per opposing faction kill.")]
        public int PointsPerKill { get; set; } = 3;
    }
}
