using System.Globalization;
using System.Text;
using CarbonSim.Web.Player;

namespace CarbonSim.Web.Admin;

/// <summary>
/// Writes the console's reports as CSV. Each report is built from the same <see cref="AdminRunDetail"/>
/// the screen renders, so an exported file and the table it sits beside never disagree. Numbers are
/// written invariantly, because a spreadsheet reading a thousand-separated cell would treat it as text.
/// </summary>
public static class AdminCsv
{
    /// <summary>The report named by <paramref name="report"/>, or null when there is no such report.</summary>
    public static string? For(string report, AdminRunDetail run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return report.Trim().ToLowerInvariant() switch
        {
            AdminReport.System => System(run),
            AdminReport.Companies => Companies(run),
            AdminReport.Units => Units(run),
            AdminReport.Prices => Prices(run),
            AdminReport.Leaderboard => Leaderboard(run),
            _ => null,
        };
    }

    private static string System(AdminRunDetail run)
    {
        StringBuilder csv = new();
        Row(csv, "Metric", "This year", "To date");

        SystemTotalsView now = run.ThisYear;
        SystemTotalsView all = run.ToDate;

        Row(csv, "Cap", Number(now.Cap), Number(all.Cap));
        Row(csv, "Free allocation", Number(now.FreeAllocation), Number(all.FreeAllocation));
        Row(csv, "Allowances on offer", Number(now.AuctionableVolume), Number(all.AuctionableVolume));
        Row(csv, "Forecast emissions", Number(now.ForecastEmissions), Number(all.ForecastEmissions));
        Row(csv, "Allowances surrendered", Number(now.AllowancesSurrendered), Number(all.AllowancesSurrendered));
        Row(csv, "Offsets surrendered", Number(now.OffsetsSurrendered), Number(all.OffsetsSurrendered));
        Row(csv, "Banked", Number(now.Banked), Number(all.Banked));
        Row(csv, "Forfeited", Number(now.Forfeited), Number(all.Forfeited));
        Row(csv, "Auction volume", Number(now.AuctionVolume), Number(all.AuctionVolume));
        Row(csv, "Auction revenue", Number(now.AuctionRevenue), Number(all.AuctionRevenue));
        Row(csv, "Average allowance price", Number(now.AverageAllowancePrice), Number(all.AverageAllowancePrice));
        Row(csv, "Average offset price", Number(now.AverageOffsetPrice), Number(all.AverageOffsetPrice));
        Row(csv, "Abatement implemented", now.AbatementsImplemented.ToString(CultureInfo.InvariantCulture), all.AbatementsImplemented.ToString(CultureInfo.InvariantCulture));
        Row(csv, "Emissions reduced", Number(now.EmissionsReduced), Number(all.EmissionsReduced));
        Row(csv, "Penalties", now.PenaltyCount.ToString(CultureInfo.InvariantCulture), all.PenaltyCount.ToString(CultureInfo.InvariantCulture));
        Row(csv, "Penalty value", Number(now.PenaltyValue), Number(all.PenaltyValue));
        Row(csv, "Fines", Number(now.FineValue), Number(all.FineValue));

        return csv.ToString();
    }

    private static string Companies(AdminRunDetail run)
    {
        StringBuilder csv = new();
        Row(csv, "Company", "Sector", "Player", "Automated", "Capital", "Emissions", "Obligation", "Free allocation", "Shortfall", "Cost of compliance", "Marginal cost", "Final position", "Reconciled", "Penalty");

        foreach (AdminCompanyView company in run.Companies)
        {
            Row(
                csv,
                company.Name,
                company.Sector,
                company.Owner,
                Yes(company.Automated),
                Number(company.Capital),
                Number(company.Emissions),
                Number(company.Obligation),
                Number(company.FreeAllocation),
                Number(company.Shortfall),
                Number(company.CostOfCompliance),
                Number(company.MarginalCost),
                Number(company.FinalPosition),
                Yes(company.Reconciled),
                Number(company.PenaltyCash));
        }

        return csv.ToString();
    }

    private static string Units(AdminRunDetail run)
    {
        StringBuilder csv = new();
        Row(csv, "Unit", "Company", "Sector", "Baseline emissions", "Free allocation", "Forecast emissions", "Reduction", "Auto trade", "Shut down", "Abatement implemented");

        foreach (AdminUnitView unit in run.Units)
        {
            Row(
                csv,
                unit.Name,
                unit.Company,
                unit.Sector,
                Number(unit.BaselineEmissions),
                Number(unit.FreeAllocation),
                Number(unit.ForecastEmissions),
                Number(unit.Reduction),
                Yes(unit.AutoTrade),
                Yes(unit.ShutDown),
                unit.ImplementedCount.ToString(CultureInfo.InvariantCulture));
        }

        return csv.ToString();
    }

    private static string Prices(AdminRunDetail run)
    {
        StringBuilder csv = new();
        Row(csv, "Year", "Allowance volume", "Average allowance price", "Offset volume", "Average offset price", "Average auction price", "Lowest auction price", "Highest auction price", "Trades");

        foreach (AdminPriceView price in run.Prices)
        {
            Row(
                csv,
                price.Year.ToString(CultureInfo.InvariantCulture),
                Number(price.AllowanceVolume),
                Number(price.AllowanceAverage),
                Number(price.OffsetVolume),
                Number(price.OffsetAverage),
                Optional(price.AuctionAverage),
                Optional(price.AuctionLow),
                Optional(price.AuctionHigh),
                price.Trades.ToString(CultureInfo.InvariantCulture));
        }

        return csv.ToString();
    }

    private static string Leaderboard(AdminRunDetail run)
    {
        StringBuilder csv = new();
        Row(csv, "Rank", "Company", "Unit", "Player", "Marginal cost", "Final position", "Automated");

        foreach (AdminLeaderView row in run.Leaderboard)
        {
            Row(
                csv,
                row.Rank.ToString(CultureInfo.InvariantCulture),
                row.Company,
                row.Unit,
                row.Player,
                Number(row.MarginalCost),
                Number(row.FinalPosition),
                Yes(row.Automated));
        }

        return csv.ToString();
    }

    private static void Row(StringBuilder csv, params string[] cells)
    {
        for (int index = 0; index < cells.Length; index++)
        {
            if (index > 0)
            {
                csv.Append(',');
            }

            csv.Append(Escape(cells[index]));
        }

        csv.Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Optional(decimal? value) => value is null ? string.Empty : Number(value.Value);

    private static string Yes(bool value) => value ? "yes" : "no";
}
