// <copyright file="GensRewardRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for gens reward request packets (0xF8, 0x09).
/// </summary>
[PlugIn]
[Display(Name = "Gens Reward Request Handler", Description = "Handles the gens reward request packet.")]
[Guid("D4E5F6A7-B8C9-4D5E-0F1A-2B3C4D5E6F7A")]
[BelongsToGroup(GensGroupHandlerPlugIn.GroupKey)]
public class GensRewardRequestHandlerPlugIn : ISubPacketHandlerPlugIn,
    ISupportCustomConfiguration<GensRewardConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public GensRewardConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x09;

    /// <inheritdoc/>
    public object CreateDefaultConfig() => new GensRewardConfiguration();

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null ||
            player is not RemotePlayer remotePlayer ||
            remotePlayer.Connection is null)
        {
            return;
        }

        this.Configuration ??= new GensRewardConfiguration();
        var character = player.SelectedCharacter;

        if (character.GensType == 0 ||
            character.GensContribution < this.Configuration.MinimumContribution)
        {
            await SendRewardResponseAsync(remotePlayer, 0x01).ConfigureAwait(false);
            return;
        }

        var tier = this.FindRewardTier(character.GensContribution);
        if (tier is null)
        {
            await SendRewardResponseAsync(remotePlayer, 0x01).ConfigureAwait(false);
            return;
        }

        player.TryAddMoney(tier.ZenReward);
        character.GensContribution = 0;

        await SendRewardResponseAsync(remotePlayer, 0x00).ConfigureAwait(false);
    }

    private GensRewardTier? FindRewardTier(int contribution)
    {
        GensRewardTier? best = null;
        foreach (var tier in this.Configuration!.RewardTiers)
        {
            if (contribution >= tier.MinimumContribution &&
                (best is null || tier.MinimumContribution > best.MinimumContribution))
            {
                best = tier;
            }
        }

        return best;
    }

    private static async ValueTask SendRewardResponseAsync(RemotePlayer remotePlayer, byte result)
    {
        var connection = remotePlayer.Connection;
        if (connection is null)
        {
            return;
        }

        int Write()
        {
            const int size = 5;
            var span = connection.Output.GetSpan(size)[..size];
            span[0] = 0xC1;
            span[1] = size;
            span[2] = 0xF8;
            span[3] = 0x09;
            span[4] = result;
            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
