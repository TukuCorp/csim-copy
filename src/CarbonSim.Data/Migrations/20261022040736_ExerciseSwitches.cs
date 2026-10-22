using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExerciseSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiTradeAssistance",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExchangeAccess",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "LeaderboardVisible",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OtcAccess",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiTradeAssistance",
                table: "Parameters");

            migrationBuilder.DropColumn(
                name: "ExchangeAccess",
                table: "Parameters");

            migrationBuilder.DropColumn(
                name: "LeaderboardVisible",
                table: "Parameters");

            migrationBuilder.DropColumn(
                name: "OtcAccess",
                table: "Parameters");
        }
    }
}
