// <copyright file="AddMissingNpcsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This update adds missing NPC definitions (Luke the Helper, Priestess Veina)
/// so the S20 client can interact with them properly.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A7F3B2E1-9C4D-4A8E-B6D5-1E2F3A4B5C6D")]
public class AddMissingNpcsPlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Add missing NPC definitions";

    internal const string PlugInDescription =
        "Adds Luke the Helper (258) and Priestess Veina (567) NPC definitions for S20 client compatibility.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddMissingNpcs;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        this.AddLukeTheHelper(context, gameConfiguration);
        this.AddPriestessVeina(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private void AddLukeTheHelper(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.Any(m => m.Number == 258))
        {
            return;
        }

        var def = context.CreateNew<MonsterDefinition>();
        def.Number = 258;
        def.Designation = "Luke the Helper";
        def.NpcWindow = NpcWindow.LegacyQuest;
        def.ObjectKind = NpcObjectKind.PassiveNpc;
        def.SetGuid(def.Number);
        gameConfiguration.Monsters.Add(def);
    }

    private void AddPriestessVeina(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.Any(m => m.Number == 567))
        {
            return;
        }

        var def = context.CreateNew<MonsterDefinition>();
        def.Number = 567;
        def.Designation = "Priestess Veina";
        def.NpcWindow = NpcWindow.NpcDialog;
        def.ObjectKind = NpcObjectKind.PassiveNpc;
        def.SetGuid(def.Number);
        gameConfiguration.Monsters.Add(def);
    }
}
