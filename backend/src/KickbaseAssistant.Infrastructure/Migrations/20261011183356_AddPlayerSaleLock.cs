using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSaleLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerSaleLocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSaleLocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSaleLocks_LeagueId_PlayerId",
                table: "PlayerSaleLocks",
                columns: new[] { "LeagueId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerSaleLocks");
        }
    }
}
