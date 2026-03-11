// <copyright file="GmVoiceBroadcastRelayPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends GM voice broadcast data to the client using sub-code 0x02.
/// </summary>
[PlugIn]
[Display(Name = "GM Voice Broadcast Relay", Description = "Relays GM voice broadcast to the client.")]
[Guid("C2D3E4F5-A6B7-4C8D-9E0F-1A2B3C4D5E6F")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
public class GmVoiceBroadcastRelayPlugIn : IGmVoiceBroadcastRelayPlugIn
{
    private const byte PacketType = 0xC2;
    private const byte VoiceCode = 0xD5;
    private const byte GmBroadcastSubCode = 0x02;
    private const int HeaderSize = 7; // C2(1) + Len(2) + Code(1) + Sub(1) + SenderId(2)

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmVoiceBroadcastRelayPlugIn"/> class.
    /// </summary>
    public GmVoiceBroadcastRelayPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask RelayGmVoiceBroadcastAsync(
        IIdentifiable speaker, ReadOnlyMemory<byte> opusData)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        var senderId = speaker.GetId(this._player);
        var totalLength = HeaderSize + opusData.Length;

        using (await connection.OutputLock.LockAsync().ConfigureAwait(false))
        {
            var span = connection.Output.GetSpan(totalLength)[..totalLength];
            span[0] = PacketType;
            span[1] = (byte)(totalLength >> 8);
            span[2] = (byte)(totalLength & 0xFF);
            span[3] = VoiceCode;
            span[4] = GmBroadcastSubCode;
            span[5] = (byte)(senderId >> 8);
            span[6] = (byte)(senderId & 0xFF);
            opusData.Span.CopyTo(span[HeaderSize..]);
            connection.Output.Advance(totalLength);
            await connection.Output.FlushAsync().ConfigureAwait(false);
        }
    }
}
