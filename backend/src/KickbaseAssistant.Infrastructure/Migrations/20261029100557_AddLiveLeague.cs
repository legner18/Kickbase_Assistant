using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveLeague : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TimelineFetchedAt",
                table: "LiveMatchPlayers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimelinePoints",
                table: "LiveMatchPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiveManagerScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatchdayNumber = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    PointsCounted = table.Column<bool>(type: "boolean", nullable: false),
                    MatchdayRank = table.Column<int>(type: "integer", nullable: false),
                    SeasonPoints = table.Column<int>(type: "integer", nullable: false),
                    SeasonRank = table.Column<int>(type: "integer", nullable: false),
                    LineupSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveManagerScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveMatchPlayerEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LiveMatchPlayerId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventTypeId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveMatchPlayerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveMatchPlayerEvents_LiveMatchPlayers_LiveMatchPlayerId",
                        column: x => x.LiveMatchPlayerId,
                        principalTable: "LiveMatchPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveManagerPlayers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LiveManagerScoreId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InLineup = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveManagerPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveManagerPlayers_LiveManagerScores_LiveManagerScoreId",
                        column: x => x.LiveManagerScoreId,
                        principalTable: "LiveManagerScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveManagerPlayers_LiveManagerScoreId_PlayerId",
                table: "LiveManagerPlayers",
                columns: new[] { "LiveManagerScoreId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveManagerScores_LeagueId_ManagerId_MatchdayNumber",
                table: "LiveManagerScores",
                columns: new[] { "LeagueId", "ManagerId", "MatchdayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveMatchPlayerEvents_LiveMatchPlayerId_EventId",
                table: "LiveMatchPlayerEvents",
                columns: new[] { "LiveMatchPlayerId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveManagerPlayers");

            migrationBuilder.DropTable(
                name: "LiveMatchPlayerEvents");

            migrationBuilder.DropTable(
                name: "LiveManagerScores");

            migrationBuilder.DropColumn(
                name: "TimelineFetchedAt",
                table: "LiveMatchPlayers");

            migrationBuilder.DropColumn(
                name: "TimelinePoints",
                table: "LiveMatchPlayers");
        }
    }
}
