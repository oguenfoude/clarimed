using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVirtualPrinterInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DicomNodes");

            migrationBuilder.DropTable(
                name: "InboxDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DicomNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AETitle = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DicomNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssignedToStudyId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
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
    }
}
