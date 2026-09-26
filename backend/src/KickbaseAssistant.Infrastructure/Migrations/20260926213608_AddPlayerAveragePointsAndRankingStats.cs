using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAveragePointsAndRankingStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AveragePoints",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentMatchday",
                table: "LeagueManagerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchdayPoints",
                table: "LeagueManagerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchdayRank",
                table: "LeagueManagerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeasonPoints",
                table: "LeagueManagerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeasonRank",
                table: "LeagueManagerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragePoints",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CurrentMatchday",
                table: "LeagueManagerStats");

            migrationBuilder.DropColumn(
                name: "MatchdayPoints",
                table: "LeagueManagerStats");

            migrationBuilder.DropColumn(
                name: "MatchdayRank",
                table: "LeagueManagerStats");

            migrationBuilder.DropColumn(
                name: "SeasonPoints",
                table: "LeagueManagerStats");

            migrationBuilder.DropColumn(
                name: "SeasonRank",
                table: "LeagueManagerStats");
        }
    }
}
