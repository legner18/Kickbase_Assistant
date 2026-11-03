using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveMatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompetitionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchdayNumber = table.Column<int>(type: "integer", nullable: false),
                    KickoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HomeTeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HomeTeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HomeTeamSymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AwayTeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AwayTeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AwayTeamSymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Minute = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    HomeTeamPoints = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamPoints = table.Column<int>(type: "integer", nullable: true),
                    PointsUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveMatchPlayers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    EventKeys = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveMatchPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveMatchPlayers_LiveMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "LiveMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveMatches_CompetitionId_KickoffAt",
                table: "LiveMatches",
                columns: new[] { "CompetitionId", "KickoffAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveMatchPlayers_MatchId_TeamId_PlayerId",
                table: "LiveMatchPlayers",
                columns: new[] { "MatchId", "TeamId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveMatchPlayers");

            migrationBuilder.DropTable(
                name: "LiveMatches");
        }
    }
}
