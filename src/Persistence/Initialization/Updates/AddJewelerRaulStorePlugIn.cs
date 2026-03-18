// <copyright file="AddJewelerRaulStorePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Assigns a merchant store to Jeweler Raul (NPC 546) with jewels,
/// crafting materials, quest items, pets, seeds, spheres, and event tickets.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D")]
public class AddJewelerRaulStorePlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Add Jeweler Raul Store";
    internal const string PlugInDescription =
        "Assigns a full test merchant store to Jeweler Raul (NPC 546).";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddJewelerRaulStore;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(
        IContext context,
        GameConfiguration gameConfiguration)
    {
        await ValueTask.CompletedTask;

        var raul = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == 546);
        if (raul is null || raul.MerchantStore is not null)
        {
            return;
        }

        var helper = new ItemHelper(context, gameConfiguration);
        var store = BuildStore(context, helper);
        store.SetGuid((short)raul.Number);
        raul.MerchantStore = store;
    }

    private static ItemStorage BuildStore(IContext ctx, ItemHelper h)
    {
        var storage = ctx.CreateNew<ItemStorage>();
        AddItems(storage, h);
        return storage;
    }

    private static void AddItems(ItemStorage s, ItemHelper h)
    {
        // Row 0: Core jewels (1×1)
        Add(s, h, 0, 15, 12, 1, 0);   // Jewel of Chaos
        Add(s, h, 1, 13, 14, 1, 0);   // Jewel of Bless
        Add(s, h, 2, 14, 14, 1, 0);   // Jewel of Soul
        Add(s, h, 3, 16, 14, 1, 0);   // Jewel of Life
        Add(s, h, 4, 22, 14, 1, 0);   // Jewel of Creation
        Add(s, h, 5, 31, 14, 1, 0);   // Jewel of Guardian
        Add(s, h, 6, 41, 14, 1, 0);   // Gemstone
        Add(s, h, 7, 42, 14, 1, 0);   // Jewel of Harmony

        // Row 1: Stones & Fenrir materials (1×1)
        Add(s, h, 8, 43, 14, 1, 0);   // Lower Refine Stone
        Add(s, h, 9, 44, 14, 1, 0);   // Higher Refine Stone
        Add(s, h, 10, 10, 14, 1, 0);  // Town Portal Scroll
        Add(s, h, 11, 29, 13, 1, 0);  // Armor of Guardsman
        Add(s, h, 12, 32, 13, 1, 0);  // Splinter of Armor
        Add(s, h, 13, 33, 13, 1, 0);  // Bless of Guardian
        Add(s, h, 14, 34, 13, 1, 0);  // Claw of Beast
        Add(s, h, 15, 35, 13, 1, 0);  // Fragment of Horn

        // Rows 2-3: Wing & quest items (1×2)
        Add(s, h, 16, 14, 13, 1, 0);  // Loch's Feather
        Add(s, h, 17, 14, 13, 1, 1);  // Monarch's Crest (Loch+1)
        Add(s, h, 18, 52, 13, 1, 0);  // Flame of Condor
        Add(s, h, 19, 53, 13, 1, 0);  // Feather of Condor
        Add(s, h, 20, 24, 14, 1, 0);  // Broken Sword
        Add(s, h, 21, 68, 14, 1, 0);  // Eye of Abyssal
        Add(s, h, 22, 66, 14, 1, 0);  // Horn of Hell Maine
        Add(s, h, 23, 67, 14, 1, 0);  // Feather of Dark Phoenix

        // Row 4: Quest items + spirits + pets (1×1)
        Add(s, h, 32, 23, 14, 1, 0);  // Scroll of Emperor
        Add(s, h, 33, 25, 14, 1, 0);  // Tear of Elf
        Add(s, h, 34, 26, 14, 1, 0);  // Soul Shard of Wizard
        Add(s, h, 35, 65, 14, 1, 0);  // Flame of Death Beam Knight
        Add(s, h, 36, 31, 13, 1, 0);  // Spirit (Dark Horse)
        Add(s, h, 37, 31, 13, 1, 1);  // Spirit +1 (Dark Raven)
        Add(s, h, 38, 0, 13, 1, 0);   // Guardian Angel
        Add(s, h, 39, 1, 13, 1, 0);   // Imp

        // Rows 5-6: 2×2 items + pets (1×1)
        Add(s, h, 40, 36, 13, 1, 0);  // Broken Horn (2×2)
        Add(s, h, 42, 2, 13, 1, 0);   // Horn of Uniria
        Add(s, h, 43, 3, 13, 1, 0);   // Horn of Dinorant
        Add(s, h, 44, 37, 13, 1, 0);  // Horn of Fenrir (2×2)
        Add(s, h, 46, 64, 13, 1, 0);  // Demon
        Add(s, h, 47, 65, 13, 1, 0);  // Spirit of Guardian
        Add(s, h, 50, 67, 13, 1, 0);  // Pet Rudolf
        Add(s, h, 51, 80, 13, 1, 0);  // Pet Panda
        Add(s, h, 54, 106, 13, 1, 0); // Pet Unicorn
        Add(s, h, 55, 123, 13, 1, 0); // Pet Skeleton

        // Row 7: Seeds (1×1)
        Add(s, h, 56, 60, 12, 1, 0);  // Seed (Fire)
        Add(s, h, 57, 61, 12, 1, 0);  // Seed (Water)
        Add(s, h, 58, 62, 12, 1, 0);  // Seed (Ice)
        Add(s, h, 59, 63, 12, 1, 0);  // Seed (Wind)
        Add(s, h, 60, 64, 12, 1, 0);  // Seed (Lightning)
        Add(s, h, 61, 65, 12, 1, 0);  // Seed (Earth)

        // Row 8: Spheres + Devil's Eye (1×1)
        Add(s, h, 64, 70, 12, 1, 0);  // Sphere (Mono)
        Add(s, h, 65, 71, 12, 1, 0);  // Sphere (Di)
        Add(s, h, 66, 72, 12, 1, 0);  // Sphere (Tri)
        Add(s, h, 67, 73, 12, 1, 0);  // Sphere (Tetra)
        Add(s, h, 68, 74, 12, 1, 0);  // Sphere (Penta)
        Add(s, h, 69, 17, 14, 1, 1);  // Devil's Eye +1
        Add(s, h, 70, 17, 14, 1, 2);  // Devil's Eye +2
        Add(s, h, 71, 17, 14, 1, 3);  // Devil's Eye +3

        // Row 9: Devil's Eye + Key (1×1)
        Add(s, h, 72, 17, 14, 1, 4);  // Devil's Eye +4
        Add(s, h, 73, 17, 14, 1, 5);  // Devil's Eye +5
        Add(s, h, 74, 17, 14, 1, 6);  // Devil's Eye +6
        Add(s, h, 75, 17, 14, 1, 7);  // Devil's Eye +7
        Add(s, h, 76, 18, 14, 1, 1);  // Devil's Key +1
        Add(s, h, 77, 18, 14, 1, 2);  // Devil's Key +2
        Add(s, h, 78, 18, 14, 1, 3);  // Devil's Key +3
        Add(s, h, 79, 18, 14, 1, 4);  // Devil's Key +4

        // Row 10: Devil's Key + Old Scroll (1×1)
        Add(s, h, 80, 18, 14, 1, 5);  // Devil's Key +5
        Add(s, h, 81, 18, 14, 1, 6);  // Devil's Key +6
        Add(s, h, 82, 18, 14, 1, 7);  // Devil's Key +7
        Add(s, h, 83, 49, 13, 1, 1);  // Old Scroll +1
        Add(s, h, 84, 49, 13, 1, 2);  // Old Scroll +2
        Add(s, h, 85, 49, 13, 1, 3);  // Old Scroll +3
        Add(s, h, 86, 49, 13, 1, 4);  // Old Scroll +4
        Add(s, h, 87, 49, 13, 1, 5);  // Old Scroll +5

        // Rows 11-12: Blood Bone + Archangel (1×2)
        Add(s, h, 88, 17, 13, 1, 1);  // Blood Bone +1
        Add(s, h, 89, 17, 13, 1, 2);  // Blood Bone +2
        Add(s, h, 90, 17, 13, 1, 3);  // Blood Bone +3
        Add(s, h, 91, 17, 13, 1, 4);  // Blood Bone +4
        Add(s, h, 92, 16, 13, 1, 1);  // Scroll of Archangel +1
        Add(s, h, 93, 16, 13, 1, 2);  // Scroll of Archangel +2
        Add(s, h, 94, 16, 13, 1, 3);  // Scroll of Archangel +3
        Add(s, h, 95, 16, 13, 1, 4);  // Scroll of Archangel +4

        // Rows 13-14: More event items (1×2)
        Add(s, h, 104, 17, 13, 1, 5); // Blood Bone +5
        Add(s, h, 105, 17, 13, 1, 6); // Blood Bone +6
        Add(s, h, 106, 16, 13, 1, 5); // Scroll of Archangel +5
        Add(s, h, 107, 16, 13, 1, 6); // Scroll of Archangel +6
        Add(s, h, 108, 50, 13, 1, 1); // Illusion Covenant +1
        Add(s, h, 109, 50, 13, 1, 2); // Illusion Covenant +2
        Add(s, h, 110, 50, 13, 1, 3); // Illusion Covenant +3
        Add(s, h, 111, 50, 13, 1, 4); // Illusion Covenant +4
    }

    private static void Add(
        ItemStorage storage,
        ItemHelper helper,
        byte slot,
        byte itemId,
        byte group,
        byte stack,
        byte level)
    {
        storage.Items.Add(helper.CreateItem(slot, itemId, group, stack, level));
    }
}
