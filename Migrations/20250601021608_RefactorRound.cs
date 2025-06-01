using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class RefactorRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundEntries_Rounds_RoundId",
                table: "RoundEntries");

            migrationBuilder.DropColumn(
                name: "Selected",
                table: "RoundEntries");

            migrationBuilder.RenameColumn(
                name: "RoundId",
                table: "RoundEntries",
                newName: "StageId");

            migrationBuilder.RenameIndex(
                name: "IX_RoundEntries_RoundId",
                table: "RoundEntries",
                newName: "IX_RoundEntries_StageId");

            migrationBuilder.CreateTable(
                name: "RoundStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundStages_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoundStages_RoundId",
                table: "RoundStages",
                column: "RoundId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoundEntries_RoundStages_StageId",
                table: "RoundEntries",
                column: "StageId",
                principalTable: "RoundStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundEntries_RoundStages_StageId",
                table: "RoundEntries");

            migrationBuilder.DropTable(
                name: "RoundStages");

            migrationBuilder.RenameColumn(
                name: "StageId",
                table: "RoundEntries",
                newName: "RoundId");

            migrationBuilder.RenameIndex(
                name: "IX_RoundEntries_StageId",
                table: "RoundEntries",
                newName: "IX_RoundEntries_RoundId");

            migrationBuilder.AddColumn<bool>(
                name: "Selected",
                table: "RoundEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundEntries_Rounds_RoundId",
                table: "RoundEntries",
                column: "RoundId",
                principalTable: "Rounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
