// <copyright file="GensJoinRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for gens join request packets (0xF8, 0x01).
/// </summary>
[PlugIn]
[Display(Name = "Gens Join Request Handler", Description = "Handles the gens join request packet.")]
[Guid("B2C3D4E5-F6A7-4B5C-8D9E-0F1A2B3C4D5E")]
[BelongsToGroup(GensGroupHandlerPlugIn.GroupKey)]
public class GensJoinRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x01;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 5 || player.SelectedCharacter is null)
        {
            return;
        }

        var gensType = packet.Span[4];
        if (gensType is not (1 or 2))
        {
            await SendJoinResponseAsync(player, 0x01, 0).ConfigureAwait(false);
            return;
        }

        if (player.SelectedCharacter.GensType != 0)
        {
            await SendJoinResponseAsync(player, 0x01, 0).ConfigureAwait(false);
            return;
        }

        player.SelectedCharacter.GensType = gensType;
        await SendJoinResponseAsync(player, 0x00, gensType).ConfigureAwait(false);
    }

    private static async ValueTask SendJoinResponseAsync(Player player, byte result, byte gensType)
    {
        if (player is not RemotePlayer remotePlayer || remotePlayer.Connection is null)
        {
            return;
        }

        var connection = remotePlayer.Connection;

        int Write()
        {
            const int size = 6;
            var span = connection.Output.GetSpan(size)[..size];
            span[0] = 0xC1;
            span[1] = size;
            span[2] = 0xF8;
            span[3] = 0x01;
            span[4] = result;
            span[5] = gensType;
            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
