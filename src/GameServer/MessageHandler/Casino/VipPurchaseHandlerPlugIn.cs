// <copyright file="VipPurchaseHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for VIP package purchase requests.
/// Packet: C3 [len] FA 30 [packageType:1].
/// </summary>
[PlugIn]
[Display(Name = "VIP Purchase Handler",
    Description = "Handles VIP purchase request packets (0xFA 0x30).")]
[Guid("D4F2A8C1-7B3E-4D6A-9E1F-5C8B2A4D6F0E")]
internal class VipPurchaseHandlerPlugIn : IPacketHandlerPlugIn
{
    private const byte MainCode = 0xFA;
    private const byte SubCode = 0x30;
    private const byte ResponseSubCode = 0x31;
    private const int ExpectedLength = 5; // C3(1) + len(1) + main(1) + sub(1) + packageType(1)
    private const int ResponsePacketLength = 15; // C3(1) + len(1) + main(1) + sub(1) + success(1) + lumisBalance(8) + vipDays(2)

    private static readonly (int Days, long Cost)[] Packages =
    {
        (7, 200),    // packageType 0
        (30, 500),   // packageType 1
        (90, 1200),  // packageType 2
    };

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

        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            player.Logger.LogWarning("VIP purchase rejected: player not in EnteredWorld state.");
            await SendResponseAsync(player, success: false).ConfigureAwait(false);
            return;
        }

        var packageType = span[4];
        if (packageType >= Packages.Length)
        {
            player.Logger.LogWarning("VIP purchase rejected: invalid package type {Type}.", packageType);
            await SendResponseAsync(player, success: false).ConfigureAwait(false);
            return;
        }

        var account = player.Account;
        if (account is null)
        {
            return;
        }

        var (days, cost) = Packages[packageType];

        if (account.LumisBalance < cost)
        {
            player.Logger.LogWarning(
                "VIP purchase rejected: insufficient Lumís. Has {Balance}, needs {Cost}.",
                account.LumisBalance, cost);
            await SendResponseAsync(player, success: false).ConfigureAwait(false);
            return;
        }

        account.LumisBalance -= cost;

        var now = DateTime.UtcNow;
        var baseDate = account.VipExpiresAt.HasValue && account.VipExpiresAt.Value > now
            ? account.VipExpiresAt.Value
            : now;
        account.VipExpiresAt = baseDate.AddDays(days);

        player.Logger.LogInformation(
            "VIP activated: package={Days}d, cost={Cost} Lumís, expires={Expires}, balance={Balance}.",
            days, cost, account.VipExpiresAt, account.LumisBalance);

        await SendResponseAsync(player, success: true).ConfigureAwait(false);
    }

    private static async ValueTask SendResponseAsync(Player player, bool success)
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

        var account = player.Account;
        var balance = account?.LumisBalance ?? 0;
        var vipDays = (ushort)0;

        if (account?.VipExpiresAt is { } expiry && expiry > DateTime.UtcNow)
        {
            vipDays = (ushort)Math.Min((expiry - DateTime.UtcNow).Days, ushort.MaxValue);
        }

        var successByte = (byte)(success ? 1 : 0);

        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(ResponsePacketLength)[..ResponsePacketLength];
            span[0] = 0xC3;
            span[1] = (byte)ResponsePacketLength;
            span[2] = MainCode;
            span[3] = ResponseSubCode;
            span[4] = successByte;
            BinaryPrimitives.WriteInt64LittleEndian(span[5..], balance);
            BinaryPrimitives.WriteUInt16LittleEndian(span[13..], vipDays);
            return ResponsePacketLength;
        }).ConfigureAwait(false);
    }
}
