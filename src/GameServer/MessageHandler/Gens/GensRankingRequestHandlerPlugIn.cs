// <copyright file="GensRankingRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for gens ranking request packets (0xF8, 0x0B).
/// </summary>
[PlugIn]
[Display(Name = "Gens Ranking Request Handler", Description = "Handles the gens ranking request packet.")]
[Guid("E5F6A7B8-C9D0-4E5F-1A2B-3C4D5E6F7A8B")]
[BelongsToGroup(GensGroupHandlerPlugIn.GroupKey)]
public class GensRankingRequestHandlerPlugIn : ISubPacketHandlerPlugIn,
    ISupportCustomConfiguration<GensRewardConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public GensRewardConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x0B;

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
        var gensType = character.GensType;
        var contribution = character.GensContribution;
        var rank = this.CalculateRank(contribution);
        var connection = remotePlayer.Connection;

        int Write()
        {
            const int size = 13;
            var span = connection.Output.GetSpan(size)[..size];
            span[0] = 0xC1;
            span[1] = size;
            span[2] = 0xF8;
            span[3] = 0x0B;
            span[4] = gensType;
            BinaryPrimitives.WriteInt32BigEndian(span[5..], contribution);
            BinaryPrimitives.WriteInt32BigEndian(span[9..], rank);
            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private int CalculateRank(int contribution)
    {
        int bestRank = 0;
        int bestMin = -1;
        foreach (var tier in this.Configuration!.RewardTiers)
        {
            if (contribution >= tier.MinimumContribution && tier.MinimumContribution > bestMin)
            {
                bestRank = tier.Rank;
                bestMin = tier.MinimumContribution;
            }
        }

        return bestRank;
    }
}
