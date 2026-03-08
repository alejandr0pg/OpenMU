// <copyright file="VoiceDataRelayPlugIn.cs" company="MUnique">
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
/// Sends relayed voice chat data to the client.
/// </summary>
[PlugIn]
[Display(Name = "Voice Data Relay", Description = "Relays voice data to the client.")]
[Guid("A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
public class VoiceDataRelayPlugIn : IVoiceDataRelayPlugIn
{
    private const byte PacketType = 0xC2;
    private const byte VoiceCode = 0xD5;
    private const byte VoiceSubCode = 0x01;
    private const int HeaderSize = 9;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceDataRelayPlugIn"/> class.
    /// </summary>
    public VoiceDataRelayPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask RelayVoiceDataAsync(
        IIdentifiable speaker, byte senderX, byte senderY, ReadOnlyMemory<byte> opusData)
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
            span[4] = VoiceSubCode;
            span[5] = (byte)(senderId >> 8);
            span[6] = (byte)(senderId & 0xFF);
            span[7] = senderX;
            span[8] = senderY;
            opusData.Span.CopyTo(span[HeaderSize..]);
            connection.Output.Advance(totalLength);
            await connection.Output.FlushAsync().ConfigureAwait(false);
        }
    }
}
