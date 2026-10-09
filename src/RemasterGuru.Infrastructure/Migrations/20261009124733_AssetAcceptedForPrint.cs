using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemasterGuru.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssetAcceptedForPrint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptedForPrint",
                table: "Assets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedForPrint",
                table: "Assets");
        }
    }
}
