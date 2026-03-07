// <copyright file="ShowCharacterListExtendedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameServer.RemoteView.Guild;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowCharacterListPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowCharacterListExtendedPlugIn_Name), Description = nameof(PlugInResources.ShowCharacterListExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("DDDEED0A-9421-4A9B-9ED8-0691B7051666")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowCharacterListExtendedPlugIn : IShowCharacterListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowCharacterListExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowCharacterListExtendedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowCharacterListAsync()
    {
        var connection = this._player.Connection;
        if (connection is null || this._player.Account is not { } account)
        {
            return;
        }

        var unlockFlags = CreateUnlockFlags(account);
        await this.SendCharacterListAsync(connection, account, unlockFlags).ConfigureAwait(false);
        await SendCharacterStatsAsync(connection, account).ConfigureAwait(false);
        if (unlockFlags > CharacterCreationUnlockFlags.None)
        {
            await connection.SendCharacterClassCreationUnlockAsync(unlockFlags).ConfigureAwait(false);
        }
    }

    private static CharacterCreationUnlockFlags CreateUnlockFlags(Account account)
    {
        byte aggregatedFlags = 0;
        var result = account.UnlockedCharacterClasses?
            .Select(c => c.CreationAllowedFlag)
            .Aggregate(aggregatedFlags, (current, flag) => (byte)(current | flag)) ?? 0;
        return (CharacterCreationUnlockFlags)result;
    }

    /// <summary>
    /// Sends a custom packet (F3 F1) with base stats for each character.
    /// Layout per character: SlotIndex(1) + STR(2) + AGI(2) + VIT(2) + ENE(2) + CMD(2) = 11 bytes.
    /// </summary>
    private static async ValueTask SendCharacterStatsAsync(IConnection connection, Account account)
    {
        const int entrySize = 11;
        int count = account.Characters.Count;

        int Write()
        {
            int size = 5 + (count * entrySize); // C2 header(4) + subcode(1) + entries
            var span = connection.Output.GetSpan(size)[..size];
            span[0] = 0xC2;                           // C2 = variable-length header
            span[1] = (byte)((size >> 8) & 0xFF);     // Length high byte
            span[2] = (byte)(size & 0xFF);             // Length low byte
            span[3] = 0xF3;                            // Main code (character)
            span[4] = 0xF1;                            // Sub code (custom: stats)

            int offset = 5;
            foreach (var character in account.Characters.OrderBy(c => c.CharacterSlot))
            {
                span[offset] = character.CharacterSlot;
                WriteUInt16LE(span, offset + 1, GetStat(character, Stats.BaseStrength));
                WriteUInt16LE(span, offset + 3, GetStat(character, Stats.BaseAgility));
                WriteUInt16LE(span, offset + 5, GetStat(character, Stats.BaseVitality));
                WriteUInt16LE(span, offset + 7, GetStat(character, Stats.BaseEnergy));
                WriteUInt16LE(span, offset + 9, GetStat(character, Stats.BaseLeadership));
                offset += entrySize;
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static ushort GetStat(DataModel.Entities.Character character, AttributeDefinition stat)
    {
        return (ushort)(character.Attributes.FirstOrDefault(s => s.Definition == stat)?.Value ?? 0);
    }

    private static void WriteUInt16LE(Span<byte> span, int offset, ushort value)
    {
        span[offset] = (byte)(value & 0xFF);
        span[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private async ValueTask SendCharacterListAsync(IConnection connection, Account account, CharacterCreationUnlockFlags unlockFlags)
    {
        var guildPositions = new GuildPosition?[account.Characters.Count];
        int i = 0;
        foreach (var character in account.Characters)
        {
            guildPositions[i] = await this._player.GameServerContext.GuildServer.GetGuildPositionAsync(character.Id).ConfigureAwait(false);
            i++;
        }

        var appearanceSerializer = this._player.AppearanceSerializer;
        int Write()
        {
            var size = CharacterListExtendedRef.GetRequiredSize(account.Characters.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new CharacterListExtendedRef(span)
            {
                UnlockFlags = unlockFlags,
                CharacterCount = (byte)account.Characters.Count,
                IsVaultExtended = account.IsVaultExtended,
            };

            var j = 0;
            foreach (var character in account.Characters.OrderBy(c => c.CharacterSlot))
            {
                var characterData = packet[j];
                characterData.SlotIndex = character.CharacterSlot;
                characterData.Name = character.Name;
                characterData.Level = (ushort)(character.Attributes.FirstOrDefault(s => s.Definition == Stats.Level)?.Value ?? 1);
                characterData.Status = character.CharacterStatus.Convert();
                characterData.GuildPosition = guildPositions[j].Convert();
                appearanceSerializer.WriteAppearanceData(characterData.Appearance, new CharacterAppearanceDataAdapter(character), false);
                j++;
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}