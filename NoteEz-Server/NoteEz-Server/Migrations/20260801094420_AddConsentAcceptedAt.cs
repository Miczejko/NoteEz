using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteEz_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentAcceptedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAt",
                table: "PendingRegistrations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "PendingRegistrations");
        }
    }
}
