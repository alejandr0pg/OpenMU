// <copyright file="IVoiceDataRelayPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface for relaying voice chat data to nearby players.
/// </summary>
public interface IVoiceDataRelayPlugIn : IViewPlugIn
{
    /// <summary>
    /// Relays voice data from a speaking player to the observer.
    /// </summary>
    /// <param name="speaker">The player who is speaking.</param>
    /// <param name="senderX">The X coordinate of the speaker.</param>
    /// <param name="senderY">The Y coordinate of the speaker.</param>
    /// <param name="opusData">The Opus-encoded audio data.</param>
    ValueTask RelayVoiceDataAsync(IIdentifiable speaker, byte senderX, byte senderY, ReadOnlyMemory<byte> opusData);
}
