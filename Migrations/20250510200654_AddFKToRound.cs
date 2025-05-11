using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddFKToRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoundId",
                table: "IntegrationSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions",
                column: "RoundId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationSubscriptions_Rounds_RoundId",
                table: "IntegrationSubscriptions",
                column: "RoundId",
                principalTable: "Rounds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationSubscriptions_Rounds_RoundId",
                table: "IntegrationSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions");

            migrationBuilder.DropColumn(
                name: "RoundId",
                table: "IntegrationSubscriptions");
        }
    }
}
