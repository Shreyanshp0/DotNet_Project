using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNet_Project.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalSentimentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalSentimentAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JournalEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sentiment = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EmotionIndicators = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AIInsight = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalSentimentAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalSentimentAnalyses_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalSentimentAnalyses_JournalEntryId",
                table: "JournalSentimentAnalyses",
                column: "JournalEntryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalSentimentAnalyses");
        }
    }
}
