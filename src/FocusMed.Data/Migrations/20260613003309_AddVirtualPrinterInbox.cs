using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualPrinterInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrinterRegistered",
                table: "ClinicSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InboxDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedToStudyId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboxDocuments_Studies_AssignedToStudyId",
                        column: x => x.AssignedToStudyId,
                        principalTable: "Studies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxDocuments_AssignedToStudyId",
                table: "InboxDocuments",
                column: "AssignedToStudyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxDocuments");

            migrationBuilder.DropColumn(
                name: "PrinterRegistered",
                table: "ClinicSettings");
        }
    }
}
