// <copyright file="ICastleSiegeStateViewPlugin.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Interface of a view whose implementation informs about the state of a castle siege event.
/// </summary>
public interface ICastleSiegeStateViewPlugin : IViewPlugIn
{
    /// <summary>
    /// Updates the state of the castle siege event.
    /// </summary>
    /// <param name="playState">The play state (0=idle, 1=playing, 2=ended).</param>
    /// <param name="remainingSeconds">The remaining time in seconds.</param>
    /// <param name="crownHolder">The crown holder (0=none, 1=attackers, 2=defenders).</param>
    ValueTask UpdateStateAsync(byte playState, ushort remainingSeconds, byte crownHolder);
}
