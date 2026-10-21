using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class TradeTimeInYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AtYearOffset",
                table: "JournalTrades",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtYearOffset",
                table: "JournalTrades");
        }
    }
}
