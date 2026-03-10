// <copyright file="CastleSiegeStartConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The castle siege start configuration.
/// </summary>
public class CastleSiegeStartConfiguration : MiniGameStartConfiguration
{
    /// <summary>
    /// Gets the default configuration for castle siege.
    /// </summary>
    public static CastleSiegeStartConfiguration Default =>
        new()
        {
            PreStartMessageDelay = TimeSpan.FromMinutes(5),
            EntranceOpenedMessage =
                "Castle Siege entrance is open and closes in {0} minute(s).",
            EntranceClosedMessage = "Castle Siege entrance closed.",
            TaskDuration = TimeSpan.FromMinutes(40),
            Timetable = PeriodicTaskConfiguration
                .GenerateTimeSequence(TimeSpan.FromHours(6)).ToList(),
        };
}
