// <copyright file="PartyDistributor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using Nito.AsyncEx;

/// <summary>
/// Handles experience and money distribution among party members.
/// </summary>
internal sealed class PartyDistributor
{
    private readonly List<Player> _distributionList;
    private readonly AsyncLock _distributionLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyDistributor"/> class.
    /// </summary>
    /// <param name="maxPartySize">Maximum party size for pre-allocation.</param>
    public PartyDistributor(byte maxPartySize)
    {
        this._distributionList = new List<Player>(maxPartySize);
    }

    /// <summary>
    /// Distributes experience after a kill to party members.
    /// </summary>
    /// <param name="partyMembers">Snapshot of party members.</param>
    /// <param name="killedObject">The killed object.</param>
    /// <param name="killer">The killer.</param>
    /// <returns>Total distributed experience.</returns>
    public async ValueTask<int> DistributeExperienceAfterKillAsync(
        IReadOnlyList<IPartyMember> partyMembers,
        IAttackable killedObject,
        IObservable killer)
    {
        using var d = await this._distributionLock.LockAsync();
        try
        {
            return await this.CalculateAndDistributeExpAsync(partyMembers, killedObject, killer).ConfigureAwait(false);
        }
        finally
        {
            this._distributionList.Clear();
        }
    }

    /// <summary>
    /// Distributes money after a kill to party members.
    /// </summary>
    /// <param name="partyMembers">Snapshot of party members.</param>
    /// <param name="killedObject">The killed object.</param>
    /// <param name="killer">The killer.</param>
    /// <param name="amount">The money amount.</param>
    public async ValueTask DistributeMoneyAfterKillAsync(
        IReadOnlyList<IPartyMember> partyMembers,
        IAttackable killedObject,
        IPartyMember killer,
        uint amount)
    {
        using var d = await this._distributionLock.LockAsync();
        try
        {
            this._distributionList.AddRange(
                partyMembers.OfType<Player>()
                    .Where(p => p.CurrentMap == killer.CurrentMap && !p.IsAtSafezone() && p.Attributes is { }));

            if (this._distributionList.Count == 0)
            {
                return;
            }

            var moneyPart = amount / this._distributionList.Count;
            this._distributionList.ForEach(p =>
                p.TryAddMoney((int)(moneyPart * p.Attributes![Stats.MoneyAmountRate])));
        }
        finally
        {
            this._distributionList.Clear();
        }
    }

    /// <summary>
    /// Gets the quest drop item groups for the whole party.
    /// </summary>
    /// <param name="partyMembers">Snapshot of party members.</param>
    /// <param name="killer">The killer.</param>
    /// <returns>The list of drop item groups.</returns>
    public async ValueTask<IList<DropItemGroup>> GetQuestDropItemGroupsAsync(
        IReadOnlyList<IPartyMember> partyMembers,
        IPartyMember killer)
    {
        using var d = await this._distributionLock.LockAsync();
        try
        {
            using (await killer.ObserverLock.ReaderLockAsync().ConfigureAwait(false))
            {
                this._distributionList.AddRange(
                    partyMembers.OfType<Player>()
                        .Where(p => p.CurrentMap == killer.CurrentMap
                                    && !p.IsAtSafezone()
                                    && p.IsAlive
                                    && (p == killer || killer.Observers.Contains(p))));
            }

            IList<DropItemGroup> result = [];
            var dropItemGroups = this._distributionList
                .SelectMany(m => m.SelectedCharacter?.GetQuestDropItemGroups() ?? Enumerable.Empty<DropItemGroup>());

            foreach (var dropItemGroup in dropItemGroups)
            {
                if (result.Count == 0)
                {
                    result = new List<DropItemGroup>();
                }

                result.Add(dropItemGroup);
            }

            return result;
        }
        finally
        {
            this._distributionList.Clear();
        }
    }

    private async ValueTask<int> CalculateAndDistributeExpAsync(
        IReadOnlyList<IPartyMember> partyMembers,
        IAttackable killedObject,
        IObservable killer)
    {
        if (killedObject.IsSummonedMonster)
        {
            return 0;
        }

        using (await killer.ObserverLock.ReaderLockAsync())
        {
            this._distributionList.AddRange(
                partyMembers.OfType<Player>()
                    .Where(p => p == killer || killer.Observers.Contains(p)));
        }

        var count = this._distributionList.Count;
        if (count == 0)
        {
            return 0;
        }

        var totalLevel = this._distributionList.Sum(p => (int)p.Attributes![Stats.TotalLevel]);
        var averageLevel = totalLevel / count;
        var averageExperience = killedObject.CalculateBaseExperience(averageLevel);
        var totalAverageExperience = averageExperience * count * Math.Pow(1.05, count - 1);
        totalAverageExperience *= killedObject.CurrentMap?.Definition.ExpMultiplier ?? 1;
        totalAverageExperience *= this._distributionList.First().GameContext.ExperienceRate;

        var randomizedTotalExperience = Rand.NextInt((int)(totalAverageExperience * 0.8), (int)(totalAverageExperience * 1.2));
        var randomizedTotalExperiencePerLevel = randomizedTotalExperience / totalLevel;

        foreach (var player in this._distributionList)
        {
            if ((short)player.Attributes![Stats.Level] == player.GameContext.Configuration.MaximumLevel)
            {
                if (player.SelectedCharacter?.CharacterClass?.IsMasterClass ?? false)
                {
                    var expMaster = (int)(randomizedTotalExperiencePerLevel * player.Attributes![Stats.TotalLevel]
                        * (player.Attributes[Stats.MasterExperienceRate] + player.Attributes[Stats.BonusExperienceRate]));
                    await player.AddMasterExperienceAsync(expMaster, killedObject).ConfigureAwait(false);
                }
            }
            else
            {
                var exp = (int)(randomizedTotalExperiencePerLevel * player.Attributes![Stats.Level]
                    * (player.Attributes[Stats.ExperienceRate] + player.Attributes[Stats.BonusExperienceRate]));
                await player.AddExperienceAsync(exp, killedObject).ConfigureAwait(false);
            }
        }

        return randomizedTotalExperience;
    }
}
