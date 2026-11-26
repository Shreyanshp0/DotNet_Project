using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNet_Project.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChildProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    WeekEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ObservationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AreasForSupportJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RecommendationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    DataLimitationsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyInsights_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyInsights_ChildProfileId_WeekStartDate",
                table: "WeeklyInsights",
                columns: new[] { "ChildProfileId", "WeekStartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyInsights");
        }
    }
}
