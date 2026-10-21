using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartYearAbatement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StartsAt",
                table: "ImplementedAbatements",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "ImplementationYears",
                table: "AbatementOptions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<decimal>(
                name: "ImplementationYears",
                table: "AbatementMenu",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartsAt",
                table: "ImplementedAbatements");

            migrationBuilder.AlterColumn<int>(
                name: "ImplementationYears",
                table: "AbatementOptions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "ImplementationYears",
                table: "AbatementMenu",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");
        }
    }
}
