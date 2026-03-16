// <copyright file="LumisShopCatalogHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for Lumis shop catalog requests.
/// Packet in:  C3 04 FA 02.
/// Packet out: C4 [len:2] FA 03 [catCount:1] [itemCount:2] [categories...] [items...].
/// </summary>
[PlugIn]
[Display(Name = "Lumis Shop Catalog Handler",
    Description = "Handles Lumis shop catalog request packets (0xFA 0x02).")]
[Guid("B1C2D3E4-F5A6-4B7C-8D9E-0F1A2B3C4D5E")]
[BelongsToGroup(CasinoGroupHandlerPlugIn.GroupKey)]
public class LumisShopCatalogHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const byte MainCode = 0xFA;
    private const byte SubCode = 0x02;

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 4 || packet.Span[3] != SubCode)
        {
            return;
        }

        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            player.Logger.LogWarning("Lumis catalog rejected: not in EnteredWorld.");
            return;
        }

        var catalogPacket = await LumisShopCatalogCache.GetOrBuildPacketAsync()
            .ConfigureAwait(false);

        if (catalogPacket is null)
        {
            player.Logger.LogWarning("Lumis catalog: failed to load from DB.");
            return;
        }

        if (player is not RemotePlayer remotePlayer)
        {
            return;
        }

        var connection = remotePlayer.Connection;
        if (connection is null)
        {
            return;
        }

        var data = catalogPacket;
        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(data.Length)[..data.Length];
            data.CopyTo(span);
            return data.Length;
        }).ConfigureAwait(false);
    }
}
