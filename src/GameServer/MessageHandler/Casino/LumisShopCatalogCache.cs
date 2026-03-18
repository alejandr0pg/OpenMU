// <copyright file="LumisShopCatalogCache.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Casino;

using System.Buffers.Binary;
using System.Threading;
using Npgsql;

/// <summary>
/// Cached catalog of Lumis shop categories and items, queried from PostgreSQL.
/// </summary>
internal static class LumisShopCatalogCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private static byte[]? _cachedPacket;
    private static List<LumisShopItem>? _cachedItems;
    private static DateTime _cacheExpiry = DateTime.MinValue;

    /// <summary>
    /// Gets the cached shop item by its identifier.
    /// </summary>
    public static LumisShopItem? GetItemById(Guid id)
    {
        return _cachedItems?.Find(i => i.Id == id);
    }

    /// <summary>
    /// Gets or builds the catalog response packet (0xFA 0x03).
    /// </summary>
    public static async Task<byte[]?> GetOrBuildPacketAsync()
    {
        if (_cachedPacket is not null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedPacket;
        }

        await CacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cachedPacket is not null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedPacket;
            }

            var connStr = BuildConnectionString();
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync().ConfigureAwait(false);

            await EnsureSeedDataAsync(conn).ConfigureAwait(false);
            var categories = await LoadCategoriesAsync(conn).ConfigureAwait(false);
            var items = await LoadItemsAsync(conn).ConfigureAwait(false);

            _cachedPacket = BuildResponsePacket(categories, items);
            _cachedItems = items;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            return _cachedPacket;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the cached catalog so the next request reloads from DB.
    /// </summary>
    public static void Invalidate()
    {
        _cacheExpiry = DateTime.MinValue;
    }

    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("DB_ADMIN_USER") ?? "postgres";
        var pass = Environment.GetEnvironmentVariable("DB_ADMIN_PW") ?? "admin";
        return $"Server={host};Port=5432;User Id={user};Password={pass};Database=openmu;";
    }

    private static async Task EnsureSeedDataAsync(NpgsqlConnection conn)
    {
        await using var check = new NpgsqlCommand(
            """SELECT count(*) FROM data."LumisShopCategory" """, conn);
        var count = (long)(await check.ExecuteScalarAsync().ConfigureAwait(false))!;
        if (count > 0)
        {
            return;
        }

        await using var seed = new NpgsqlCommand("""
            INSERT INTO data."LumisShopCategory" ("Id", "Name", "DisplayOrder", "IconIndex")
            VALUES
                ('a0000001-0000-0000-0000-000000000001', 'Weapons',     0, 0),
                ('a0000001-0000-0000-0000-000000000002', 'Armor',       1, 1),
                ('a0000001-0000-0000-0000-000000000003', 'Wings',       2, 2),
                ('a0000001-0000-0000-0000-000000000004', 'Consumables', 3, 3),
                ('a0000001-0000-0000-0000-000000000005', 'Misc',        4, 4)
            ON CONFLICT ("Id") DO NOTHING;

            INSERT INTO data."LumisShopItem"
                ("Id", "CategoryId", "ItemGroup", "ItemNumber", "ItemLevel",
                 "LumisPrice", "IsActive", "IsFeatured", "DisplayOrder")
            VALUES
                ('b0000001-0000-0000-0000-000000000001', 'a0000001-0000-0000-0000-000000000001', 0, 19, 0, 500, true, true, 0),
                ('b0000001-0000-0000-0000-000000000002', 'a0000001-0000-0000-0000-000000000001', 0, 21, 0, 800, true, false, 1),
                ('b0000001-0000-0000-0000-000000000003', 'a0000001-0000-0000-0000-000000000001', 5, 7, 0, 600, true, false, 2),
                ('b0000001-0000-0000-0000-000000000004', 'a0000001-0000-0000-0000-000000000002', 7, 15, 0, 400, true, false, 0),
                ('b0000001-0000-0000-0000-000000000005', 'a0000001-0000-0000-0000-000000000002', 8, 15, 0, 400, true, false, 1),
                ('b0000001-0000-0000-0000-000000000006', 'a0000001-0000-0000-0000-000000000002', 9, 15, 0, 400, true, false, 2),
                ('b0000001-0000-0000-0000-000000000007', 'a0000001-0000-0000-0000-000000000002', 10, 15, 0, 400, true, false, 3),
                ('b0000001-0000-0000-0000-000000000008', 'a0000001-0000-0000-0000-000000000002', 11, 15, 0, 400, true, false, 4),
                ('b0000001-0000-0000-0000-000000000009', 'a0000001-0000-0000-0000-000000000003', 12, 0, 0, 2000, true, true, 0),
                ('b0000001-0000-0000-0000-000000000010', 'a0000001-0000-0000-0000-000000000003', 12, 1, 0, 3000, true, false, 1),
                ('b0000001-0000-0000-0000-000000000011', 'a0000001-0000-0000-0000-000000000003', 12, 2, 0, 3000, true, false, 2),
                ('b0000001-0000-0000-0000-000000000012', 'a0000001-0000-0000-0000-000000000004', 14, 0, 0, 100, true, false, 0),
                ('b0000001-0000-0000-0000-000000000013', 'a0000001-0000-0000-0000-000000000004', 14, 1, 0, 100, true, false, 1),
                ('b0000001-0000-0000-0000-000000000014', 'a0000001-0000-0000-0000-000000000004', 14, 3, 0, 150, true, false, 2),
                ('b0000001-0000-0000-0000-000000000015', 'a0000001-0000-0000-0000-000000000005', 14, 13, 0, 200, true, false, 0),
                ('b0000001-0000-0000-0000-000000000016', 'a0000001-0000-0000-0000-000000000005', 14, 14, 0, 5000, true, true, 1)
            ON CONFLICT ("Id") DO NOTHING;
            """, conn);
        await seed.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<List<LumisShopCategory>> LoadCategoriesAsync(NpgsqlConnection conn)
    {
        var list = new List<LumisShopCategory>();
        await using var cmd = new NpgsqlCommand("""
            SELECT "Id", "Name", "DisplayOrder", "IconIndex"
            FROM data."LumisShopCategory" ORDER BY "DisplayOrder"
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new LumisShopCategory(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    private static async Task<List<LumisShopItem>> LoadItemsAsync(NpgsqlConnection conn)
    {
        var list = new List<LumisShopItem>();
        await using var cmd = new NpgsqlCommand("""
            SELECT "Id", "CategoryId", "ItemGroup", "ItemNumber",
                   "ItemLevel", "LumisPrice", "IsFeatured", "DisplayOrder"
            FROM data."LumisShopItem" WHERE "IsActive" = true ORDER BY "DisplayOrder"
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new LumisShopItem(
                reader.GetGuid(0), reader.GetGuid(1),
                reader.GetInt16(2), reader.GetInt16(3), reader.GetInt16(4),
                reader.GetInt64(5), reader.GetBoolean(6), reader.GetInt32(7)));
        }

        return list;
    }

    private static byte[] BuildResponsePacket(
        List<LumisShopCategory> categories,
        List<LumisShopItem> items)
    {
        var catBytes = categories.Sum(c =>
            16 + 4 + 4 + 1 + System.Text.Encoding.UTF8.GetByteCount(c.Name));
        var totalLen = 8 + catBytes + (items.Count * 51);

        var buffer = new byte[totalLen];
        var s = buffer.AsSpan();
        s[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(s[1..], (ushort)totalLen);
        s[3] = 0xFA;
        s[4] = 0x03;
        s[5] = (byte)categories.Count;
        BinaryPrimitives.WriteUInt16LittleEndian(s[6..], (ushort)items.Count);

        var offset = 8;
        foreach (var cat in categories)
        {
            cat.Id.TryWriteBytes(s[offset..]);
            offset += 16;
            BinaryPrimitives.WriteInt32LittleEndian(s[offset..], cat.DisplayOrder);
            offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(s[offset..], cat.IconIndex);
            offset += 4;
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(cat.Name);
            s[offset] = (byte)nameBytes.Length;
            offset++;
            nameBytes.CopyTo(s[offset..]);
            offset += nameBytes.Length;
        }

        foreach (var item in items)
        {
            item.Id.TryWriteBytes(s[offset..]);
            offset += 16;
            item.CategoryId.TryWriteBytes(s[offset..]);
            offset += 16;
            BinaryPrimitives.WriteInt16LittleEndian(s[offset..], item.Group);
            offset += 2;
            BinaryPrimitives.WriteInt16LittleEndian(s[offset..], item.Number);
            offset += 2;
            BinaryPrimitives.WriteInt16LittleEndian(s[offset..], item.Level);
            offset += 2;
            BinaryPrimitives.WriteInt64LittleEndian(s[offset..], item.Price);
            offset += 8;
            s[offset] = item.IsFeatured ? (byte)1 : (byte)0;
            offset++;
            BinaryPrimitives.WriteInt32LittleEndian(s[offset..], item.DisplayOrder);
            offset += 4;
        }

        return buffer;
    }
}

/// <summary>
/// A Lumis shop category loaded from the database.
/// </summary>
internal sealed record LumisShopCategory(Guid Id, string Name, int DisplayOrder, int IconIndex);

/// <summary>
/// A Lumis shop item loaded from the database.
/// </summary>
internal sealed record LumisShopItem(
    Guid Id, Guid CategoryId,
    short Group, short Number, short Level,
    long Price, bool IsFeatured, int DisplayOrder);
