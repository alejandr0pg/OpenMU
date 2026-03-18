// <copyright file="AddLumisShopDataPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Seeds the Lumis shop with initial categories and items.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E3F4A5B6-C7D8-4E9F-A0B1-2C3D4E5F6A7B")]
public class AddLumisShopDataPlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Add Lumis Shop Data";
    internal const string PlugInDescription = "Seeds the Lumis shop tables with initial categories and items.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddLumisShopData;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var connStr = BuildConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync().ConfigureAwait(false);

        await SeedCategoriesAsync(conn).ConfigureAwait(false);
        await SeedItemsAsync(conn).ConfigureAwait(false);
    }

    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("DB_ADMIN_USER") ?? "postgres";
        var pass = Environment.GetEnvironmentVariable("DB_ADMIN_PW") ?? "admin";
        return $"Server={host};Port=5432;User Id={user};Password={pass};Database=openmu;";
    }

    private static async Task SeedCategoriesAsync(NpgsqlConnection conn)
    {
        var sql = """
            INSERT INTO data."LumisShopCategory" ("Id", "Name", "DisplayOrder", "IconIndex")
            VALUES
                ('A0000001-0000-0000-0000-000000000001', 'Weapons',     0, 0),
                ('A0000001-0000-0000-0000-000000000002', 'Armor',       1, 1),
                ('A0000001-0000-0000-0000-000000000003', 'Wings',       2, 2),
                ('A0000001-0000-0000-0000-000000000004', 'Consumables', 3, 3),
                ('A0000001-0000-0000-0000-000000000005', 'Misc',        4, 4)
            ON CONFLICT ("Id") DO NOTHING;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task SeedItemsAsync(NpgsqlConnection conn)
    {
        var sql = """
            INSERT INTO data."LumisShopItem"
                ("Id", "CategoryId", "ItemGroup", "ItemNumber", "ItemLevel",
                 "LumisPrice", "IsActive", "IsFeatured", "DisplayOrder")
            VALUES
                ('B0000001-0000-0000-0000-000000000001', 'A0000001-0000-0000-0000-000000000001',
                 0, 19, 0, 500, true, true, 0),
                ('B0000001-0000-0000-0000-000000000002', 'A0000001-0000-0000-0000-000000000001',
                 0, 21, 0, 800, true, false, 1),
                ('B0000001-0000-0000-0000-000000000003', 'A0000001-0000-0000-0000-000000000001',
                 5, 7, 0, 600, true, false, 2),
                ('B0000001-0000-0000-0000-000000000004', 'A0000001-0000-0000-0000-000000000002',
                 7, 15, 0, 400, true, false, 0),
                ('B0000001-0000-0000-0000-000000000005', 'A0000001-0000-0000-0000-000000000002',
                 8, 15, 0, 400, true, false, 1),
                ('B0000001-0000-0000-0000-000000000006', 'A0000001-0000-0000-0000-000000000002',
                 9, 15, 0, 400, true, false, 2),
                ('B0000001-0000-0000-0000-000000000007', 'A0000001-0000-0000-0000-000000000002',
                 10, 15, 0, 400, true, false, 3),
                ('B0000001-0000-0000-0000-000000000008', 'A0000001-0000-0000-0000-000000000002',
                 11, 15, 0, 400, true, false, 4),
                ('B0000001-0000-0000-0000-000000000009', 'A0000001-0000-0000-0000-000000000003',
                 12, 0, 0, 2000, true, true, 0),
                ('B0000001-0000-0000-0000-000000000010', 'A0000001-0000-0000-0000-000000000003',
                 12, 1, 0, 3000, true, false, 1),
                ('B0000001-0000-0000-0000-000000000011', 'A0000001-0000-0000-0000-000000000003',
                 12, 2, 0, 3000, true, false, 2),
                ('B0000001-0000-0000-0000-000000000012', 'A0000001-0000-0000-0000-000000000004',
                 14, 0, 0, 100, true, false, 0),
                ('B0000001-0000-0000-0000-000000000013', 'A0000001-0000-0000-0000-000000000004',
                 14, 1, 0, 100, true, false, 1),
                ('B0000001-0000-0000-0000-000000000014', 'A0000001-0000-0000-0000-000000000004',
                 14, 3, 0, 150, true, false, 2),
                ('B0000001-0000-0000-0000-000000000015', 'A0000001-0000-0000-0000-000000000005',
                 14, 13, 0, 200, true, false, 0),
                ('B0000001-0000-0000-0000-000000000016', 'A0000001-0000-0000-0000-000000000005',
                 14, 14, 0, 5000, true, true, 1)
            ON CONFLICT ("Id") DO NOTHING;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
