using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicName = table.Column<string>(type: "TEXT", nullable: false),
                    AETitle = table.Column<string>(type: "TEXT", nullable: false),
                    DicomPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchivePath = table.Column<string>(type: "TEXT", nullable: false),
                    DatabasePath = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveIntervalMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sex = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Studies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudyInstanceUid = table.Column<string>(type: "TEXT", nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessionNumber = table.Column<string>(type: "TEXT", nullable: false),
                    StudyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StudyDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Modality = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Studies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Studies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudyId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", nullable: false),
                    PrinterName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesInstanceUid = table.Column<string>(type: "TEXT", nullable: false),
                    StudyId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Modality = table.Column<string>(type: "TEXT", nullable: false),
                    SeriesDescription = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Series_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SopInstanceUid = table.Column<string>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: true),
                    Rows = table.Column<int>(type: "INTEGER", nullable: true),
                    Columns = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Images_SeriesId",
                table: "Images",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_SopInstanceUid",
                table: "Images",
                column: "SopInstanceUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Name",
                table: "Patients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PatientId",
                table: "Patients",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_CreatedAt",
                table: "PrintJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Status",
                table: "PrintJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_StudyId",
                table: "PrintJobs",
                column: "StudyId");

            migrationBuilder.CreateIndex(
                name: "IX_Series_SeriesInstanceUid",
                table: "Series",
                column: "SeriesInstanceUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_StudyId",
                table: "Series",
                column: "StudyId");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_Modality",
                table: "Studies",
                column: "Modality");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_PatientId",
                table: "Studies",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_StudyDate",
                table: "Studies",
                column: "StudyDate");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_StudyInstanceUid",
                table: "Studies",
                column: "StudyInstanceUid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicSettings");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropTable(
                name: "Studies");

            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
