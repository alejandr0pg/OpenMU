// <copyright file="GmVoiceBroadcastAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Action that broadcasts GM voice data to ALL players across all maps.
/// Only players with GameMaster status can use this.
/// </summary>
public class GmVoiceBroadcastAction
{
    /// <summary>
    /// Broadcasts voice data from a GM to every connected player.
    /// </summary>
    /// <param name="player">The GM who is broadcasting.</param>
    /// <param name="opusData">The Opus-encoded voice data.</param>
    public async ValueTask BroadcastAsync(Player player, ReadOnlyMemory<byte> opusData)
    {
        if (player.SelectedCharacter?.CharacterStatus != CharacterStatus.GameMaster)
        {
            return;
        }

        await player.GameContext.ForEachPlayerAsync(
            target => target == player
                ? Task.CompletedTask
                : target.InvokeViewPlugInAsync<IGmVoiceBroadcastRelayPlugIn>(
                    p => p.RelayGmVoiceBroadcastAsync(player, opusData)).AsTask()
        ).ConfigureAwait(false);
    }
}
