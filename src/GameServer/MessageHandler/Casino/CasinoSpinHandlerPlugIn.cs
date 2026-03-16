// <copyright file="CasinoSpinHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Casino;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for casino slot-machine spin requests.
/// Packet: C3 [len] FA 11 [betAmountLE:8].
/// </summary>
[PlugIn]
[Display(Name = "Casino Spin Handler",
    Description = "Handles slot-machine spin request packets (0xFA 0x11).")]
[Guid("A7E3B1C4-9D2F-4F8A-B6E5-1C3D5A7F9B2E")]
[BelongsToGroup(CasinoGroupHandlerPlugIn.GroupKey)]
public class CasinoSpinHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const byte MainCode = 0xFA;
    private const byte SubCode = 0x11;
    private const byte ResponseSubCode = 0x12;
    private const int ExpectedLength = 12; // C3(1) + len(1) + main(1) + sub(1) + bet(8)
    private const int ResponseLength = 16; // C3(1) + len(1) + main(1) + sub(1) + r1-r3(3) + win(8) + zen(4) = 19... recalc

    private const int MinBet = 10_000;

    // C3(1) + len(1) + main(1) + sub(1) + r1(1) + r2(1) + r3(1) + winAmount(8) + newZen(4) = 18
    private const int ResponsePacketLength = 18;

    private readonly Random _rng = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < ExpectedLength)
        {
            return;
        }

        var span = packet.Span;
        if (span[3] != SubCode)
        {
            return;
        }

        var betAmount = BinaryPrimitives.ReadInt64LittleEndian(span[4..]);

        var maxBet = MUnique.OpenMU.GameLogic.PlugIns.VipHelper.MaxSlotBet(player);
        if (betAmount < MinBet || betAmount > maxBet)
        {
            player.Logger.LogWarning(
                "Casino spin rejected: bet {Bet} out of range [{Min}..{Max}].",
                betAmount, MinBet, maxBet);
            return;
        }

        if (player.PlayerState.CurrentState != PlayerState.NpcDialogOpened)
        {
            player.Logger.LogWarning("Casino spin rejected: player not in NPC dialog state.");
            return;
        }

        if (!player.TryRemoveMoney((int)betAmount))
        {
            player.Logger.LogWarning(
                "Casino spin rejected: insufficient Zen. Has {Money}, needs {Bet}.",
                player.Money, betAmount);
            return;
        }

        var result = SlotsGameLogic.Spin(betAmount, this._rng);

        if (result.WinAmount > 0)
        {
            player.TryAddMoney((int)result.WinAmount);
        }

        player.Logger.LogInformation(
            "Casino spin: bet={Bet}, reels=[{R1},{R2},{R3}], multiplier={Mult}x, win={Win}, newZen={Zen}.",
            betAmount,
            result.Reel1, result.Reel2, result.Reel3,
            result.Multiplier,
            result.WinAmount,
            player.Money);

        await this.SendResultAsync(player, result).ConfigureAwait(false);
    }

    private async ValueTask SendResultAsync(Player player, SpinResult result)
    {
        if (player is not RemotePlayer remotePlayer)
        {
            return;
        }

        var connection = remotePlayer.Connection;
        if (connection is null)
        {
            return;
        }

        var newZen = (uint)player.Money;
        var winAmount = result.WinAmount;
        var r1 = (byte)result.Reel1;
        var r2 = (byte)result.Reel2;
        var r3 = (byte)result.Reel3;

        int WritePacket()
        {
            var span = connection.Output.GetSpan(ResponsePacketLength)[..ResponsePacketLength];
            span[0] = 0xC3;
            span[1] = ResponsePacketLength;
            span[2] = MainCode;
            span[3] = ResponseSubCode;
            span[4] = r1;
            span[5] = r2;
            span[6] = r3;
            BinaryPrimitives.WriteInt64LittleEndian(span[7..], winAmount);
            BinaryPrimitives.WriteUInt32LittleEndian(span[15..], newZen);
            return ResponsePacketLength;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
