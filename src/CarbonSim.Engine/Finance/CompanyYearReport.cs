namespace CarbonSim.Engine.Finance;

/// <summary>
/// One company's year in money, as the original's end-of-year company report reads it: the capital
/// the year opened with, each line of profit and loss, and the capital it closed with. Money in is
/// positive and money out negative, so the lines add up to the change in capital.
/// </summary>
public sealed record CompanyYearReport(
    int Year,
    decimal CapitalAtStart,
    decimal OperatingProfit,
    decimal AbatementNetRevenue,
    decimal AbatementCapital,
    decimal AuctionPurchases,
    decimal SecondaryMarketPurchases,
    decimal InstrumentSales,
    decimal CompliancePenalties,
    decimal OtherFines,
    decimal LoanInterest,
    decimal CapitalAtEnd)
{
    /// <summary>What buying, selling and failing to comply cost the company in the year.</summary>
    public decimal ComplianceNet => AuctionPurchases + SecondaryMarketPurchases + InstrumentSales + CompliancePenalties;

    public decimal ChangeInCapital => CapitalAtEnd - CapitalAtStart;
}
