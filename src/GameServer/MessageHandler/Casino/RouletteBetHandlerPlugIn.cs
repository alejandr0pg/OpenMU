// <copyright file="RouletteBetHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for casino roulette bet requests.
/// Packet: C3 [len] FA 15 [betType:1] [betNumber:1] [betAmountLE:8].
/// </summary>
[PlugIn]
[Display(Name = "Roulette Bet Handler",
    Description = "Handles roulette bet request packets (0xFA 0x15).")]
[Guid("D4F6A2E8-3B1C-4E9D-A5F7-8C2E6D0B4A19")]
internal class RouletteBetHandlerPlugIn : IPacketHandlerPlugIn
{
    private const byte MainCode = 0xFA;
    private const byte SubCode = 0x15;
    private const byte ResponseSubCode = 0x16;

    // C3(1) + len(1) + main(1) + sub(1) + betType(1) + betNumber(1) + betAmount(8) = 14
    private const int ExpectedLength = 14;

    // C3(1) + len(1) + main(1) + sub(1) + winNumber(1) + isRed(1) + winAmount(8) + newZen(4) = 18
    private const int ResponsePacketLength = 18;

    private const long MinBet = 50_000;

    private readonly Random _rng = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => MainCode;

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

        var betType = (RouletteBetType)span[4];
        var betNumber = span[5];
        var betAmount = BinaryPrimitives.ReadInt64LittleEndian(span[6..]);

        if (!RouletteGameLogic.IsValidBet(betType, betNumber))
        {
            player.Logger.LogWarning("Roulette rejected: invalid bet type {Type} / number {Num}.", betType, betNumber);
            return;
        }

        var maxBet = MUnique.OpenMU.GameLogic.PlugIns.VipHelper.MaxRouletteBet(player);
        if (betAmount < MinBet || betAmount > maxBet)
        {
            player.Logger.LogWarning(
                "Roulette rejected: bet {Bet} out of range [{Min}..{Max}].",
                betAmount, MinBet, maxBet);
            return;
        }

        if (player.PlayerState.CurrentState != PlayerState.NpcDialogOpened)
        {
            player.Logger.LogWarning("Roulette rejected: player not in NPC dialog state.");
            return;
        }

        if (!player.TryRemoveMoney((int)betAmount))
        {
            player.Logger.LogWarning(
                "Roulette rejected: insufficient Zen. Has {Money}, needs {Bet}.",
                player.Money, betAmount);
            return;
        }

        var result = RouletteGameLogic.Spin(this._rng);
        var payout = RouletteGameLogic.CalculatePayout(betType, betNumber, result.WinningNumber, betAmount);

        if (payout > 0)
        {
            player.TryAddMoney((int)payout);
        }

        player.Logger.LogInformation(
            "Roulette: bet={Bet}, type={Type}, number={BetNum}, won={Won}, payout={Payout}, newZen={Zen}.",
            betAmount, betType, betNumber, result.WinningNumber, payout, player.Money);

        await this.SendResultAsync(player, result, payout).ConfigureAwait(false);
    }

    private async ValueTask SendResultAsync(Player player, RouletteResult result, long winAmount)
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
        var winningNumber = result.WinningNumber;
        var isRed = result.IsRed ? (byte)1 : (byte)0;

        int WritePacket()
        {
            var span = connection.Output.GetSpan(ResponsePacketLength)[..ResponsePacketLength];
            span[0] = 0xC3;
            span[1] = ResponsePacketLength;
            span[2] = MainCode;
            span[3] = ResponseSubCode;
            span[4] = winningNumber;
            span[5] = isRed;
            BinaryPrimitives.WriteInt64LittleEndian(span[6..], winAmount);
            BinaryPrimitives.WriteUInt32LittleEndian(span[14..], newZen);
            return ResponsePacketLength;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
