using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusMed.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCompletionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Studies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageCount",
                table: "Studies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastImageReceivedAt",
                table: "Studies",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Studies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FrameCount",
                table: "Images",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Studies_Status",
                table: "Studies",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Studies_Status",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "ImageCount",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "LastImageReceivedAt",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "FrameCount",
                table: "Images");
        }
    }
}
