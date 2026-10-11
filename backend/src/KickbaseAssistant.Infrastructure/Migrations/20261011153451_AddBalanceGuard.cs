using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BalanceAlertsSent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchdayNumber = table.Column<int>(type: "integer", nullable: false),
                    Window = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceAlertsSent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueBalanceStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Balance = table.Column<double>(type: "double precision", nullable: false),
                    CreditLimit = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NextMatchdayNumber = table.Column<int>(type: "integer", nullable: false),
                    NextKickoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RosterGaps = table.Column<string[]>(type: "text[]", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueBalanceStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceAlertsSent_LeagueId_MatchdayNumber_Window",
                table: "BalanceAlertsSent",
                columns: new[] { "LeagueId", "MatchdayNumber", "Window" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueBalanceStatuses_LeagueId",
                table: "LeagueBalanceStatuses",
                column: "LeagueId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceAlertsSent");

            migrationBuilder.DropTable(
                name: "LeagueBalanceStatuses");
        }
    }
}
