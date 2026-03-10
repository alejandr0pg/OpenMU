// <copyright file="IllusionTempleEnterHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.MiniGames;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for illusion temple enter request packets.
/// </summary>
[PlugIn]
[Display(Name = "Illusion Temple Enter Handler", Description = "Handles the illusion temple enter request packet.")]
[Guid("A7E3B2C1-9F4D-4A8E-B6D5-1C2E3F4A5B6C")]
internal class IllusionTempleEnterHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly EnterMiniGameAction _enterAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => IllusionTempleEnterRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < IllusionTempleEnterRequest.Length
            || player.SelectedCharacter?.CharacterClass is null)
        {
            return;
        }

        IllusionTempleEnterRequest request = packet;
        if (request.Header.SubCode != IllusionTempleEnterRequest.SubCode)
        {
            return;
        }

        var mapNumber = request.MapNumber;
        var ticketIndex = request.ItemSlot;

        // Map number to game level: IT1 = map 45 → level 1, IT2 = map 46 → level 2, etc.
        byte gameLevel = (byte)(mapNumber >= 45 ? mapNumber - 44 : 1);

        var definition = player.GetSuitableMiniGameDefinition(MiniGameType.IllusionTemple, gameLevel);

        await this._enterAction.TryEnterMiniGameAsync(player, MiniGameType.IllusionTemple, definition?.GameLevel ?? gameLevel, ticketIndex).ConfigureAwait(false);
    }
}
