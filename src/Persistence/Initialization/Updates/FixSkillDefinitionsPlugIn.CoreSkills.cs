// <copyright file="FixSkillDefinitionsPlugIn.CoreSkills.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;

public partial class FixSkillDefinitionsPlugIn
{
    private static IEnumerable<SkillDef> GetCoreSkillDefs()
    {
        // Wizard / SM / GM
        yield return new(SkillNumber.Poison,        "Poison",           12,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.Meteorite,     "Meteorite",        21,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.Lightning,     "Lightning",        17,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.FireBall,      "Fire Ball",         8,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.Flame,         "Flame",            25,  6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Teleport,      "Teleport",          0,  6, DamageType.Wizardry, SkillType.Other);
        yield return new(SkillNumber.Ice,           "Ice",              10,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.Twister,       "Twister",          35,  6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.EvilSpirit,    "Evil Spirit",      45,  7, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Hellfire,      "Hellfire",        120,  4, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.PowerWave,     "Power Wave",       14,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.AquaBeam,      "Aqua Beam",        80,  6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Cometfall,     "Cometfall",        70,  3, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Inferno,       "Inferno",         100,  4, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.TeleportAlly,  "Teleport Ally",     0,  6, DamageType.None,     SkillType.Other);
        yield return new(SkillNumber.SoulBarrier,   "Soul Barrier",      0,  6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.EnergyBall,    "Energy Ball",       3,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.Decay,         "Decay",            95,  6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.IceStorm,      "Ice Storm",        80,  6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Nova,          "Nova",              0,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.NovaStart,     "Nova (Start)",      0,  0, DamageType.None,     SkillType.Other);
        yield return new(SkillNumber.Lance,         "Lance",            90,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.ManaRays,      "Mana Rays",        85,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.PlasmaStorm,   "Plasma Storm",     60,  6, DamageType.Fenrir,   SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.ExpansionofWizardry, "Expansion of Wizardry", 0, 6, DamageType.None, SkillType.Buff);

        // Knight / BK / BM
        yield return new(SkillNumber.Defense,           "Defense",          0, 0, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.FallingSlash,      "Falling Slash",    0, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Lunge,             "Lunge",            0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Uppercut,          "Uppercut",         0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Cyclone,           "Cyclone",          0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Slash,             "Slash",            0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TwistingSlash,     "Twisting Slash",   0, 2, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.RagefulBlow,       "Rageful Blow",     60, 3, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.DeathStab,         "Death Stab",       70, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.CrescentMoonSlash, "Crescent Moon Slash", 90, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Impale,            "Impale",           15, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.SwellLife,         "Swell Life",        0, 0, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.FireBreath,        "Fire Breath",      30, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.StrikeofDestruction, "Strike of Destruction", 110, 5, DamageType.Physical, SkillType.AreaSkillAutomaticHits);

        // Elf / ME / HE
        yield return new(SkillNumber.TripleShot,     "Triple Shot",     0, 6, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Heal,           "Heal",            0, 6, DamageType.None,     SkillType.Regeneration);
        yield return new(SkillNumber.GreaterDefense, "Greater Defense", 0, 6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.GreaterDamage,  "Greater Damage",  0, 6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.SummonGoblin,      "Summon Goblin",       0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonStoneGolem,  "Summon Stone Golem",  0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonAssassin,    "Summon Assassin",     0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonEliteYeti,   "Summon Elite Yeti",   0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonDarkKnight,  "Summon Dark Knight",  0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonBali,        "Summon Bali",         0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.SummonSoldier,     "Summon Soldier",      0, 0, DamageType.None, SkillType.SummonMonster);
        yield return new(SkillNumber.Starfall,      "Starfall",        120, 8, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.IceArrow,      "Ice Arrow",       105, 8, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Penetration,   "Penetration",      70, 6, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.InfinityArrow, "Infinity Arrow",    0, 6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Recovery,      "Recovery",          0, 6, DamageType.None,     SkillType.Regeneration);
        yield return new(SkillNumber.MultiShot,     "Multi-Shot",       40, 6, DamageType.Physical, SkillType.AreaSkillAutomaticHits);

        // MG / DM
        yield return new(SkillNumber.FireSlash,       "Fire Slash",      80, 2, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.PowerSlash,      "Power Slash",      0, 5, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.SpiralSlash,     "Spiral Slash",    75, 5, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.FlameStrike,     "Flame Strike",   140, 3, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.GiganticStorm,   "Gigantic Storm", 110, 6, DamageType.Wizardry, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.DoppelgangerSelfExplosion, "Doppelganger Self Explosion", 140, 3, DamageType.Wizardry, SkillType.DirectHit);

        // Dark Lord / LE
        yield return new(SkillNumber.Force,               "Force",               10,  4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.FireBurst,           "Fire Burst",         100,  6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.Earthshake,          "Earthshake",         150, 10, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Summon,              "Summon",               0,  0, DamageType.None,     SkillType.Other);
        yield return new(SkillNumber.IncreaseCriticalDamage, "Increase Critical Damage", 0, 0, DamageType.None, SkillType.Buff);
        yield return new(SkillNumber.ElectricSpike,       "Electric Spike",     250, 10, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.ForceWave,           "Force Wave",          50,  4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.AbolishMagic,        "Abolish Magic",        0,  0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.FireBlast,           "Fire Blast",         150,  6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.FireScream,          "Fire Scream",        130,  6, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.Explosion79,         "Explosion",            0,  2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.ChaoticDiseier,      "Chaotic Diseier",    190,  6, DamageType.Physical, SkillType.AreaSkillAutomaticHits);

        // Castle siege skills (shared)
        yield return new(SkillNumber.Stun,             "Stun",              0, 2, DamageType.None, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.CancelStun,       "Cancel Stun",       0, 0, DamageType.None, SkillType.Other);
        yield return new(SkillNumber.SwellMana,        "Swell Mana",        0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.Invisibility,     "Invisibility",      0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.CancelInvisibility, "Cancel Invisibility", 0, 0, DamageType.None, SkillType.DirectHit);

        // Summoner
        yield return new(SkillNumber.DrainLife,          "Drain Life",         35,  6, DamageType.Wizardry, SkillType.AreaSkillExplicitTarget);
        yield return new(SkillNumber.ChainLightning,     "Chain Lightning",    70,  6, DamageType.Wizardry, SkillType.AreaSkillExplicitTarget);
        yield return new(SkillNumber.DamageReflection,   "Damage Reflection",   0,  5, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Berserker,          "Berserker",           0,  5, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Sleep,              "Sleep",               0,  6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Weakness,           "Weakness",            0,  6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Innovation,         "Innovation",          0,  6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.Explosion223,       "Explosion",          40,  6, DamageType.Curse,    SkillType.DirectHit);
        yield return new(SkillNumber.Requiem,            "Requiem",            65,  6, DamageType.Curse,    SkillType.DirectHit);
        yield return new(SkillNumber.Pollution,          "Pollution",          80,  6, DamageType.Curse,    SkillType.DirectHit);
        yield return new(SkillNumber.LightningShock,     "Lightning Shock",    95,  6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.SpellofProtection,  "Spell of Protection", 0, 0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.SpellofRestriction, "Spell of Restriction", 0, 3, DamageType.None,    SkillType.DirectHit);
        yield return new(SkillNumber.SpellofPursuit,     "Spell of Pursuit",   0,  0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.ShieldBurn,         "Shield-Burn",        0,  3, DamageType.None,     SkillType.DirectHit);

        // Rage Fighter
        yield return new(SkillNumber.KillingBlow,    "Killing Blow",    0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BeastUppercut,  "Beast Uppercut",  0, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.ChainDrive,     "Chain Drive",     0, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DarkSide,       "Dark Side",       0, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DragonRoar,     "Dragon Roar",     0, 3, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.DragonSlasher,  "Dragon Slasher",  0, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.IgnoreDefense,  "Ignore Defense",  0, 3, DamageType.Physical, SkillType.Buff);
        yield return new(SkillNumber.IncreaseHealth, "Increase Health", 0, 7, DamageType.Physical, SkillType.Buff);
        yield return new(SkillNumber.IncreaseBlock,  "Increase Block",  0, 7, DamageType.Physical, SkillType.Buff);
        yield return new(SkillNumber.Charge,         "Charge",         90, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.PhoenixShot,    "Phoenix Shot",    0, 4, DamageType.Physical, SkillType.AreaSkillExplicitTarget);

        // Generic
        yield return new(SkillNumber.MonsterSkill,   "Generic Monster Skill", 0, 5, DamageType.None, SkillType.Other);
        yield return new(SkillNumber.FlameofEvil,    "Flame of Evil (Monster)", 120, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.SummonMonster,       "Summon Monster",           0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.MagicAttackImmunity, "Magic Attack Immunity",    0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.PhysicalAttackImmunity, "Physical Attack Immunity", 0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.PotionofBless,  "Potion of Bless",  0, 0, DamageType.None, SkillType.DirectHit);
        yield return new(SkillNumber.PotionofSoul,   "Potion of Soul",   0, 0, DamageType.None, SkillType.DirectHit);
    }
}
