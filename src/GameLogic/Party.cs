// <copyright file="Party.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Diagnostics.Metrics;
using System.Threading;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Party;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

/// <summary>
/// The party object. Contains a group of players who can chat with each other,
/// and get information about the health status of their party mates.
/// </summary>
public sealed class Party : Disposable
{
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<int> PartyCount = Meter.CreateCounter<int>("PartyCount");

    private readonly ILogger<Party> _logger;
    private readonly Timer _healthUpdate;
    private readonly byte _maxPartySize;
    private readonly AsyncLock _partyListLock = new();
    private readonly PartyDistributor _distributor;

    /// <summary>
    /// Initializes a new instance of the <see cref="Party" /> class.
    /// </summary>
    /// <param name="maxPartySize">Maximum size of the party.</param>
    /// <param name="logger">Logger of this party.</param>
    public Party(byte maxPartySize, ILogger<Party> logger)
    {
        this._maxPartySize = maxPartySize;
        this._logger = logger;
        this._distributor = new PartyDistributor(maxPartySize);

        this.PartyList = new List<IPartyMember>(maxPartySize);
        var updateInterval = new TimeSpan(0, 0, 0, 0, 500);
        this._healthUpdate = new Timer(this.HealthUpdateElapsed, null, updateInterval, updateInterval);
        PartyCount.Add(1);
    }

    /// <summary>
    /// Gets the party list.
    /// </summary>
    public IList<IPartyMember> PartyList { get; }

    /// <summary>
    /// Gets the maximum size of the party.
    /// </summary>
    public byte MaxPartySize => this._maxPartySize;

    /// <summary>
    /// Gets the party master.
    /// </summary>
    public IPartyMember? PartyMaster { get; private set; }

    /// <summary>
    /// Gets the name of the meter of this class.
    /// </summary>
    internal static string MeterName => typeof(Party).FullName ?? nameof(Party);

    /// <summary>
    /// Kicks the player from the party.
    /// </summary>
    /// <param name="sender">The sender.</param>
    public async ValueTask KickMySelfAsync(IPartyMember sender)
    {
        using var l = await this._partyListLock.LockAsync();
        for (int i = 0; i < this.PartyList.Count; i++)
        {
            if (this.PartyList[i].Id == sender.Id)
            {
                await this.ExitPartyAsync(this.PartyList[i], (byte)i).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// Kicks the player from the party.
    /// </summary>
    /// <param name="index">The party list index of the member to kick.</param>
    public async ValueTask KickPlayerAsync(byte index)
    {
        using var l = await this._partyListLock.LockAsync();
        if (index >= this.PartyList.Count)
        {
            return;
        }

        var toKick = this.PartyList[index];
        await this.ExitPartyAsync(toKick, index).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the specified new party mate.
    /// </summary>
    /// <param name="newPartyMate">The new party mate.</param>
    /// <returns><c>True</c>, if adding was successful; Otherwise, <c>false</c>.</returns>
    public async ValueTask<bool> AddAsync(IPartyMember newPartyMate)
    {
        using var l = await this._partyListLock.LockAsync();
        if (this.PartyList.Count >= this._maxPartySize)
        {
            return false;
        }

        if (this.PartyList.Count == 0)
        {
            this.PartyMaster = newPartyMate;
        }

        this.PartyList.Add(newPartyMate);
        newPartyMate.Party = this;
        await this.SendPartyListAsync().ConfigureAwait(false);
        await this.UpdateNearbyCountAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Sends the chat message to all party members.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="senderCharacterName">The sender character name.</param>
    public async ValueTask SendChatMessageAsync(string message, string senderCharacterName)
    {
        using var l = await this._partyListLock.LockAsync();
        for (int i = 0; i < this.PartyList.Count; i++)
        {
            try
            {
                await this.PartyList[i].InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(message, senderCharacterName, ChatMessageType.Party)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "Error sending the chat message");
            }
        }
    }

    /// <inheritdoc cref="PartyDistributor.DistributeExperienceAfterKillAsync"/>
    public async ValueTask<int> DistributeExperienceAfterKillAsync(IAttackable killedObject, IObservable killer)
    {
        IReadOnlyList<IPartyMember> snapshot;
        using (await this._partyListLock.LockAsync())
        {
            snapshot = this.PartyList.ToArray();
        }

        return await this._distributor.DistributeExperienceAfterKillAsync(snapshot, killedObject, killer).ConfigureAwait(false);
    }

    /// <inheritdoc cref="PartyDistributor.DistributeMoneyAfterKillAsync"/>
    public async ValueTask DistributeMoneyAfterKillAsync(IAttackable killedObject, IPartyMember killer, uint amount)
    {
        IReadOnlyList<IPartyMember> snapshot;
        using (await this._partyListLock.LockAsync())
        {
            snapshot = this.PartyList.ToArray();
        }

        await this._distributor.DistributeMoneyAfterKillAsync(snapshot, killedObject, killer, amount).ConfigureAwait(false);
    }

    /// <inheritdoc cref="PartyDistributor.GetQuestDropItemGroupsAsync"/>
    public async ValueTask<IList<DropItemGroup>> GetQuestDropItemGroupsAsync(IPartyMember killer)
    {
        IReadOnlyList<IPartyMember> snapshot;
        using (await this._partyListLock.LockAsync())
        {
            snapshot = this.PartyList.ToArray();
        }

        return await this._distributor.GetQuestDropItemGroupsAsync(snapshot, killer).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.PartyList.Count > 0)
        {
            for (byte i = 0; i < this.PartyList.Count; i++)
            {
                try
                {
                    var index = i;
                    this.PartyList[i].InvokeViewPlugInAsync<IPartyMemberRemovedPlugIn>(p => p.PartyMemberRemovedAsync(index)).AsTask().WaitWithoutException();
                    this.PartyList[i].Party = null;
                }
                catch (Exception ex)
                {
                    this._logger.LogDebug(ex, "error at dispose");
                }
            }

            this.PartyList.Clear();
        }

        this._healthUpdate.Dispose();
        PartyCount.Add(-1);
    }

    private async ValueTask ExitPartyAsync(IPartyMember player, byte index)
    {
        if (this.PartyList.Count < 3 || Equals(this.PartyMaster, player))
        {
            this.Dispose();
            return;
        }

        this.PartyList.Remove(player);
        player.Party = null;
        try
        {
            await player.InvokeViewPlugInAsync<IPartyMemberRemovedPlugIn>(p => p.PartyMemberRemovedAsync(index)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogDebug(ex, "Error when calling PartyMemberRemoved. Already disconnected?");
        }

        await this.SendPartyListAsync().ConfigureAwait(false);
        await this.UpdateNearbyCountAsync().ConfigureAwait(false);
        if (player is Player actualPlayer && actualPlayer.Attributes is { } attributes)
        {
            attributes[Stats.NearbyPartyMemberCount] = 0;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Catching all Exceptions.")]
    private async void HealthUpdateElapsed(object? state)
    {
        try
        {
            using var l = await this._partyListLock.LockAsync();
            var partyMaster = this.PartyList.FirstOrDefault();
            if (partyMaster is null)
            {
                return;
            }

            bool updateNeeded = partyMaster.ViewPlugIns.GetPlugIn<IPartyHealthViewPlugIn>()?.IsHealthUpdateNeeded() ?? false;
            if (!updateNeeded)
            {
                return;
            }

            await partyMaster.InvokeViewPlugInAsync<IPartyHealthViewPlugIn>(p => p.UpdatePartyHealthAsync()).ConfigureAwait(false);
            for (var i = this.PartyList.Count - 1; i >= 1; i--)
            {
                var plugIn = this.PartyList[i].ViewPlugIns.GetPlugIn<IPartyHealthViewPlugIn>();
                if (plugIn?.IsHealthUpdateNeeded() ?? false)
                {
                    await plugIn.UpdatePartyHealthAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogDebug(ex, "Unexpected error during health update");
        }
    }

    private async ValueTask UpdateNearbyCountAsync()
    {
        for (byte i = 0; i < this.PartyList.Count; i++)
        {
            try
            {
                if (this.PartyList[i] is not Player player || player.Attributes is not { } attributes)
                {
                    continue;
                }

                using var readerLock = await player.ObserverLock.ReaderLockAsync().ConfigureAwait(false);
                attributes[Stats.NearbyPartyMemberCount] = this.PartyList.Count(player.Observers.Contains);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "Error updating {statsName}", nameof(Stats.NearbyPartyMemberCount));
            }
        }
    }

    private async ValueTask SendPartyListAsync()
    {
        for (byte i = 0; i < this.PartyList.Count; i++)
        {
            try
            {
                await this.PartyList[i].InvokeViewPlugInAsync<IUpdatePartyListPlugIn>(p => p.UpdatePartyListAsync()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "Error sending party list update");
            }
        }
    }
}
