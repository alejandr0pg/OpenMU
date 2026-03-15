using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddLumisAndCasinoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LumisBalance",
                schema: "data",
                table: "Account",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "VipExpiresAt",
                schema: "data",
                table: "Account",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "StorePriceCurrency",
                schema: "data",
                table: "Item",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LumisBalance",
                schema: "data",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "VipExpiresAt",
                schema: "data",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "StorePriceCurrency",
                schema: "data",
                table: "Item");
        }
    }
}
