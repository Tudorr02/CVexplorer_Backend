using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVexplorer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_CvEvaluationResults_CVs_CvId",
            //    table: "CvEvaluationResults");

            //migrationBuilder.DropIndex(
            //    name: "IX_CvEvaluationResults_CvId",
            //    table: "CvEvaluationResults");

            //migrationBuilder.DropColumn(
            //    name: "CvId",
            //    table: "CvEvaluationResults");

            migrationBuilder.CreateIndex(
                name: "IX_CVs_EvaluationId",
                table: "CVs",
                column: "EvaluationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CVs_CvEvaluationResults_EvaluationId",
                table: "CVs",
                column: "EvaluationId",
                principalTable: "CvEvaluationResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CVs_CvEvaluationResults_EvaluationId",
                table: "CVs");

            migrationBuilder.DropIndex(
                name: "IX_CVs_EvaluationId",
                table: "CVs");

            migrationBuilder.AddColumn<int>(
                name: "CvId",
                table: "CvEvaluationResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvEvaluationResults_CvId",
                table: "CvEvaluationResults",
                column: "CvId",
                unique: true,
                filter: "[CvId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CvEvaluationResults_CVs_CvId",
                table: "CvEvaluationResults",
                column: "CvId",
                principalTable: "CVs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
