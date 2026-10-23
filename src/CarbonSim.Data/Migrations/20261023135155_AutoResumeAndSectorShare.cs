using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutoResumeAndSectorShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FreeAllocationShare",
                table: "Sectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoResumeAfterAuction",
                table: "Clocks",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResumeAt",
                table: "Clocks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeAllocationShare",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "AutoResumeAfterAuction",
                table: "Clocks");

            migrationBuilder.DropColumn(
                name: "ResumeAt",
                table: "Clocks");
        }
    }
}
