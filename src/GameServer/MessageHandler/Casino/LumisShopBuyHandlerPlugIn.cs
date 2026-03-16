// <copyright file="LumisShopBuyHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for Lumis shop buy requests.
/// Packet in:  C3 14 FA 04 [shopItemId:16].
/// Packet out: C3 0E FA 05 [success:1] [newBalance:8] [group:1] [number:2] [slot:1].
/// </summary>
[PlugIn]
[Display(Name = "Lumis Shop Buy Handler",
    Description = "Handles Lumis shop buy request packets (0xFA 0x04).")]
[Guid("C2D3E4F5-A6B7-4C8D-9E0F-1A2B3C4D5E6F")]
[BelongsToGroup(CasinoGroupHandlerPlugIn.GroupKey)]
public class LumisShopBuyHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const byte MainCode = 0xFA;
    private const byte SubCode = 0x04;
    private const byte ResponseSubCode = 0x05;
    private const int ExpectedLength = 20;

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < ExpectedLength || packet.Span[3] != SubCode)
        {
            return;
        }

        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            await SendResponseAsync(player, false, 0, 0, 0).ConfigureAwait(false);
            return;
        }

        var shopItemId = new Guid(packet.Span[4..20]);
        var shopItem = LumisShopCatalogCache.GetItemById(shopItemId);

        if (shopItem is null)
        {
            player.Logger.LogWarning("Lumis buy rejected: item {Id} not found.", shopItemId);
            await SendResponseAsync(player, false, 0, 0, 0).ConfigureAwait(false);
            return;
        }

        var account = player.Account;
        if (account is null)
        {
            return;
        }

        if (account.LumisBalance < shopItem.Price)
        {
            player.Logger.LogWarning(
                "Lumis buy rejected: balance {Balance} < price {Price}.",
                account.LumisBalance, shopItem.Price);
            await SendResponseAsync(player, false, 0, 0, 0).ConfigureAwait(false);
            return;
        }

        var itemDef = FindItemDefinition(player, shopItem.Group, shopItem.Number);
        if (itemDef is null)
        {
            player.Logger.LogWarning(
                "Lumis buy rejected: no item def for group {G} number {N}.",
                shopItem.Group, shopItem.Number);
            await SendResponseAsync(player, false, 0, 0, 0).ConfigureAwait(false);
            return;
        }

        var newItem = CreateItem(player, itemDef, shopItem.Level);
        var slot = player.Inventory!.CheckInvSpace(newItem);
        if (slot is null)
        {
            player.Logger.LogWarning("Lumis buy rejected: inventory full.");
            await SendResponseAsync(player, false, 0, 0, 0).ConfigureAwait(false);
            return;
        }

        account.LumisBalance -= shopItem.Price;
        newItem.ItemSlot = (byte)slot;
        await player.Inventory.AddItemAsync(newItem).ConfigureAwait(false);

        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(
            p => p.ItemAppearAsync(newItem)).ConfigureAwait(false);

        player.Logger.LogInformation(
            "Lumis buy: item {G}/{N}+{L}, price={Price}, balance={Bal}, slot={Slot}.",
            shopItem.Group, shopItem.Number, shopItem.Level,
            shopItem.Price, account.LumisBalance, newItem.ItemSlot);

        await SendResponseAsync(
            player, true,
            (byte)shopItem.Group, (ushort)shopItem.Number,
            newItem.ItemSlot).ConfigureAwait(false);

        await SendBalanceUpdateAsync(player).ConfigureAwait(false);
    }

    private static ItemDefinition? FindItemDefinition(Player player, short group, short number)
    {
        return player.GameContext.Configuration.Items
            .FirstOrDefault(d => d.Group == group && d.Number == number);
    }

    private static Item CreateItem(Player player, ItemDefinition definition, short level)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = (byte)level;
        item.Durability = definition.Durability;
        item.HasSkill = definition.Skill is not null;
        return item;
    }

    private static async ValueTask SendResponseAsync(
        Player player, bool success, byte group, ushort number, byte slot)
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

        const int len = 14;
        var balance = player.Account?.LumisBalance ?? 0;
        var ok = (byte)(success ? 1 : 0);

        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(len)[..len];
            span[0] = 0xC3;
            span[1] = (byte)len;
            span[2] = MainCode;
            span[3] = ResponseSubCode;
            span[4] = ok;
            BinaryPrimitives.WriteInt64LittleEndian(span[5..], balance);
            span[13] = slot;
            return len;
        }).ConfigureAwait(false);
    }

    private static async ValueTask SendBalanceUpdateAsync(Player player)
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

        const int len = 14;
        var lumis = player.Account?.LumisBalance ?? 0;
        var vip = (byte)(player.Account?.VipExpiresAt > DateTime.UtcNow ? 1 : 0);

        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(len)[..len];
            span[0] = 0xC3;
            span[1] = (byte)len;
            span[2] = 0xFA;
            span[3] = 0x01;
            BinaryPrimitives.WriteInt64LittleEndian(span[4..], lumis);
            span[12] = vip;
            span[13] = 0;
            return len;
        }).ConfigureAwait(false);
    }
}
