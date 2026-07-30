using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteEz_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteScheduledDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "Notes",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "Notes");
        }
    }
}
