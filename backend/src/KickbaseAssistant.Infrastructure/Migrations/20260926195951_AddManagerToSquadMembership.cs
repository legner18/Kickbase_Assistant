using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KickbaseAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerToSquadMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SquadMemberships_LeagueId_PlayerId",
                table: "SquadMemberships");

            migrationBuilder.AddColumn<string>(
                name: "ManagerId",
                table: "SquadMemberships",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                table: "SquadMemberships",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SquadMemberships_LeagueId_ManagerId_PlayerId",
                table: "SquadMemberships",
                columns: new[] { "LeagueId", "ManagerId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SquadMemberships_LeagueId_ManagerId_PlayerId",
                table: "SquadMemberships");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "SquadMemberships");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                table: "SquadMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_SquadMemberships_LeagueId_PlayerId",
                table: "SquadMemberships",
                columns: new[] { "LeagueId", "PlayerId" },
                unique: true);
        }
    }
}
