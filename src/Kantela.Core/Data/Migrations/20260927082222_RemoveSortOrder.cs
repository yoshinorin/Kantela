using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kantela.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_SortOrder",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Sites");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Sites",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_SortOrder",
                table: "Sites",
                column: "SortOrder");
        }
    }
}
