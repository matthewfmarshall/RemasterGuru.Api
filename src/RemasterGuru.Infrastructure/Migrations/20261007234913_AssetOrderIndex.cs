using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemasterGuru.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssetOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "Assets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AlbumId_OrderIndex",
                table: "Assets",
                columns: new[] { "AlbumId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_AlbumId_OrderIndex",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "Assets");
        }
    }
}
