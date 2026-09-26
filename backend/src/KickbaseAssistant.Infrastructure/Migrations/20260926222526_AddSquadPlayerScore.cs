using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSquadPlayerScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SquadPlayerScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", nullable: false),
                    MomentumScore = table.Column<double>(type: "double precision", nullable: false),
                    LineupScore = table.Column<double>(type: "double precision", nullable: false),
                    FixtureScore = table.Column<double>(type: "double precision", nullable: false),
                    OverallScore = table.Column<double>(type: "double precision", nullable: false),
                    MarketValueChangeAbs = table.Column<double>(type: "double precision", nullable: true),
                    MarketValueChangePercent = table.Column<double>(type: "double precision", nullable: true),
                    ScannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquadPlayerScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SquadPlayerScores_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SquadPlayerScores_LeagueId_OverallScore",
                table: "SquadPlayerScores",
                columns: new[] { "LeagueId", "OverallScore" });

            migrationBuilder.CreateIndex(
                name: "IX_SquadPlayerScores_PlayerId",
                table: "SquadPlayerScores",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SquadPlayerScores");
        }
    }
}
