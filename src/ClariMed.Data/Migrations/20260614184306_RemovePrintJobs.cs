using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePrintJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    StudyId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    MergedPdfPath = table.Column<string>(type: "TEXT", nullable: true),
                    PrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrinterName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_CreatedAt",
                table: "PrintJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_DocumentId",
                table: "PrintJobs",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Status",
                table: "PrintJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_StudyId",
                table: "PrintJobs",
                column: "StudyId");
        }
    }
}
