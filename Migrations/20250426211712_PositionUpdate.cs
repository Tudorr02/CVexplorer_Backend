using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class PositionUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Weights_Certification",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_ExperienceMonths",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_Languages",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_Level",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_MinimumEducation",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_NiceToHave",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_RequiredSkills",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Weights_Responsibilities",
                table: "Positions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Weights_Certification",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_ExperienceMonths",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_Languages",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_Level",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_MinimumEducation",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_NiceToHave",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_RequiredSkills",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Weights_Responsibilities",
                table: "Positions");
        }
    }
}
