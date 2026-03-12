// <copyright file="FixFireBlastSkillTypePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Ensures FireBlast (skill 74) is configured as a normal DirectHit skill
/// with the correct damage attribute relationships, so it deals damage in
/// regular gameplay — not only during Castle Siege.
/// </summary>
[Display(Name = PlugInName, Description = PlugInDescription)]
[PlugIn]
[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public class FixFireBlastSkillTypePlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Fix FireBlast Skill Type";
    internal const string PlugInDescription = "Ensures FireBlast is DirectHit and has correct SkillBaseDamageBonus attribute relationships.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FixFireBlastSkillType;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var fireBlast = gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.FireBlast);
        if (fireBlast is null)
        {
            return ValueTask.CompletedTask;
        }

        // Ensure the skill type is DirectHit so it fires outside Castle Siege.
        fireBlast.SkillType = SkillType.DirectHit;

        // Remove all existing attribute relationships to avoid duplicates from
        // prior migration runs that may have pointed to stale attribute instances.
        foreach (var rel in fireBlast.AttributeRelationships.ToList())
        {
            fireBlast.AttributeRelationships.Remove(rel);
        }

        // Re-add the canonical relationships using GetPersistent(), which always
        // resolves to the single correct AttributeDefinition entity in the DB.
        var skillBaseDamageBonus = Stats.SkillBaseDamageBonus.GetPersistent(gameConfiguration);
        var totalStrength = Stats.TotalStrength.GetPersistent(gameConfiguration);
        var totalEnergy = Stats.TotalEnergy.GetPersistent(gameConfiguration);

        var strRel = CharacterClassHelper.CreateAttributeRelationship(
            context, gameConfiguration,
            skillBaseDamageBonus, 1.0f / 25, totalStrength,
            InputOperator.Multiply, AggregateType.AddRaw);
        fireBlast.AttributeRelationships.Add(strRel);

        var eneRel = CharacterClassHelper.CreateAttributeRelationship(
            context, gameConfiguration,
            skillBaseDamageBonus, 1.0f / 50, totalEnergy,
            InputOperator.Multiply, AggregateType.AddRaw);
        fireBlast.AttributeRelationships.Add(eneRel);

        return ValueTask.CompletedTask;
    }
}
