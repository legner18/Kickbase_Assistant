using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaInsightsAndMarkierungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketListingHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ListedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketListingHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMarkings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Labels = table.Column<string[]>(type: "text[]", nullable: false),
                    OwnLineupEstimate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMarkings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketListingHistories_LeagueId_PlayerId_Outcome",
                table: "MarketListingHistories",
                columns: new[] { "LeagueId", "PlayerId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMarkings_LeagueId_PlayerId",
                table: "PlayerMarkings",
                columns: new[] { "LeagueId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketListingHistories");

            migrationBuilder.DropTable(
                name: "PlayerMarkings");
        }
    }
}
