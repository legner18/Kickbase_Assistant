using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchOdds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompetitionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HomeOdds = table.Column<double>(type: "double precision", nullable: false),
                    DrawOdds = table.Column<double>(type: "double precision", nullable: false),
                    AwayOdds = table.Column<double>(type: "double precision", nullable: false),
                    HomeWinProbability = table.Column<double>(type: "double precision", nullable: false),
                    DrawProbability = table.Column<double>(type: "double precision", nullable: false),
                    AwayWinProbability = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedHomeGoals = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedAwayGoals = table.Column<double>(type: "double precision", nullable: false),
                    BookmakerCount = table.Column<int>(type: "integer", nullable: false),
                    SourceHomeTeam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceAwayTeam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchOdds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchOdds_MatchId",
                table: "MatchOdds",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchOdds");
        }
    }
}
