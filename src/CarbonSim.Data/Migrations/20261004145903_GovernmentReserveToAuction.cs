using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class GovernmentReserveToAuction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GovernmentReserveToAuctionPercent",
                table: "Parameters",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GovernmentReserveToAuctionPercent",
                table: "Parameters");
        }
    }
}
