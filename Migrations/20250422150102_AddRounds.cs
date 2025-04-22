using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Round",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Round", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Round_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    CvId = table.Column<int>(type: "int", nullable: false),
                    Selected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundEntry_CVs_CvId",
                        column: x => x.CvId,
                        principalTable: "CVs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoundEntry_Round_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Round",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Round_PositionId",
                table: "Round",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundEntry_CvId",
                table: "RoundEntry",
                column: "CvId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundEntry_RoundId",
                table: "RoundEntry",
                column: "RoundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoundEntry");

            migrationBuilder.DropTable(
                name: "Round");
        }
    }
}
