using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemasterGuru.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssetDisplayVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayVersion",
                table: "Assets",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Original");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayVersion",
                table: "Assets");
        }
    }
}
