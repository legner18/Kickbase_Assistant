using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFixture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamFixtures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchdayNumber = table.Column<int>(type: "integer", nullable: false),
                    OpponentTeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OpponentTeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsHome = table.Column<bool>(type: "boolean", nullable: false),
                    DifficultyTier = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamFixtures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamFixtures_CompetitionId_TeamId_MatchdayNumber",
                table: "TeamFixtures",
                columns: new[] { "CompetitionId", "TeamId", "MatchdayNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamFixtures");
        }
    }
}
