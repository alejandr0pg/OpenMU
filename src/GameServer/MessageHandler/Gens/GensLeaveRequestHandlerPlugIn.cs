// <copyright file="GensLeaveRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for gens leave request packets (0xF8, 0x03).
/// </summary>
[PlugIn]
[Display(Name = "Gens Leave Request Handler", Description = "Handles the gens leave request packet.")]
[Guid("C3D4E5F6-A7B8-4C5D-9E0F-1A2B3C4D5E6F")]
[BelongsToGroup(GensGroupHandlerPlugIn.GroupKey)]
public class GensLeaveRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x03;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        if (player.SelectedCharacter.GensType == 0)
        {
            await SendLeaveResponseAsync(player, 0x01).ConfigureAwait(false);
            return;
        }

        player.SelectedCharacter.GensType = 0;
        await SendLeaveResponseAsync(player, 0x00).ConfigureAwait(false);
    }

    private static async ValueTask SendLeaveResponseAsync(Player player, byte result)
    {
        if (player is not RemotePlayer remotePlayer || remotePlayer.Connection is null)
        {
            return;
        }

        var connection = remotePlayer.Connection;

        int Write()
        {
            const int size = 5;
            var span = connection.Output.GetSpan(size)[..size];
            span[0] = 0xC1;
            span[1] = size;
            span[2] = 0xF8;
            span[3] = 0x03;
            span[4] = result;
            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
