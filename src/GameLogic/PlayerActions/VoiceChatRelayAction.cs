// <copyright file="VoiceChatRelayAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Action that relays voice chat data to nearby players using the observer system.
/// </summary>
public class VoiceChatRelayAction
{
    /// <summary>
    /// Relays the voice data from the sender to all nearby observers.
    /// </summary>
    /// <param name="player">The player who is speaking.</param>
    /// <param name="opusData">The Opus-encoded voice data.</param>
    public async ValueTask RelayAsync(Player player, ReadOnlyMemory<byte> opusData)
    {
        if (!player.IsAlive || player.CurrentMap is null)
        {
            return;
        }

        var x = player.Position.X;
        var y = player.Position.Y;

        await player.ForEachWorldObserverAsync<IVoiceDataRelayPlugIn>(
            p => p.RelayVoiceDataAsync(player, x, y, opusData),
            false).ConfigureAwait(false);
    }
}
