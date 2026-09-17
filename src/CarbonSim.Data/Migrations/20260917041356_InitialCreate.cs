using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarbonSim.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResetCodeHash = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    ResetCodeExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResetCodeAttempts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Simulations",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    RandomState = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentYear = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simulations", x => x.SimulationId);
                });

            migrationBuilder.CreateTable(
                name: "AllocationYears",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Cap = table.Column<decimal>(type: "TEXT", nullable: false),
                    FreeAllocation = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationYears", x => new { x.SimulationId, x.Year });
                    table.ForeignKey(
                        name: "FK_AllocationYears_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCleared = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnsoldVolume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => new { x.SimulationId, x.Year, x.Sequence });
                    table.ForeignKey(
                        name: "FK_Auctions_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => new { x.SimulationId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_CashMovements_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clocks",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentYear = table.Column<int>(type: "INTEGER", nullable: false),
                    Elapsed = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RunningSince = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HaltAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OpenNoticedThrough = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedThrough = table.Column<int>(type: "INTEGER", nullable: false),
                    CloseNoticedThrough = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosedThrough = table.Column<int>(type: "INTEGER", nullable: false),
                    HaltedForYearEnd = table.Column<bool>(type: "INTEGER", nullable: false),
                    PauseAfterAuction = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuctionNotice = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clocks", x => x.SimulationId);
                    table.ForeignKey(
                        name: "FK_Clocks_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Exchanges",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastOrderId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchanges", x => x.SimulationId);
                    table.ForeignKey(
                        name: "FK_Exchanges_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Government",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revenue = table.Column<decimal>(type: "TEXT", nullable: false),
                    HasIssued = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Government", x => x.SimulationId);
                    table.ForeignKey(
                        name: "FK_Government_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LedgerGrantedYears",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerGrantedYears", x => new { x.SimulationId, x.Year });
                    table.ForeignKey(
                        name: "FK_LedgerGrantedYears_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtcOffers",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferId = table.Column<long>(type: "INTEGER", nullable: false),
                    SellerUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    BuyerUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtcOffers", x => new { x.SimulationId, x.OfferId });
                    table.ForeignKey(
                        name: "FK_OtcOffers_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => new { x.SimulationId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_Players_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EmissionShare = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => new { x.SimulationId, x.Name });
                    table.ForeignKey(
                        name: "FK_Sectors_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradingSystems",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingSystemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSystems", x => new { x.SimulationId, x.TradingSystemId });
                    table.ForeignKey(
                        name: "FK_TradingSystems_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionLots",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsForward = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionLots", x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage });
                    table.ForeignKey(
                        name: "FK_AuctionLots_Auctions_SimulationId_Year_Sequence",
                        columns: x => new { x.SimulationId, x.Year, x.Sequence },
                        principalTable: "Auctions",
                        principalColumns: new[] { "SimulationId", "Year", "Sequence" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionResults",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    OfferedVolume = table.Column<decimal>(type: "TEXT", nullable: false),
                    ClearingPrice = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionResults", x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage });
                    table.ForeignKey(
                        name: "FK_AuctionResults_Auctions_SimulationId_Year_Sequence",
                        columns: x => new { x.SimulationId, x.Year, x.Sequence },
                        principalTable: "Auctions",
                        principalColumns: new[] { "SimulationId", "Year", "Sequence" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderBooks",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    LastTradePrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBooks", x => new { x.SimulationId, x.ProductKind, x.Vintage });
                    table.ForeignKey(
                        name: "FK_OrderBooks_Exchanges_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Exchanges",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentIssued",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentIssued", x => new { x.SimulationId, x.Vintage });
                    table.ForeignKey(
                        name: "FK_GovernmentIssued_Government_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Government",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentReserves",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentReserves", x => new { x.SimulationId, x.Vintage });
                    table.ForeignKey(
                        name: "FK_GovernmentReserves_Government_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Government",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbatementMenu",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sector = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AnnualReductionShare = table.Column<decimal>(type: "TEXT", nullable: false),
                    UpfrontCostPerTonne = table.Column<decimal>(type: "TEXT", nullable: false),
                    AnnualNetRevenuePerTonne = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImplementationYears = table.Column<int>(type: "INTEGER", nullable: false),
                    LifetimeYears = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbatementMenu", x => new { x.SimulationId, x.Sector, x.Code });
                    table.ForeignKey(
                        name: "FK_AbatementMenu_Sectors_SimulationId_Sector",
                        columns: x => new { x.SimulationId, x.Sector },
                        principalTable: "Sectors",
                        principalColumns: new[] { "SimulationId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Capital = table.Column<decimal>(type: "TEXT", nullable: false),
                    EscrowedCash = table.Column<decimal>(type: "TEXT", nullable: false),
                    OverdraftLimit = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => new { x.SimulationId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_Companies_Players_SimulationId_OwnerPlayerId",
                        columns: x => new { x.SimulationId, x.OwnerPlayerId },
                        principalTable: "Players",
                        principalColumns: new[] { "SimulationId", "PlayerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Companies_Sectors_SimulationId_Sector",
                        columns: x => new { x.SimulationId, x.Sector },
                        principalTable: "Sectors",
                        principalColumns: new[] { "SimulationId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Companies_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parameters",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingSystemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cap = table.Column<decimal>(type: "TEXT", nullable: false),
                    AnnualCapReductionRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    FreeAllocationShare = table.Column<decimal>(type: "TEXT", nullable: false),
                    Years = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetUsageLimit = table.Column<decimal>(type: "TEXT", nullable: false),
                    BankingLimit = table.Column<decimal>(type: "TEXT", nullable: false),
                    PenaltyPerTonne = table.Column<decimal>(type: "TEXT", nullable: false),
                    PenaltyAllowanceDebit = table.Column<decimal>(type: "TEXT", nullable: false),
                    AuctionFloorPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AuctionCeilingPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AuctionsPerYear = table.Column<int>(type: "INTEGER", nullable: false),
                    YearLength = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    AuctionDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TradingOpenShareOfYear = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolatilityBand = table.Column<decimal>(type: "TEXT", nullable: false),
                    OverdraftInterestRate = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parameters", x => new { x.SimulationId, x.TradingSystemId });
                    table.ForeignKey(
                        name: "FK_Parameters_TradingSystems_SimulationId_TradingSystemId",
                        columns: x => new { x.SimulationId, x.TradingSystemId },
                        principalTable: "TradingSystems",
                        principalColumns: new[] { "SimulationId", "TradingSystemId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionAwards",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    BidId = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionAwards", x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage, x.BidId });
                    table.ForeignKey(
                        name: "FK_AuctionAwards_AuctionResults_SimulationId_Year_Sequence_Vintage",
                        columns: x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage },
                        principalTable: "AuctionResults",
                        principalColumns: new[] { "SimulationId", "Year", "Sequence", "Vintage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionRejectedBids",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    BidId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionRejectedBids", x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage, x.BidId });
                    table.ForeignKey(
                        name: "FK_AuctionRejectedBids_AuctionResults_SimulationId_Year_Sequence_Vintage",
                        columns: x => new { x.SimulationId, x.Year, x.Sequence, x.Vintage },
                        principalTable: "AuctionResults",
                        principalColumns: new[] { "SimulationId", "Year", "Sequence", "Vintage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookTrades",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    BuyOrderId = table.Column<long>(type: "INTEGER", nullable: false),
                    SellOrderId = table.Column<long>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookTrades", x => new { x.SimulationId, x.ProductKind, x.Vintage, x.Sequence });
                    table.ForeignKey(
                        name: "FK_BookTrades_OrderBooks_SimulationId_ProductKind_Vintage",
                        columns: x => new { x.SimulationId, x.ProductKind, x.Vintage },
                        principalTable: "OrderBooks",
                        principalColumns: new[] { "SimulationId", "ProductKind", "Vintage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionBids",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    BidId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    Won = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionBids", x => new { x.SimulationId, x.Year, x.Sequence, x.BidId });
                    table.ForeignKey(
                        name: "FK_AuctionBids_Auctions_SimulationId_Year_Sequence",
                        columns: x => new { x.SimulationId, x.Year, x.Sequence },
                        principalTable: "Auctions",
                        principalColumns: new[] { "SimulationId", "Year", "Sequence" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionBids_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bots",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    AbatementMargin = table.Column<decimal>(type: "TEXT", nullable: false),
                    BidPriceNoise = table.Column<decimal>(type: "TEXT", nullable: false),
                    BidVolumeFraction = table.Column<decimal>(type: "TEXT", nullable: false),
                    OffsetDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReservationPriceFactor = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bots", x => new { x.SimulationId, x.BotId });
                    table.ForeignKey(
                        name: "FK_Bots_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCompliances",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Obligation = table.Column<decimal>(type: "TEXT", nullable: false),
                    OffsetsSurrendered = table.Column<decimal>(type: "TEXT", nullable: false),
                    AllowancesSurrendered = table.Column<decimal>(type: "TEXT", nullable: false),
                    Banked = table.Column<decimal>(type: "TEXT", nullable: false),
                    Forfeited = table.Column<decimal>(type: "TEXT", nullable: false),
                    Shortfall = table.Column<decimal>(type: "TEXT", nullable: false),
                    PenaltyCash = table.Column<decimal>(type: "TEXT", nullable: false),
                    PenaltyAllowanceDebit = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCompliances", x => new { x.SimulationId, x.Year, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_CompanyCompliances_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalTrades",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    BuyerCompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    SellerCompanyId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalTrades", x => new { x.SimulationId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_JournalTrades_Companies_SimulationId_BuyerCompanyId",
                        columns: x => new { x.SimulationId, x.BuyerCompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalTrades_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "SimulationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAvailable",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAvailable", x => new { x.SimulationId, x.CompanyId, x.ProductKind, x.Vintage });
                    table.ForeignKey(
                        name: "FK_LedgerAvailable_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEscrowed",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEscrowed", x => new { x.SimulationId, x.CompanyId, x.ProductKind, x.Vintage });
                    table.ForeignKey(
                        name: "FK_LedgerEscrowed_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReconciledCompanyYears",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciledCompanyYears", x => new { x.SimulationId, x.CompanyId, x.Year });
                    table.ForeignKey(
                        name: "FK_ReconciledCompanyYears_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaselineEmissions = table.Column<decimal>(type: "TEXT", nullable: false),
                    NormalOperatingProfit = table.Column<decimal>(type: "TEXT", nullable: false),
                    AutoTrade = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => new { x.SimulationId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_Units_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BausGrowthBands",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BausGrowthId = table.Column<int>(type: "INTEGER", nullable: false),
                    TradingSystemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sector = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MinAnnualRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaxAnnualRate = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BausGrowthBands", x => new { x.SimulationId, x.BausGrowthId });
                    table.ForeignKey(
                        name: "FK_BausGrowthBands_Parameters_SimulationId_TradingSystemId",
                        columns: x => new { x.SimulationId, x.TradingSystemId },
                        principalTable: "Parameters",
                        principalColumns: new[] { "SimulationId", "TradingSystemId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotBidSections",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotBidSections", x => new { x.SimulationId, x.BotId, x.Year, x.Section });
                    table.ForeignKey(
                        name: "FK_BotBidSections_Bots_SimulationId_BotId",
                        columns: x => new { x.SimulationId, x.BotId },
                        principalTable: "Bots",
                        principalColumns: new[] { "SimulationId", "BotId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotTriggerDone",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotTriggerDone", x => new { x.SimulationId, x.BotId, x.Year, x.Trigger });
                    table.ForeignKey(
                        name: "FK_BotTriggerDone_Bots_SimulationId_BotId",
                        columns: x => new { x.SimulationId, x.BotId },
                        principalTable: "Bots",
                        principalColumns: new[] { "SimulationId", "BotId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotTriggerTimes",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    AtFractionOfYear = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotTriggerTimes", x => new { x.SimulationId, x.BotId, x.Trigger });
                    table.ForeignKey(
                        name: "FK_BotTriggerTimes_Bots_SimulationId_BotId",
                        columns: x => new { x.SimulationId, x.BotId },
                        principalTable: "Bots",
                        principalColumns: new[] { "SimulationId", "BotId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotUnits",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotUnits", x => new { x.SimulationId, x.BotId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_BotUnits_Bots_SimulationId_BotId",
                        columns: x => new { x.SimulationId, x.BotId },
                        principalTable: "Bots",
                        principalColumns: new[] { "SimulationId", "BotId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbatementOptions",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpfrontCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    AnnualReduction = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImplementationYears = table.Column<int>(type: "INTEGER", nullable: false),
                    LifetimeYears = table.Column<int>(type: "INTEGER", nullable: false),
                    AnnualNetRevenue = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbatementOptions", x => new { x.SimulationId, x.UnitId, x.Code });
                    table.ForeignKey(
                        name: "FK_AbatementOptions_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fines",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FineId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fines", x => new { x.SimulationId, x.FineId });
                    table.ForeignKey(
                        name: "FK_Fines_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImplementedAbatements",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionCode = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ImplementedIn = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatingFromYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpiresAfterYear = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImplementedAbatements", x => new { x.SimulationId, x.UnitId, x.OptionIndex });
                    table.ForeignKey(
                        name: "FK_ImplementedAbatements_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    FillPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: true),
                    StopPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    FilledVolume = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EscrowedCash = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => new { x.SimulationId, x.OrderId });
                    table.ForeignKey(
                        name: "FK_Orders_Companies_SimulationId_CompanyId",
                        columns: x => new { x.SimulationId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "SimulationId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitAllocations",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    BausGrowth = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitAllocations", x => new { x.SimulationId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_UnitAllocations_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitShutdowns",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitShutdowns", x => new { x.SimulationId, x.UnitId, x.Year });
                    table.ForeignKey(
                        name: "FK_UnitShutdowns_Units_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitAllocationYears",
                columns: table => new
                {
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    FreeAllocation = table.Column<decimal>(type: "TEXT", nullable: false),
                    BausEmissions = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitAllocationYears", x => new { x.SimulationId, x.UnitId, x.Year });
                    table.ForeignKey(
                        name: "FK_UnitAllocationYears_UnitAllocations_SimulationId_UnitId",
                        columns: x => new { x.SimulationId, x.UnitId },
                        principalTable: "UnitAllocations",
                        principalColumns: new[] { "SimulationId", "UnitId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyName",
                table: "Accounts",
                column: "CompanyName",
                unique: true,
                filter: "\"CompanyName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionBids_SimulationId_CompanyId",
                table: "AuctionBids",
                columns: new[] { "SimulationId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BausGrowthBands_SimulationId_TradingSystemId",
                table: "BausGrowthBands",
                columns: new[] { "SimulationId", "TradingSystemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bots_SimulationId_CompanyId",
                table: "Bots",
                columns: new[] { "SimulationId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SimulationId_OwnerPlayerId",
                table: "Companies",
                columns: new[] { "SimulationId", "OwnerPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SimulationId_Sector",
                table: "Companies",
                columns: new[] { "SimulationId", "Sector" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCompliances_SimulationId_CompanyId",
                table: "CompanyCompliances",
                columns: new[] { "SimulationId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Fines_SimulationId_UnitId",
                table: "Fines",
                columns: new[] { "SimulationId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalTrades_SimulationId_BuyerCompanyId",
                table: "JournalTrades",
                columns: new[] { "SimulationId", "BuyerCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SimulationId_CompanyId",
                table: "Orders",
                columns: new[] { "SimulationId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SimulationId_UnitId",
                table: "Orders",
                columns: new[] { "SimulationId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingSystems_SimulationId",
                table: "TradingSystems",
                column: "SimulationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_SimulationId_CompanyId",
                table: "Units",
                columns: new[] { "SimulationId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbatementMenu");

            migrationBuilder.DropTable(
                name: "AbatementOptions");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "AllocationYears");

            migrationBuilder.DropTable(
                name: "AuctionAwards");

            migrationBuilder.DropTable(
                name: "AuctionBids");

            migrationBuilder.DropTable(
                name: "AuctionLots");

            migrationBuilder.DropTable(
                name: "AuctionRejectedBids");

            migrationBuilder.DropTable(
                name: "BausGrowthBands");

            migrationBuilder.DropTable(
                name: "BookTrades");

            migrationBuilder.DropTable(
                name: "BotBidSections");

            migrationBuilder.DropTable(
                name: "BotTriggerDone");

            migrationBuilder.DropTable(
                name: "BotTriggerTimes");

            migrationBuilder.DropTable(
                name: "BotUnits");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "Clocks");

            migrationBuilder.DropTable(
                name: "CompanyCompliances");

            migrationBuilder.DropTable(
                name: "Fines");

            migrationBuilder.DropTable(
                name: "GovernmentIssued");

            migrationBuilder.DropTable(
                name: "GovernmentReserves");

            migrationBuilder.DropTable(
                name: "ImplementedAbatements");

            migrationBuilder.DropTable(
                name: "JournalTrades");

            migrationBuilder.DropTable(
                name: "LedgerAvailable");

            migrationBuilder.DropTable(
                name: "LedgerEscrowed");

            migrationBuilder.DropTable(
                name: "LedgerGrantedYears");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "OtcOffers");

            migrationBuilder.DropTable(
                name: "ReconciledCompanyYears");

            migrationBuilder.DropTable(
                name: "UnitAllocationYears");

            migrationBuilder.DropTable(
                name: "UnitShutdowns");

            migrationBuilder.DropTable(
                name: "AuctionResults");

            migrationBuilder.DropTable(
                name: "Parameters");

            migrationBuilder.DropTable(
                name: "OrderBooks");

            migrationBuilder.DropTable(
                name: "Bots");

            migrationBuilder.DropTable(
                name: "Government");

            migrationBuilder.DropTable(
                name: "UnitAllocations");

            migrationBuilder.DropTable(
                name: "Auctions");

            migrationBuilder.DropTable(
                name: "TradingSystems");

            migrationBuilder.DropTable(
                name: "Exchanges");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Sectors");

            migrationBuilder.DropTable(
                name: "Simulations");
        }
    }
}
