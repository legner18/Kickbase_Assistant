using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueManagerStats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EstimatedBudget = table.Column<double>(type: "double precision", nullable: false),
                    AverageOverpayPercent = table.Column<double>(type: "double precision", nullable: true),
                    TransferCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueManagerStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    OtherManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MarketValueAtTransfer = table.Column<double>(type: "double precision", nullable: true),
                    OverpayPercent = table.Column<double>(type: "double precision", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueTransfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueManagerStats_LeagueId_ManagerId",
                table: "LeagueManagerStats",
                columns: new[] { "LeagueId", "ManagerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueTransfers_LeagueId",
                table: "LeagueTransfers",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueTransfers_ManagerId_PlayerId_Type_OccurredAt",
                table: "LeagueTransfers",
                columns: new[] { "ManagerId", "PlayerId", "Type", "OccurredAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueManagerStats");

            migrationBuilder.DropTable(
                name: "LeagueTransfers");
        }
    }
}
