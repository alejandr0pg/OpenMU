// <copyright file="CasinoGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for casino/lumis packets (0xFA identifier).
/// Routes to sub-handlers by subcode.
/// </summary>
[PlugIn]
[Display(Name = "Casino Group Handler", Description = "Handles casino/lumis packets (0xFA).")]
[Guid("E3A1B2C4-D5F6-4A7B-8C9D-0E1F2A3B4C5D")]
internal class CasinoGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key.
    /// </summary>
    internal const byte GroupKey = 0xFA;

    /// <summary>
    /// Initializes a new instance of the <see cref="CasinoGroupHandlerPlugIn" /> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public CasinoGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
