using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClariMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Studies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Studies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Studies_IsDeleted",
                table: "Studies",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Studies_IsDeleted",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Studies");
        }
    }
}
