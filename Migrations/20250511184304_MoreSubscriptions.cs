using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class MoreSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions",
                column: "RoundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSubscriptions_RoundId",
                table: "IntegrationSubscriptions",
                column: "RoundId",
                unique: true);
        }
    }
}
