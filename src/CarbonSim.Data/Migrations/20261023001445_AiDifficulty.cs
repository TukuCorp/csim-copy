using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiDifficulty",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                // A run saved before the setting existed played at Normal (BotDifficulty value 1), not Easy (0).
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDifficulty",
                table: "Parameters");
        }
    }
}
