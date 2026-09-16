using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Finance;

/// <summary>
/// The company's books. Operating profit comes from its units, abatement adds or subtracts its
/// running revenue, trading and compliance move money through <see cref="CashLedger"/>, and a
/// company that ends a year in the red pays interest on the balance it is carrying.
/// </summary>
public sealed class CompanyFinance
{
    private readonly Simulation _simulation;

    public CompanyFinance(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    /// <summary>Money on hand plus what the credit line still allows.</summary>
    public decimal AvailableCapital(Company company) => _simulation.Cash.Available(company);

    /// <summary>How much of the credit line is left; it shrinks once the company is in the red.</summary>
    public decimal AvailableOverdraft(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        decimal headroom = company.OverdraftLimit + Math.Min(company.Capital, 0m);

        return headroom < 0m ? 0m : headroom;
    }

    /// <summary>What the company's units earn in a year before abatement and compliance.</summary>
    public decimal NormalOperatingProfit(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return company.Units.Sum(unit => unit.NormalOperatingProfit);
    }

    /// <summary>What abatement projects pay or cost the company in a year, once they are operating.</summary>
    public decimal AbatementNetRevenue(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return company.Units
            .SelectMany(unit => _simulation.Abatements.For(unit))
            .Sum(project => project.OperatingRevenueIn(year));
    }

    /// <summary>Operating profit with the abatement result folded in.</summary>
    public decimal NopWithAbatement(Company company, int year) =>
        NormalOperatingProfit(company, year) + AbatementNetRevenue(company, year);

    /// <summary>Everything that happened to the company's cash in a year.</summary>
    public decimal NetRevenue(Company company, int year) => _simulation.Cash.MovementsFor(company, year);

    /// <summary>Interest charged on a negative balance in a year, as a negative amount.</summary>
    public decimal Interest(Company company, int year) =>
        _simulation.Cash.MovementsFor(company, year, CashCategory.Interest);

    /// <summary>
    /// Closes the books on a year: interest first, on the balance the company is carrying, then
    /// the year's operating profit and abatement result are paid in. Charging interest first
    /// means a good year cannot erase what borrowing cost.
    /// </summary>
    public void CloseYear(int year)
    {
        RequireYear(year);

        Parameters parameters = _simulation.TradingSystems.Single().Parameters;

        foreach (Company company in _simulation.Companies)
        {
            if (company.Capital < 0m && parameters.OverdraftInterestRate > 0m)
            {
                decimal interest = -Math.Round(-company.Capital * parameters.OverdraftInterestRate, 2, MidpointRounding.AwayFromZero);
                _simulation.Cash.Charge(company, -interest, CashCategory.Interest, $"Interest on borrowing in year {year}");
            }

            decimal operatingProfit = NormalOperatingProfit(company, year);

            if (operatingProfit > 0m)
            {
                _simulation.Cash.Deposit(company, operatingProfit, CashCategory.OperatingProfit, $"Normal operating profit, year {year}");
            }

            decimal abatementRevenue = AbatementNetRevenue(company, year);

            if (abatementRevenue > 0m)
            {
                _simulation.Cash.Deposit(company, abatementRevenue, CashCategory.AbatementNetRevenue, $"Abatement net revenue, year {year}");
            }
            else if (abatementRevenue < 0m)
            {
                _simulation.Cash.Charge(company, -abatementRevenue, CashCategory.AbatementNetRevenue, $"Abatement net cost, year {year}");
            }
        }
    }

    private void RequireYear(int year)
    {
        if (year < 1 || year > _simulation.Allocation.Years.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                $"This simulation runs for {_simulation.Allocation.Years.Count} years.");
        }
    }
}
