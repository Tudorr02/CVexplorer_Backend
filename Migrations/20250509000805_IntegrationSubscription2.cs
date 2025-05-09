using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationSubscription2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "IntegrationSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "IntegrationSubscriptions");
        }
    }
}
