using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ClinicSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "ClinicSettings");
        }
    }
}
