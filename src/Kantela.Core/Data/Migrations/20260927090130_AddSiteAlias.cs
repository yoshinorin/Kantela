using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kantela.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Sites",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Sites");
        }
    }
}
