using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class PositionLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Certifications",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Languages",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinimumEducationLevel",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumExperienceMonths",
                table: "Positions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NiceToHave",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredSkills",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Responsibilities",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Certifications",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Languages",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "MinimumEducationLevel",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "MinimumExperienceMonths",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "NiceToHave",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "RequiredSkills",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Responsibilities",
                table: "Positions");
        }
    }
}
