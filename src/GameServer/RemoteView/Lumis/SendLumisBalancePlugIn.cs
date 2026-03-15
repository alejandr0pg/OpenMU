namespace MUnique.OpenMU.GameServer.RemoteView.Lumis;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends the Lumís balance to the client when the player enters the world.
/// </summary>
[PlugIn]
[Display(Name = "Send Lumís Balance On Login", Description = "Sends Lumís balance and VIP status when player enters world.", ResourceType = typeof(PlugInResources))]
[Guid("B7E3A1F2-4C8D-4E9B-A5F6-1D2E3F4A5B6C")]
public class SendLumisBalancePlugIn : IPlayerStateChangedPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld)
        {
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

        const int len = 14;
        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(len)[..len];
            span[0] = 0xC3;
            span[1] = (byte)len;
            span[2] = 0xFA;
            span[3] = 0x01;
            var lumis = player.Account?.LumisBalance ?? 0;
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span[4..], lumis);
            span[12] = (byte)(player.Account?.VipExpiresAt > DateTime.UtcNow ? 1 : 0);
            span[13] = 0;
            return len;
        }).ConfigureAwait(false);
    }
}
