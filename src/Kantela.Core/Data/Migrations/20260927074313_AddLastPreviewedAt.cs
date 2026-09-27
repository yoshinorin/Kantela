using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kantela.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastPreviewedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPreviewedAt",
                table: "Sites",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPreviewedAt",
                table: "Sites");
        }
    }
}
