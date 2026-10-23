using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManualSurrender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticSurrender",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                // Saved exercises predate the switch and surrendered automatically, so they keep doing so.
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnlimitedBanking",
                table: "Parameters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SurrenderInstructions",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Allowances = table.Column<decimal>(type: "TEXT", nullable: false),
                    Offsets = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurrenderInstructions", x => new { x.SimulationId, x.CompanyId, x.Year });
                    table.ForeignKey(
                        name: "FK_SurrenderInstructions_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurrenderInstructions");

            migrationBuilder.DropColumn(
                name: "AutomaticSurrender",
                table: "Parameters");

            migrationBuilder.DropColumn(
                name: "UnlimitedBanking",
                table: "Parameters");
        }
    }
}
