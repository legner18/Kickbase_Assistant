using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSquadMembershipAndMarketValueChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MarketValueChangeAbs",
                table: "MarketScanResults",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MarketValueChangePercent",
                table: "MarketScanResults",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SquadMemberships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquadMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SquadMemberships_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SquadMemberships_LeagueId_PlayerId",
                table: "SquadMemberships",
                columns: new[] { "LeagueId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SquadMemberships_PlayerId",
                table: "SquadMemberships",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SquadMemberships");

            migrationBuilder.DropColumn(
                name: "MarketValueChangeAbs",
                table: "MarketScanResults");

            migrationBuilder.DropColumn(
                name: "MarketValueChangePercent",
                table: "MarketScanResults");
        }
    }
}
