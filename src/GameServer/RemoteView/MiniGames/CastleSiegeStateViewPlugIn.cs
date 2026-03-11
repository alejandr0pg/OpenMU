// <copyright file="CastleSiegeStateViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.MiniGames;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends castle siege state updates to the client via packet 0xB1 0x01.
/// </summary>
[PlugIn]
[Guid("D8E5F1A2-3B7C-4D9E-8F01-2A3B4C5D6E7F")]
public class CastleSiegeStateViewPlugIn : ICastleSiegeStateViewPlugin
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeStateViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeStateViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask UpdateStateAsync(byte playState, ushort remainingSeconds, byte crownHolder)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        // Packet: C1 08 B1 01 [playState] [remainHi] [remainLo] [crownHolder]
        const int size = 8;
        var span = connection.Output.GetSpan(size)[..size];
        span[0] = 0xC1;
        span[1] = (byte)size;
        span[2] = 0xB1;
        span[3] = 0x01;
        span[4] = playState;
        span[5] = (byte)(remainingSeconds >> 8);
        span[6] = (byte)(remainingSeconds & 0xFF);
        span[7] = crownHolder;
        connection.Output.Advance(size);
        await connection.Output.FlushAsync().ConfigureAwait(false);
    }
}
