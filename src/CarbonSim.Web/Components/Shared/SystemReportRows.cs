using System.Globalization;
using CarbonSim.Engine.Reporting;
using CarbonSim.Web.Resources;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Components.Shared;

/// <summary>One line of the system report: what it measures, this year and to date.</summary>
public sealed record SystemReportRow(string Label, string ThisYear, string ToDate);

/// <summary>
/// The system report's rows, in the original's order and on its figures: allowances sold by the
/// government and in the secondary market apart, abatement undertaken in tonnes, and the number and
/// value of penalties as two lines. The player's System info and the trainer's Reports read the same
/// rows, so the two screens cannot disagree about what the room has done.
/// </summary>
public static class SystemReportRows
{
    public static IReadOnlyList<SystemReportRow> For(IStringLocalizer<SharedStrings> strings, SystemTotals now, SystemTotals all)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(all);

        return
        [
            Tonnes(strings["Cap"], now.Cap, all.Cap),
            Tonnes(strings["FreeAllowancesThisYear"], now.FreeAllocation, all.FreeAllocation),
            Tonnes(strings["TotalAllowancesOnOffer"], now.AuctionableVolume, all.AuctionableVolume),
            Tonnes(strings["ForecastEmissions"], now.ForecastEmissions, all.ForecastEmissions),
            Tonnes(strings["AllowancesSurrenderedToGovernment"], now.AllowancesSurrendered, all.AllowancesSurrendered),
            Tonnes(strings["OffsetsSurrenderedToGovernment"], now.OffsetsSurrendered, all.OffsetsSurrendered),
            Tonnes(strings["AllowancesBankedForTheNextYear"], now.Banked, all.Banked),
            Tonnes(strings["Forfeited"], now.Forfeited, all.Forfeited),
            Tonnes(strings["AllowancesSoldByGovernment"], now.AuctionVolume, all.AuctionVolume),
            Tonnes(strings["AllowancesSoldInSecondaryMarket"], now.AllowancesSold, all.AllowancesSold),
            new(strings["AuctionRevenueCollectedByGovernment"], Display.Money(now.AuctionRevenue), Display.Money(all.AuctionRevenue)),
            new(strings["AverageAllowancePriceAllChannels"], Display.Price(now.AverageAllowancePrice), Display.Price(all.AverageAllowancePrice)),
            new(strings["AuctionAveragePrice"], Display.Price(now.AuctionAveragePrice), Display.Price(all.AuctionAveragePrice)),
            new(strings["Average"] + " " + strings["Offset"], Display.Price(now.AverageOffsetPrice), Display.Price(all.AverageOffsetPrice)),
            Tonnes(strings["AbatementUndertaken"], now.AbatementTonnes, all.AbatementTonnes),
            Tonnes(strings["EmissionReductions"], now.EmissionsReduced, all.EmissionsReduced),
            new(
                strings["NumberOfCompliancePenaltiesApplied"],
                now.PenaltyCount.ToString(CultureInfo.CurrentCulture),
                all.PenaltyCount.ToString(CultureInfo.CurrentCulture)),
            new(strings["ValueOfGovtPenaltiesApplied"], Display.Money(now.PenaltyValue), Display.Money(all.PenaltyValue)),
        ];
    }

    private static SystemReportRow Tonnes(string label, decimal thisYear, decimal toDate) =>
        new(label, Display.Tonnes(thisYear), Display.Tonnes(toDate));
}
