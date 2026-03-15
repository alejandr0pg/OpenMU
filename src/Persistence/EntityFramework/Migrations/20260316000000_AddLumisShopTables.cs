using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddLumisShopTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LumisShopCategory",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IconIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LumisShopCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LumisShopItem",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemGroup = table.Column<short>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    LumisPrice = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LumisShopItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LumisShopItem_LumisShopCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "data",
                        principalTable: "LumisShopCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LumisShopItem_CategoryId",
                schema: "data",
                table: "LumisShopItem",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LumisShopItem",
                schema: "data");

            migrationBuilder.DropTable(
                name: "LumisShopCategory",
                schema: "data");
        }
    }
}
