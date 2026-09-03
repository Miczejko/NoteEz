using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteEz_Server.Migrations
{
    /// <inheritdoc />
    public partial class InviteByUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedEmail",
                table: "GroupInvites");

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedUserId",
                table: "GroupInvites",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvites_InvitedUserId",
                table: "GroupInvites",
                column: "InvitedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvites_Users_InvitedUserId",
                table: "GroupInvites",
                column: "InvitedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvites_Users_InvitedUserId",
                table: "GroupInvites");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvites_InvitedUserId",
                table: "GroupInvites");

            migrationBuilder.DropColumn(
                name: "InvitedUserId",
                table: "GroupInvites");

            migrationBuilder.AddColumn<string>(
                name: "InvitedEmail",
                table: "GroupInvites",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
