// <copyright file="FixSkillDefinitionsPlugIn.MasterSkills.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;

public partial class FixSkillDefinitionsPlugIn
{
    private static IEnumerable<SkillDef> GetMasterSkillDefs()
    {
        // Common master skills
        yield return new(SkillNumber.DurabilityReduction1,   "Durability Reduction (1)",  17, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.PvPDefenceRateInc,      "PvP Defence Rate Inc",      12, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MaximumSDincrease,      "Maximum SD increase",       13, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.AutomaticManaRecInc,    "Automatic Mana Rec Inc",     7, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.PoisonResistanceInc,    "Poison Resistance Inc",      1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.DurabilityReduction2,   "Durability Reduction (2)",  17, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.SdRecoverySpeedInc,     "SD Recovery Speed Inc",      1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.AutomaticHpRecInc,      "Automatic HP Rec Inc",       1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.LightningResistanceInc, "Lightning Resistance Inc",   1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.DefenseIncrease,        "Defense Increase",          16, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.AutomaticAgRecInc,      "Automatic AG Rec Inc",       1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.IceResistanceIncrease,  "Ice Resistance Increase",    1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.DurabilityReduction3,   "Durability Reduction (3)",  17, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.DefenseSuccessRateInc,  "Defense Success Rate Inc",   1, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MaximumLifeIncrease,    "Maximum Life Increase",      9, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.ManaReduction,          "Mana Reduction",            18, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MonsterAttackSdInc,     "Monster Attack SD Inc",     11, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MonsterAttackLifeInc,   "Monster Attack Life Inc",    6, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MinimumAttackPowerInc,  "Minimum Attack Power Inc",  22, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.MonsterAttackManaInc,   "Monster Attack Mana Inc",    6, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.PvPAttackRate,          "PvP Attack Rate",           14, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.AttackSuccRateInc,      "Attack Succ Rate Inc",      13, 0, DamageType.None, SkillType.PassiveBoost);
        yield return new(SkillNumber.MaximumManaIncrease,    "Maximum Mana Increase",      9, 0, DamageType.None, SkillType.PassiveBoost);

        // Blade Master
        yield return new(SkillNumber.SwellLifeProficiency,     "Swell Life Proficiency",    7, 0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.CycloneStrengthener,      "Cyclone Strengthener",      22, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.SlashStrengthener,        "Slash Strengthener",         3, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.FallingSlashStreng,       "Falling Slash Streng",       3, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.LungeStrengthener,        "Lunge Strengthener",         3, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TwistingSlashStreng,      "Twisting Slash Streng",      3, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.RagefulBlowStreng,        "Rageful Blow Streng",       22, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TwistingSlashMastery,     "Twisting Slash Mastery",     1, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.RagefulBlowMastery,       "Rageful Blow Mastery",       1, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.WeaponMasteryBladeMaster, "Weapon Mastery",            22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DeathStabStrengthener,    "Death Stab Strengthener",   22, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.StrikeofDestrStr,         "Strike of Destr Str",       22, 5, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TwoHandedSwordStrengthener, "Two-handed Sword Stren",   4, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.OneHandedSwordStrengthener, "One-handed Sword Stren",  22, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.MaceStrengthener,         "Mace Strengthener",          3, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.SpearStrengthener,        "Spear Strengthener",         3, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.TwoHandedSwordMaster,     "Two-handed Sword Mast",      5, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.OneHandedSwordMaster,     "One-handed Sword Mast",     23, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.MaceMastery,              "Mace Mastery",               1, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.SpearMastery,             "Spear Mastery",              1, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.SwellLifeStrengt,         "Swell Life Strengt",         7, 0, DamageType.None,     SkillType.DirectHit);

        // Grand Master
        yield return new(SkillNumber.FlameStrengthener,         "Flame Strengthener",         3, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.LightningStrengthener,     "Lightning Strengthener",     3, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.ExpansionofWizStreng,      "Expansion of Wiz Streng",    1, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.InfernoStrengthener,       "Inferno Strengthener",      22, 0, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.BlastStrengthener,         "Blast Strengthener",        22, 3, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.ExpansionofWizMas,         "Expansion of Wiz Mas",       1, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.PoisonStrengthener,        "Poison Strengthener",        3, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.EvilSpiritStreng,          "Evil Spirit Streng",        22, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.MagicMasteryGrandMaster,   "Magic Mastery",             22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DecayStrengthener,         "Decay Strengthener",        22, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.HellfireStrengthener,      "Hellfire Strengthener",      3, 0, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.IceStrengthener,           "Ice Strengthener",           3, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.OneHandedStaffStrengthener, "One-handed Staff Stren",   22, 0, DamageType.Wizardry, SkillType.PassiveBoost);
        yield return new(SkillNumber.TwoHandedStaffStrengthener, "Two-handed Staff Stren",    4, 0, DamageType.Wizardry, SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldStrengthenerGrandMaster, "Shield Strengthener",   10, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.OneHandedStaffMaster,      "One-handed Staff Mast",     23, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.TwoHandedStaffMaster,      "Two-handed Staff Mast",      5, 0, DamageType.Wizardry, SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldMasteryGrandMaster,  "Shield Mastery",            17, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.SoulBarrierStrength,       "Soul Barrier Strength",      7, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.SoulBarrierProficie,       "Soul Barrier Proficie",     10, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.MinimumWizardryInc,        "Minimum Wizardry Inc",      22, 0, DamageType.None,     SkillType.PassiveBoost);

        // High Elf
        yield return new(SkillNumber.HealStrengthener,          "Heal Strengthener",         22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TripleShotStrengthener,    "Triple Shot Strengthener",  22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.SummonedMonsterStr1,       "Summoned Monster Str (1)",  16, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.PenetrationStrengthener,   "Penetration Strengthener",  22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DefenseIncreaseStr,        "Defense Increase Str",      22, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.TripleShotMastery,         "Triple Shot Mastery",        0, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.SummonedMonsterStr2,       "Summoned Monster Str (2)",  16, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.AttackIncreaseStr,         "Attack Increase Str",       22, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.WeaponMasteryHighElf,      "Weapon Mastery",            22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.AttackIncreaseMastery,     "Attack Increase Mastery",   22, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.DefenseIncreaseMastery,    "Defense Increase Mastery",  22, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.IceArrowStrengthener,      "Ice Arrow Strengthener",    22, 8, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BowStrengthener,           "Bow Strengthener",          22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.CrossbowStrengthener,      "Crossbow Strengthener",      3, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldStrengthenerHighElf, "Shield Strengthener",       10, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.BowMastery,                "Bow Mastery",               23, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.CrossbowMastery,           "Crossbow Mastery",           5, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldMasteryHighElf,      "Shield Mastery",            15, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.InfinityArrowStr,          "Infinity Arrow Str",         1, 6, DamageType.None,     SkillType.Buff);
        yield return new(SkillNumber.MinimumAttPowerInc,        "Minimum Att Power Inc",     22, 0, DamageType.Physical, SkillType.PassiveBoost);

        // Dimension Master (Summoner)
        yield return new(SkillNumber.FireTomeStrengthener,  "Fire Tome Strengthener",    3, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.WindTomeStrengthener,  "Wind Tome Strengthener",    3, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.LightningTomeStren,    "Lightning Tome Stren",      3, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.FireTomeMastery,       "Fire Tome Mastery",         7, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.WindTomeMastery,       "Wind Tome Mastery",         1, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.LightningTomeMastery,  "Lightning Tome Mastery",    7, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.SleepStrengthener,     "Sleep Strengthener",        1, 6, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.ChainLightningStr,     "Chain Lightning Str",      22, 6, DamageType.Wizardry, SkillType.AreaSkillExplicitTarget);
        yield return new(SkillNumber.LightningShockStr,     "Lightning Shock Str",      22, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.MagicMasterySummoner,  "Magic Mastery",            22, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.DrainLifeStrengthener, "Drain Life Strengthener",  22, 6, DamageType.Wizardry, SkillType.DirectHit);
        yield return new(SkillNumber.StickStrengthener,     "Stick Strengthener",       22, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.OtherWorldTomeStreng,  "Other World Tome Streng",   3, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.StickMastery,          "Stick Mastery",             5, 0, DamageType.Curse,    SkillType.PassiveBoost);
        yield return new(SkillNumber.OtherWorldTomeMastery, "Other World Tome Mastery", 23, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.BerserkerStrengthener, "Berserker Strengthener",    7, 5, DamageType.Curse,    SkillType.DirectHit);
        yield return new(SkillNumber.BerserkerProficiency,  "Berserker Proficiency",     7, 5, DamageType.Curse,    SkillType.DirectHit);
        yield return new(SkillNumber.MinimumWizCurseInc,    "Minimum Wiz/Curse Inc",    22, 0, DamageType.None,     SkillType.PassiveBoost);

        // Duel Master
        yield return new(SkillNumber.CycloneStrengthenerDuelMaster,        "Cyclone Strengthener",    22, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.LightningStrengthenerDuelMaster,      "Lightning Strengthener",   3, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.TwistingSlashStrengthenerDuelMaster,  "Twisting Slash Stren",     3, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.PowerSlashStreng,                     "Power Slash Streng",       3, 5, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.FlameStrengthenerDuelMaster,          "Flame Strengthener",       3, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BlastStrengthenerDuelMaster,          "Blast Strengthener",      22, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.WeaponMasteryDuelMaster,              "Weapon Mastery",          22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.InfernoStrengthenerDuelMaster,        "Inferno Strengthener",    22, 0, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.EvilSpiritStrengthenerDuelMaster,     "Evil Spirit Strengthen",  22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.MagicMasteryDuelMaster,               "Magic Mastery",           22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.IceStrengthenerDuelMaster,            "Ice Strengthener",         3, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BloodAttackStrengthen,                "Blood Attack Strengthen", 22, 3, DamageType.None,     SkillType.DirectHit);

        // Lord Emperor
        yield return new(SkillNumber.FireBurstStreng,               "Fire Burst Streng",         22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.ForceWaveStreng,               "Force Wave Streng",          3, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DarkHorseStreng1,              "Dark Horse Streng (1)",     17, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.CriticalDmgIncPowUp,          "Critical DMG Inc PowUp",     3, 0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.EarthshakeStreng,              "Earthshake Streng",         22,10, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.WeaponMasteryLordEmperor,      "Weapon Mastery",            22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.FireBurstMastery,              "Fire Burst Mastery",         1, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.CritDmgIncPowUp2,             "Crit DMG Inc PowUp (2)",    10, 0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.EarthshakeMastery,             "Earthshake Mastery",         1,10, DamageType.Physical, SkillType.AreaSkillAutomaticHits);
        yield return new(SkillNumber.CritDmgIncPowUp3,             "Crit DMG Inc PowUp (3)",     7, 0, DamageType.None,     SkillType.DirectHit);
        yield return new(SkillNumber.FireScreamStren,               "Fire Scream Stren",         22, 6, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DarkSpiritStr,                 "Dark Spirit Str",            3, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ScepterStrengthener,           "Scepter Strengthener",      22, 0, DamageType.Physical, SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldStrengthenerLordEmperor, "Shield Strengthener",       10, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.UseScepterPetStr,              "Use Scepter : Pet Str",      3, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DarkSpiritStr2,                "Dark Spirit Str (2)",        7, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ScepterMastery,                "Scepter Mastery",            5, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ShieldMastery,                 "Shield Mastery",            17, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.CommandAttackInc,              "Command Attack Inc",        20, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DarkSpiritStr3,                "Dark Spirit Str (3)",        1, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.PetDurabilityStr,              "Pet Durability Str",        17, 0, DamageType.None,     SkillType.PassiveBoost);

        // Fist Master (Rage Fighter)
        yield return new(SkillNumber.KillingBlowStrengthener,        "Killing Blow Strengthener",    22, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BeastUppercutStrengthener,      "Beast Uppercut Strengthener",  22, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.KillingBlowMastery,             "Killing Blow Mastery",          1, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.BeastUppercutMastery,           "Beast Uppercut Mastery",        1, 2, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.WeaponMasteryFistMaster,        "Weapon Mastery",               22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.ChainDriveStrengthener,         "Chain Drive Strengthener",     22, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DarkSideStrengthener,           "Dark Side Strengthener",       22, 4, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DragonRoarStrengthener,         "Dragon Roar Strengthener",     22, 3, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.EquippedWeaponStrengthener,     "Equipped Weapon Strengthener", 22, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DefSuccessRateIncPowUp,         "Def SuccessRate IncPowUp",     22, 7, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.EquippedWeaponMastery,          "Equipped Weapon Mastery",       1, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.DefSuccessRateIncMastery,       "DefSuccessRate IncMastery",    22, 7, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.StaminaIncreaseStrengthener,    "Stamina Increase Strengthener", 5, 7, DamageType.Physical, SkillType.DirectHit);
        yield return new(SkillNumber.DurabilityReduction1FistMaster, "Durability Reduction (1)",     17, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.IncreasePvPDefenseRate,         "Increase PvP Defense Rate",    29, 0, DamageType.None,     SkillType.PassiveBoost);
        yield return new(SkillNumber.IncreaseMaximumSd,              "Increase Maximum SD",          33, 0, DamageType.None,     SkillType.PassiveBoost);
    }
}
