using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Studies_AccessionNumber",
                table: "Studies",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_CreatedAt",
                table: "Studies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Studies_DeletedAt",
                table: "Studies",
                column: "DeletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Studies_AccessionNumber",
                table: "Studies");

            migrationBuilder.DropIndex(
                name: "IX_Studies_CreatedAt",
                table: "Studies");

            migrationBuilder.DropIndex(
                name: "IX_Studies_DeletedAt",
                table: "Studies");
        }
    }
}
