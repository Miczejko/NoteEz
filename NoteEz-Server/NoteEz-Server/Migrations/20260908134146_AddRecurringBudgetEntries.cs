using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteEz_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBudgetEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringBudgetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBudgetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBudgetEntries_BudgetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "BudgetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringBudgetEntries_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBudgetEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBudgetEntries_CategoryId",
                table: "RecurringBudgetEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBudgetEntries_CreatedByUserId",
                table: "RecurringBudgetEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBudgetEntries_GroupId",
                table: "RecurringBudgetEntries",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringBudgetEntries");
        }
    }
}
