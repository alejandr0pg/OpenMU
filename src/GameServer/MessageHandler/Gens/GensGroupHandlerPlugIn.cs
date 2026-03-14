// <copyright file="GensGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Gens;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for gens packets (0xF8 identifier).
/// </summary>
[PlugIn]
[Display(Name = "Gens Group Handler", Description = "Handles gens system packets (0xF8).")]
[Guid("A1B2C3D4-E5F6-4A5B-9C8D-7E6F5A4B3C2D")]
internal class GensGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key.
    /// </summary>
    internal const byte GroupKey = 0xF8;

    /// <summary>
    /// Initializes a new instance of the <see cref="GensGroupHandlerPlugIn" /> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public GensGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
