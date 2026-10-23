using Bunit;
using CarbonSim.Engine.Finance;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The end-of-year company report (catalog `EndOfYearCompanyReport`): the capital each year opened
/// with, every profit-and-loss line, and the capital it closed with, a column per year. The player sees
/// their own company's on Company Management; the trainer picks any company on the reports page.
/// </summary>
public sealed class CompanyReportScreensTests : ScreenTestContext
{
    private static readonly CompanyYearReport Year1 = new(1, 1_000_000m, 400_000m, -10_000m, -50_000m, -100_000m, -25_000m, 30_000m, 0m, -7_000m, 0m, 1_238_000m);

    [Fact]
    public void Company_management_shows_the_companys_own_report_a_column_per_year()
    {
        View = PlayerViewFixture.Sample(SimulationId) with { CompanyReports = [Year1, Year1 with { Year = 2, CapitalAtStart = 1_238_000m, CapitalAtEnd = 1_300_000m }] };

        IRenderedComponent<CompanyManagement> page = RenderComponent<CompanyManagement>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        string report = page.Find("table.company-report").TextContent;

        report.Should().Contain(Localizer(Services)["CapitalOnHandStartOfThisYear"].Value);
        report.Should().Contain(Localizer(Services)["LoanInterest"].Value);
        report.Should().Contain("1,238,000").And.Contain("1,300,000");
        page.FindAll("table.company-report thead th").Should().HaveCount(3, "a label column and one per year");
    }

    [Fact]
    public void The_console_shows_the_report_of_the_company_it_picks()
    {
        AdminRunDetail detail = AdminViewFixture.Detail(SimulationId, "YearEnded") with
        {
            CompanyReports =
            [
                new AdminCompanyReportView("Delta Power", [Year1]),
                new AdminCompanyReportView("Red River Power", [Year1 with { OtherFines = -99_000m }]),
            ],
        };
        AdminSnapshot snapshot = AdminViewFixture.Sample(SimulationId) with { Run = detail };

        IRenderedComponent<AdminReports> page = RenderComponent<AdminReports>(parameters => parameters
            .AddCascadingValue(snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Find("table.company-report").TextContent.Should().NotContain("99,000");
        page.Find("#admin-company-report-company").Change("Red River Power");
        page.Find("table.company-report").TextContent.Should().Contain("99,000");
    }
}
