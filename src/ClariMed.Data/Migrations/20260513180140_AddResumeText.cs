using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResumeText",
                table: "ClinicSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumeText",
                table: "ClinicSettings");
        }
    }
}
