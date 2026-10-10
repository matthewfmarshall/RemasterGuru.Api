using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemasterGuru.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserAuth0Subject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Auth0Subject",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Auth0Subject",
                table: "Users",
                column: "Auth0Subject",
                unique: true,
                filter: "[Auth0Subject] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Auth0Subject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0Subject",
                table: "Users");
        }
    }
}
