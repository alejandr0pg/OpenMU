// <copyright file="MerchantStoreRaul.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Merchant store for Jeweler Raul (NPC 546) — test shop with all
/// jewels, crafting materials, quest items, pets, seeds, spheres
/// and event tickets at base prices.
/// </summary>
internal partial class NpcInitialization
{
    private ItemStorage CreateJewelerRaulStore(short number)
    {
        List<Item> itemList = new ()
        {
            // ── Row 0 (slots 0-7): Core Jewels — all 1×1 ──
            this.ItemHelper.CreateItem(0, 15, 12, 1, 0),   // Jewel of Chaos
            this.ItemHelper.CreateItem(1, 13, 14, 1, 0),   // Jewel of Bless
            this.ItemHelper.CreateItem(2, 14, 14, 1, 0),   // Jewel of Soul
            this.ItemHelper.CreateItem(3, 16, 14, 1, 0),   // Jewel of Life
            this.ItemHelper.CreateItem(4, 22, 14, 1, 0),   // Jewel of Creation
            this.ItemHelper.CreateItem(5, 31, 14, 1, 0),   // Jewel of Guardian
            this.ItemHelper.CreateItem(6, 41, 14, 1, 0),   // Gemstone
            this.ItemHelper.CreateItem(7, 42, 14, 1, 0),   // Jewel of Harmony

            // ── Row 1 (slots 8-15): Stones & Fenrir materials — all 1×1 ──
            this.ItemHelper.CreateItem(8, 43, 14, 1, 0),   // Lower Refine Stone
            this.ItemHelper.CreateItem(9, 44, 14, 1, 0),   // Higher Refine Stone
            this.ItemHelper.CreateItem(10, 10, 14, 1, 0),  // Town Portal Scroll
            this.ItemHelper.CreateItem(11, 29, 13, 1, 0),  // Armor of Guardsman
            this.ItemHelper.CreateItem(12, 32, 13, 1, 0),  // Splinter of Armor
            this.ItemHelper.CreateItem(13, 33, 13, 1, 0),  // Bless of Guardian
            this.ItemHelper.CreateItem(14, 34, 13, 1, 0),  // Claw of Beast
            this.ItemHelper.CreateItem(15, 35, 13, 1, 0),  // Fragment of Horn

            // ── Rows 2-3 (slots 16-23): Wing & quest 1×2 items ──
            // Each occupies 1 col × 2 rows (row below is reserved).
            this.ItemHelper.CreateItem(16, 14, 13, 1, 0),  // Loch's Feather         1×2
            this.ItemHelper.CreateItem(17, 14, 13, 1, 1),  // Loch's Feather +1 (Crest) 1×2
            this.ItemHelper.CreateItem(18, 52, 13, 1, 0),  // Flame of Condor        1×2
            this.ItemHelper.CreateItem(19, 53, 13, 1, 0),  // Feather of Condor      1×2
            this.ItemHelper.CreateItem(20, 24, 14, 1, 0),  // Broken Sword           1×2
            this.ItemHelper.CreateItem(21, 68, 14, 1, 0),  // Eye of Abyssal         1×2
            this.ItemHelper.CreateItem(22, 66, 14, 1, 0),  // Horn of Hell Maine     1×2
            this.ItemHelper.CreateItem(23, 67, 14, 1, 0),  // Feather of Dark Phoenix 1×2

            // ── Row 4 (slots 32-39): Quest items 1×1, spirits, pets ──
            this.ItemHelper.CreateItem(32, 23, 14, 1, 0),  // Scroll of Emperor
            this.ItemHelper.CreateItem(33, 25, 14, 1, 0),  // Tear of Elf
            this.ItemHelper.CreateItem(34, 26, 14, 1, 0),  // Soul Shard of Wizard
            this.ItemHelper.CreateItem(35, 65, 14, 1, 0),  // Flame of Death Beam Knight
            this.ItemHelper.CreateItem(36, 31, 13, 1, 0),  // Spirit (Dark Horse)
            this.ItemHelper.CreateItem(37, 31, 13, 1, 1),  // Spirit +1 (Dark Raven)
            this.ItemHelper.CreateItem(38, 0, 13, 1, 0),   // Guardian Angel
            this.ItemHelper.CreateItem(39, 1, 13, 1, 0),   // Imp

            // ── Rows 5-6 (slots 40-55): 2×2 items + pets ──
            // Broken Horn 2×2 occupies slots 40,41,48,49
            this.ItemHelper.CreateItem(40, 36, 13, 1, 0),  // Broken Horn            2×2
            this.ItemHelper.CreateItem(42, 2, 13, 1, 0),   // Horn of Uniria         1×1
            this.ItemHelper.CreateItem(43, 3, 13, 1, 0),   // Horn of Dinorant       1×1
            // Horn of Fenrir 2×2 occupies slots 44,45,52,53
            this.ItemHelper.CreateItem(44, 37, 13, 1, 0),  // Horn of Fenrir         2×2
            this.ItemHelper.CreateItem(46, 64, 13, 1, 0),  // Demon                  1×1
            this.ItemHelper.CreateItem(47, 65, 13, 1, 0),  // Spirit of Guardian      1×1
            this.ItemHelper.CreateItem(50, 67, 13, 1, 0),  // Pet Rudolf             1×1
            this.ItemHelper.CreateItem(51, 80, 13, 1, 0),  // Pet Panda              1×1
            this.ItemHelper.CreateItem(54, 106, 13, 1, 0), // Pet Unicorn            1×1
            this.ItemHelper.CreateItem(55, 123, 13, 1, 0), // Pet Skeleton           1×1

            // ── Row 7 (slots 56-63): Seeds — all 1×1 ──
            this.ItemHelper.CreateItem(56, 60, 12, 1, 0),  // Seed (Fire)
            this.ItemHelper.CreateItem(57, 61, 12, 1, 0),  // Seed (Water)
            this.ItemHelper.CreateItem(58, 62, 12, 1, 0),  // Seed (Ice)
            this.ItemHelper.CreateItem(59, 63, 12, 1, 0),  // Seed (Wind)
            this.ItemHelper.CreateItem(60, 64, 12, 1, 0),  // Seed (Lightning)
            this.ItemHelper.CreateItem(61, 65, 12, 1, 0),  // Seed (Earth)

            // ── Row 8 (slots 64-71): Spheres + Devil's Eye — all 1×1 ──
            this.ItemHelper.CreateItem(64, 70, 12, 1, 0),  // Sphere (Mono)
            this.ItemHelper.CreateItem(65, 71, 12, 1, 0),  // Sphere (Di)
            this.ItemHelper.CreateItem(66, 72, 12, 1, 0),  // Sphere (Tri)
            this.ItemHelper.CreateItem(67, 73, 12, 1, 0),  // Sphere (Tetra)
            this.ItemHelper.CreateItem(68, 74, 12, 1, 0),  // Sphere (Penta)
            this.ItemHelper.CreateItem(69, 17, 14, 1, 1),  // Devil's Eye +1
            this.ItemHelper.CreateItem(70, 17, 14, 1, 2),  // Devil's Eye +2
            this.ItemHelper.CreateItem(71, 17, 14, 1, 3),  // Devil's Eye +3

            // ── Row 9 (slots 72-79): Devil's Eye + Key — all 1×1 ──
            this.ItemHelper.CreateItem(72, 17, 14, 1, 4),  // Devil's Eye +4
            this.ItemHelper.CreateItem(73, 17, 14, 1, 5),  // Devil's Eye +5
            this.ItemHelper.CreateItem(74, 17, 14, 1, 6),  // Devil's Eye +6
            this.ItemHelper.CreateItem(75, 17, 14, 1, 7),  // Devil's Eye +7
            this.ItemHelper.CreateItem(76, 18, 14, 1, 1),  // Devil's Key +1
            this.ItemHelper.CreateItem(77, 18, 14, 1, 2),  // Devil's Key +2
            this.ItemHelper.CreateItem(78, 18, 14, 1, 3),  // Devil's Key +3
            this.ItemHelper.CreateItem(79, 18, 14, 1, 4),  // Devil's Key +4

            // ── Row 10 (slots 80-87): Devil's Key + Old Scroll — all 1×1 ──
            this.ItemHelper.CreateItem(80, 18, 14, 1, 5),  // Devil's Key +5
            this.ItemHelper.CreateItem(81, 18, 14, 1, 6),  // Devil's Key +6
            this.ItemHelper.CreateItem(82, 18, 14, 1, 7),  // Devil's Key +7
            this.ItemHelper.CreateItem(83, 49, 13, 1, 1),  // Old Scroll +1
            this.ItemHelper.CreateItem(84, 49, 13, 1, 2),  // Old Scroll +2
            this.ItemHelper.CreateItem(85, 49, 13, 1, 3),  // Old Scroll +3
            this.ItemHelper.CreateItem(86, 49, 13, 1, 4),  // Old Scroll +4
            this.ItemHelper.CreateItem(87, 49, 13, 1, 5),  // Old Scroll +5

            // ── Rows 11-12 (slots 88-95): Blood Bone + Archangel 1×2 ──
            this.ItemHelper.CreateItem(88, 17, 13, 1, 1),  // Blood Bone +1          1×2
            this.ItemHelper.CreateItem(89, 17, 13, 1, 2),  // Blood Bone +2          1×2
            this.ItemHelper.CreateItem(90, 17, 13, 1, 3),  // Blood Bone +3          1×2
            this.ItemHelper.CreateItem(91, 17, 13, 1, 4),  // Blood Bone +4          1×2
            this.ItemHelper.CreateItem(92, 16, 13, 1, 1),  // Scroll of Archangel +1 1×2
            this.ItemHelper.CreateItem(93, 16, 13, 1, 2),  // Scroll of Archangel +2 1×2
            this.ItemHelper.CreateItem(94, 16, 13, 1, 3),  // Scroll of Archangel +3 1×2
            this.ItemHelper.CreateItem(95, 16, 13, 1, 4),  // Scroll of Archangel +4 1×2

            // ── Rows 13-14 (slots 104-111): More event 1×2 items ──
            this.ItemHelper.CreateItem(104, 17, 13, 1, 5), // Blood Bone +5          1×2
            this.ItemHelper.CreateItem(105, 17, 13, 1, 6), // Blood Bone +6          1×2
            this.ItemHelper.CreateItem(106, 16, 13, 1, 5), // Scroll of Archangel +5 1×2
            this.ItemHelper.CreateItem(107, 16, 13, 1, 6), // Scroll of Archangel +6 1×2
            this.ItemHelper.CreateItem(108, 50, 13, 1, 1), // Illusion Covenant +1   1×2
            this.ItemHelper.CreateItem(109, 50, 13, 1, 2), // Illusion Covenant +2   1×2
            this.ItemHelper.CreateItem(110, 50, 13, 1, 3), // Illusion Covenant +3   1×2
            this.ItemHelper.CreateItem(111, 50, 13, 1, 4), // Illusion Covenant +4   1×2
        };

        var storage = this.CreateMerchantStore(itemList);
        storage.SetGuid(number);
        return storage;
    }
}
