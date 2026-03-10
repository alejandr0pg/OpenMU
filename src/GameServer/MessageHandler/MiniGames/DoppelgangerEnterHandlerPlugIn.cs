// <copyright file="DoppelgangerEnterHandlerPlugIn.cs" company="MUnique">
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
/// Handler for doppelganger enter request packets.
/// </summary>
[PlugIn]
[Display(Name = "Doppelganger Enter Handler", Description = "Handles the doppelganger enter request packet.")]
[Guid("B8F4C3D2-A05E-5B9F-C7E6-2D3F4A5B6C7D")]
internal class DoppelgangerEnterHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly EnterMiniGameAction _enterAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => DoppelgangerEnterRequestRef.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < DoppelgangerEnterRequestRef.Length
            || player.SelectedCharacter?.CharacterClass is null)
        {
            return;
        }

        DoppelgangerEnterRequestRef request = packet.Span;
        if (request.Header.SubCode != DoppelgangerEnterRequestRef.SubCode)
        {
            return;
        }

        var ticketIndex = request.TicketItemSlot;

        // Determine game level from player level.
        var definition = player.GetSuitableMiniGameDefinition(MiniGameType.Doppelganger, 0);
        var gameLevel = definition?.GameLevel ?? 1;

        await this._enterAction.TryEnterMiniGameAsync(player, MiniGameType.Doppelganger, gameLevel, ticketIndex).ConfigureAwait(false);
    }
}
