// <copyright file="IGmVoiceBroadcastRelayPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface for relaying GM voice broadcast data to all players.
/// </summary>
public interface IGmVoiceBroadcastRelayPlugIn : IViewPlugIn
{
    /// <summary>
    /// Relays GM voice broadcast data to a player.
    /// </summary>
    /// <param name="speaker">The GM who is broadcasting.</param>
    /// <param name="opusData">The Opus-encoded audio data.</param>
    ValueTask RelayGmVoiceBroadcastAsync(IIdentifiable speaker, ReadOnlyMemory<byte> opusData);
}
