using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsAndMergedPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentId",
                table: "PrintJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergedPdfPath",
                table: "PrintJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentOutputPath",
                table: "ClinicSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MergedPdfOutputPath",
                table: "ClinicSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WatchFolderPath",
                table: "ClinicSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    PdfFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConvertedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    StudyId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_DocumentId",
                table: "PrintJobs",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReceivedAt",
                table: "Documents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StudyId",
                table: "Documents",
                column: "StudyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Documents_DocumentId",
                table: "PrintJobs",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Documents_DocumentId",
                table: "PrintJobs");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_DocumentId",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "MergedPdfPath",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "DocumentOutputPath",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "MergedPdfOutputPath",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "WatchFolderPath",
                table: "ClinicSettings");
        }
    }
}
