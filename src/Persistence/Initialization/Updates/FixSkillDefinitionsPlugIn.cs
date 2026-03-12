// <copyright file="FixSkillDefinitionsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Fixes skill definitions (Name, AttackDamage, Range, DamageType, SkillType) to match
/// the current SkillsInitializer values. Safe to run on existing databases — only updates
/// the specified fields; does NOT recreate skills or touch character/player data.
/// </summary>
[Display(Name = PlugInName, Description = PlugInDescription)]
[PlugIn]
[Guid("B9C4D2E1-F3A7-4B8C-9D6E-A2F1C3B5D7E9")]
public partial class FixSkillDefinitionsPlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Fix Skill Definitions";
    internal const string PlugInDescription = "Fixes skill Name, AttackDamage, Range, DamageType and SkillType to match current initializer values.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FixSkillDefinitions;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var lookup = BuildSkillLookup();

        foreach (var skill in gameConfiguration.Skills)
        {
            if (!lookup.TryGetValue((SkillNumber)skill.Number, out var def))
            {
                continue;
            }

            skill.Name = def.Name;
            skill.AttackDamage = def.Damage;
            skill.Range = def.Range;
            skill.DamageType = def.DamageType;
            skill.SkillType = def.SkillType;
        }

        return ValueTask.CompletedTask;
    }

    private static Dictionary<SkillNumber, SkillDef> BuildSkillLookup()
    {
        var result = new Dictionary<SkillNumber, SkillDef>();
        foreach (var d in GetCoreSkillDefs())
        {
            result[d.Number] = d;
        }

        foreach (var d in GetMasterSkillDefs())
        {
            result[d.Number] = d;
        }

        return result;
    }

    /// <summary>Lightweight description of a skill's key fields.</summary>
    internal readonly record struct SkillDef(
        SkillNumber Number,
        string Name,
        int Damage,
        short Range,
        DamageType DamageType,
        SkillType SkillType);
}
