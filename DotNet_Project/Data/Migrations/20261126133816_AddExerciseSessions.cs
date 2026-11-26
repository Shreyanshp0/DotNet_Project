using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNet_Project.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChildProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WasCompleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseSessions_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSessions_ChildProfileId_CompletedAt",
                table: "ExerciseSessions",
                columns: new[] { "ChildProfileId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseSessions");
        }
    }
}
