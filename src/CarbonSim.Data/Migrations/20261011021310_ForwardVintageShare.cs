using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class ForwardVintageShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default is zero rather than the engine's calibrated 0.20: the runs already saved
            // were played before the host sold forward vintages at all, so this records what they
            // actually did. A run loaded from here keeps the auctions its own snapshot carries.
            migrationBuilder.AddColumn<decimal>(
                name: "ForwardVintageShare",
                table: "Parameters",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForwardVintageShare",
                table: "Parameters");
        }
    }
}
