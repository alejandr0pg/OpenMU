// <copyright file="VoiceChatDataHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for voice chat data packets (code 0xD5).
/// </summary>
[PlugIn]
[Display(Name = "Voice Chat Handler", Description = "Handles voice chat data packets and relays to nearby players.")]
[Guid("B8E2C3A1-5D4F-4E6B-9A1C-7F3D2E8B4C5A")]
internal class VoiceChatDataHandlerPlugIn : IPacketHandlerPlugIn
{
    private const byte VoicePacketCode = 0xD5;
    private const byte VoiceDataSubCode = 0x01;
    private const byte GmBroadcastSubCode = 0x02;
    private const int MaxVoicePayload = 4000;
    private const int MinPacketLength = 6; // C2(1) + LenHi(1) + LenLo(1) + Code(1) + SubCode(1) + Data(1+)
    private const int VoiceDataOffset = 5; // data starts after C2 header + code + subcode

    private readonly VoiceChatRelayAction _relayAction = new();
    private readonly GmVoiceBroadcastAction _gmBroadcastAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => VoicePacketCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < MinPacketLength)
        {
            return;
        }

        var span = packet.Span;
        var subCode = span[4];
        var voiceDataLength = packet.Length - VoiceDataOffset;
        if (voiceDataLength <= 0 || voiceDataLength > MaxVoicePayload)
        {
            return;
        }

        var opusData = packet.Slice(VoiceDataOffset, voiceDataLength);

        if (subCode == VoiceDataSubCode)
        {
            await this._relayAction.RelayAsync(player, opusData).ConfigureAwait(false);
        }
        else if (subCode == GmBroadcastSubCode)
        {
            await this._gmBroadcastAction.BroadcastAsync(player, opusData).ConfigureAwait(false);
        }
    }
}
