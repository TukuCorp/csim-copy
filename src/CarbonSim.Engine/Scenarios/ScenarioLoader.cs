using System.Globalization;
using System.Text.Json;
using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Scenarios;

/// <summary>
/// Turns a scenario file into a ready-to-run <see cref="Simulation"/>. Loading validates
/// everything it can before building anything, so a file with several mistakes reports all
/// of them at once.
/// </summary>
public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Simulation LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string sourceName = Path.GetFileName(path);

        if (!File.Exists(path))
        {
            throw new ScenarioLoadException(sourceName, [$"the scenario file '{path}' does not exist."]);
        }

        return LoadJson(File.ReadAllText(path), sourceName);
    }

    public static Simulation LoadJson(string json, string sourceName = "scenario")
    {
        ArgumentNullException.ThrowIfNull(json);

        ScenarioFile file = Parse(json, sourceName);
        List<string> errors = [];

        string? name = string.IsNullOrWhiteSpace(file.Name) ? null : file.Name.Trim();

        if (name is null)
        {
            errors.Add("name is required.");
        }

        Parameters? parameters = ReadParameters(file.Parameters, errors);
        IReadOnlyList<Sector> sectors = ReadSectors(file.Sectors, errors);
        IReadOnlyList<CompanyTemplate> companies = ReadCompanies(file.Companies, sectors, errors);

        if (parameters is not null)
        {
            CheckGrowthCoverage(parameters, sectors, errors);
        }

        if (errors.Count > 0)
        {
            throw new ScenarioLoadException(sourceName, errors);
        }

        return Build(name!, file.Seed ?? 0UL, parameters!, sectors, companies);
    }

    private static ScenarioFile Parse(string json, string sourceName)
    {
        ScenarioFile? file;

        try
        {
            file = JsonSerializer.Deserialize<ScenarioFile>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new ScenarioLoadException(sourceName, [$"{sourceName} is not valid JSON: {exception.Message}"]);
        }

        return file ?? throw new ScenarioLoadException(sourceName, [$"{sourceName} contains no scenario."]);
    }

    private static Parameters? ReadParameters(ParametersFile? file, List<string> errors)
    {
        const string Prefix = "parameters";

        if (file is null)
        {
            errors.Add($"{Prefix}: the parameters block is required.");
            return null;
        }

        List<string> problems = [];
        Require(problems, file.Cap, "cap");
        Require(problems, file.AnnualCapReductionRate, "annualCapReductionRate");
        Require(problems, file.FreeAllocationShare, "freeAllocationShare");
        Require(problems, file.Years, "years");
        Require(problems, file.OffsetUsageLimit, "offsetUsageLimit");
        Require(problems, file.BankingLimit, "bankingLimit");
        Require(problems, file.PenaltyPerTonne, "penaltyPerTonne");
        Require(problems, file.PenaltyAllowanceDebit, "penaltyAllowanceDebit");
        Require(problems, file.AuctionFloorPrice, "auctionFloorPrice");
        Require(problems, file.AuctionCeilingPrice, "auctionCeilingPrice");
        Require(problems, file.AuctionsPerYear, "auctionsPerYear");
        Require(problems, file.YearLengthMinutes, "yearLengthMinutes");
        Require(problems, file.AuctionDurationMinutes, "auctionDurationMinutes");
        Require(problems, file.TradingOpenShareOfYear, "tradingOpenShareOfYear");
        Require(problems, file.VolatilityBand, "volatilityBand");
        Require(problems, file.OverdraftInterestRate, "overdraftInterestRate");

        List<SectorBausGrowth> growth = [];

        if (file.BausGrowthBySector is null || file.BausGrowthBySector.Count == 0)
        {
            problems.Add("bausGrowthBySector must give a growth band for at least one sector.");
        }
        else
        {
            for (int index = 0; index < file.BausGrowthBySector.Count; index++)
            {
                BausGrowthFile entry = file.BausGrowthBySector[index];

                if (string.IsNullOrWhiteSpace(entry.Sector)
                    || entry.MinAnnualRate is null
                    || entry.MaxAnnualRate is null)
                {
                    problems.Add($"bausGrowthBySector[{index}] needs sector, minAnnualRate and maxAnnualRate.");
                    continue;
                }

                growth.Add(new SectorBausGrowth(entry.Sector.Trim(), entry.MinAnnualRate.Value, entry.MaxAnnualRate.Value));
            }
        }

        if (problems.Count > 0)
        {
            errors.AddRange(problems.Select(problem => $"{Prefix}: {problem}"));
            return null;
        }

        Parameters parameters = new()
        {
            Cap = file.Cap!.Value,
            AnnualCapReductionRate = file.AnnualCapReductionRate!.Value,
            FreeAllocationShare = file.FreeAllocationShare!.Value,
            Years = file.Years!.Value,
            BausGrowthBySector = growth,
            OffsetUsageLimit = file.OffsetUsageLimit!.Value,
            BankingLimit = file.BankingLimit!.Value,
            PenaltyPerTonne = file.PenaltyPerTonne!.Value,
            PenaltyAllowanceDebit = file.PenaltyAllowanceDebit!.Value,
            AuctionFloorPrice = file.AuctionFloorPrice!.Value,
            AuctionCeilingPrice = file.AuctionCeilingPrice!.Value,
            AuctionsPerYear = file.AuctionsPerYear!.Value,
            YearLength = TimeSpan.FromMinutes((double)file.YearLengthMinutes!.Value),
            AuctionDuration = TimeSpan.FromMinutes((double)file.AuctionDurationMinutes!.Value),
            TradingOpenShareOfYear = file.TradingOpenShareOfYear!.Value,
            VolatilityBand = file.VolatilityBand!.Value,
            OverdraftInterestRate = file.OverdraftInterestRate!.Value,
        };

        errors.AddRange(parameters.Validate().Select(error => $"{Prefix}: {error}"));

        return parameters;
    }

    private static IReadOnlyList<Sector> ReadSectors(List<SectorFile>? files, List<string> errors)
    {
        List<Sector> sectors = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        decimal shareTotal = 0m;
        bool sharesKnown = true;

        if (files is null || files.Count == 0)
        {
            errors.Add("sectors: at least one sector is required.");
            return sectors;
        }

        for (int index = 0; index < files.Count; index++)
        {
            SectorFile file = files[index];
            string path = $"sectors[{index}]";
            string? name = string.IsNullOrWhiteSpace(file.Name) ? null : file.Name.Trim();

            if (name is null)
            {
                errors.Add($"{path}: a sector needs a name.");
                continue;
            }

            path = $"{path} '{name}'";

            if (file.EmissionShare is null)
            {
                errors.Add($"{path}: emissionShare is required.");
                sharesKnown = false;
                continue;
            }

            if (file.EmissionShare is < 0m or > 1m)
            {
                errors.Add($"{path}: emissionShare must be between 0 and 1 (was {Invariant(file.EmissionShare.Value)}).");
                sharesKnown = false;
                continue;
            }

            shareTotal += file.EmissionShare.Value;

            if (!names.Add(name))
            {
                errors.Add($"{path}: this sector name is used more than once.");
                continue;
            }

            if (file.AbatementOptions is null || file.AbatementOptions.Count == 0)
            {
                errors.Add($"{path}: at least one abatement option is required.");
                continue;
            }

            sectors.Add(new Sector(name, file.EmissionShare.Value, ReadAbatementOptions(file.AbatementOptions, path, errors)));
        }

        if (sharesKnown && names.Count > 0 && shareTotal != 1m)
        {
            errors.Add($"sectors: emission shares must sum to 1 (they sum to {Invariant(shareTotal)}).");
        }

        return sectors;
    }

    /// <summary>Validates a sector's abatement menu and returns the entries that are usable.</summary>
    private static List<AbatementMenu> ReadAbatementOptions(
        List<AbatementOptionFile> files,
        string sectorPath,
        List<string> errors)
    {
        HashSet<string> codes = new(StringComparer.Ordinal);
        List<AbatementMenu> menu = [];

        for (int index = 0; index < files.Count; index++)
        {
            AbatementOptionFile file = files[index];
            string path = $"{sectorPath} abatementOptions[{index}]";
            string? code = string.IsNullOrWhiteSpace(file.Code) ? null : file.Code.Trim();

            if (code is null)
            {
                errors.Add($"{path}: an abatement option needs a code.");
                continue;
            }

            path = $"{path} '{code}'";

            if (!codes.Add(code))
            {
                errors.Add($"{path}: this option code is used more than once in the sector.");
                continue;
            }

            bool entryOk = true;

            if (string.IsNullOrWhiteSpace(file.Name))
            {
                errors.Add($"{path}: name is required.");
                entryOk = false;
            }

            if (file.AnnualReductionShare is null or <= 0m or > 1m)
            {
                errors.Add($"{path}: annualReductionShare must be greater than 0 and at most 1.");
                entryOk = false;
            }

            if (file.UpfrontCostPerTonne is null or < 0m)
            {
                errors.Add($"{path}: upfrontCostPerTonne is required and must not be negative.");
                entryOk = false;
            }

            if (file.AnnualNetRevenuePerTonne is null)
            {
                errors.Add($"{path}: annualNetRevenuePerTonne is required.");
                entryOk = false;
            }

            if (file.ImplementationYears is null or < 0)
            {
                errors.Add($"{path}: implementationYears is required and must not be negative.");
                entryOk = false;
            }

            if (file.LifetimeYears is null or < 1)
            {
                errors.Add($"{path}: lifetimeYears is required and must be at least 1.");
                entryOk = false;
            }

            if (!entryOk)
            {
                continue;
            }

            menu.Add(new AbatementMenu(
                code,
                file.Name!,
                file.AnnualReductionShare!.Value,
                file.UpfrontCostPerTonne!.Value,
                file.AnnualNetRevenuePerTonne!.Value,
                file.ImplementationYears!.Value,
                file.LifetimeYears!.Value));
        }

        return menu;
    }

    private static IReadOnlyList<CompanyTemplate> ReadCompanies(
        List<CompanyFile>? files,
        IReadOnlyList<Sector> sectors,
        List<string> errors)
    {
        List<CompanyTemplate> companies = [];
        HashSet<string> companyNames = new(StringComparer.Ordinal);
        HashSet<string> unitNames = new(StringComparer.Ordinal);

        if (files is null || files.Count == 0)
        {
            errors.Add("companies: at least one company is required.");
            return companies;
        }

        Dictionary<string, Sector> sectorsByName = sectors.ToDictionary(sector => sector.Name, StringComparer.Ordinal);

        for (int index = 0; index < files.Count; index++)
        {
            CompanyFile file = files[index];
            string path = $"companies[{index}]";
            string? name = string.IsNullOrWhiteSpace(file.Name) ? null : file.Name.Trim();

            if (name is null)
            {
                errors.Add($"{path}: a company needs a name.");
                continue;
            }

            path = $"{path} '{name}'";

            if (!companyNames.Add(name))
            {
                errors.Add($"{path}: this company name is used more than once.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(file.Sector)
                || !sectorsByName.TryGetValue(file.Sector.Trim(), out Sector? sector))
            {
                errors.Add($"{path}: sector '{file.Sector}' is not defined by this scenario.");
                continue;
            }

            if (file.Capital is null or <= 0m)
            {
                errors.Add($"{path}: capital is required and must be greater than zero.");
                continue;
            }

            if (file.OverdraftLimit is null or < 0m)
            {
                errors.Add($"{path}: overdraftLimit is required and must not be negative.");
                continue;
            }

            PlayerKind? ownerKind = file.OwnerKind?.Trim().ToLowerInvariant() switch
            {
                "human" => PlayerKind.Human,
                "ai" => PlayerKind.Ai,
                _ => null,
            };

            if (ownerKind is null)
            {
                errors.Add($"{path}: ownerKind must be 'human' or 'ai' (was '{file.OwnerKind}').");
                continue;
            }

            if (file.Units is null || file.Units.Count == 0)
            {
                errors.Add($"{path}: a company needs at least one unit.");
                continue;
            }

            List<UnitTemplate> units = [];

            for (int unitIndex = 0; unitIndex < file.Units.Count; unitIndex++)
            {
                UnitFile unit = file.Units[unitIndex];
                string unitPath = $"{path} units[{unitIndex}]";
                string? unitName = string.IsNullOrWhiteSpace(unit.Name) ? null : unit.Name.Trim();

                if (unitName is null)
                {
                    errors.Add($"{unitPath}: a unit needs a name.");
                    continue;
                }

                if (!unitNames.Add(unitName))
                {
                    errors.Add($"{unitPath} '{unitName}': this unit name is used more than once.");
                    continue;
                }

                if (unit.BaselineEmissions is null or <= 0m)
                {
                    errors.Add($"{unitPath} '{unitName}': baselineEmissions is required and must be greater than zero.");
                    continue;
                }

                units.Add(new UnitTemplate(
                    unitName,
                    unit.BaselineEmissions.Value,
                    unit.NormalOperatingProfit ?? 0m));
            }

            if (units.Count == 0)
            {
                continue;
            }

            companies.Add(new CompanyTemplate(
                name,
                sector,
                ownerKind.Value,
                file.Capital.Value,
                file.OverdraftLimit.Value,
                units));
        }

        return companies;
    }

    private static void CheckGrowthCoverage(
        Parameters parameters,
        IReadOnlyList<Sector> sectors,
        List<string> errors)
    {
        HashSet<string> covered = new(StringComparer.Ordinal);

        foreach (SectorBausGrowth growth in parameters.BausGrowthBySector)
        {
            covered.Add(growth.Sector);

            if (sectors.All(sector => sector.Name != growth.Sector))
            {
                errors.Add($"parameters: bausGrowthBySector names sector '{growth.Sector}', which this scenario does not define.");
            }
        }

        foreach (Sector sector in sectors.Where(sector => !covered.Contains(sector.Name)))
        {
            errors.Add($"parameters: bausGrowthBySector is missing a growth band for sector '{sector.Name}'.");
        }
    }

    private static Simulation Build(
        string name,
        ulong seed,
        Parameters parameters,
        IReadOnlyList<Sector> sectors,
        IReadOnlyList<CompanyTemplate> companies)
    {
        List<Player> players = [];
        List<Company> companyEntities = [];
        int playerId = 1;
        int companyId = 1;
        int unitId = 1;

        foreach (CompanyTemplate template in companies)
        {
            Player player = new(playerId++, template.Name, template.OwnerKind);
            Company company = new(companyId++, template.Name, template.Sector, player, template.Capital, template.OverdraftLimit);

            foreach (UnitTemplate unitTemplate in template.Units)
            {
                company.AddUnit(new Unit(
                    unitId++,
                    unitTemplate.Name,
                    company,
                    unitTemplate.BaselineEmissions,
                    AbatementMenu.For(template.Sector, unitTemplate.BaselineEmissions),
                    unitTemplate.NormalOperatingProfit));
            }

            players.Add(player);
            companyEntities.Add(company);
        }

        TradingSystem tradingSystem = new(1, name, parameters, sectors, companyEntities);

        return Simulation.Create(name, seed, [tradingSystem], players);
    }

    private static void Require<T>(List<string> problems, T? value, string field)
        where T : struct
    {
        if (value is null)
        {
            problems.Add($"{field} is required.");
        }
    }

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record CompanyTemplate(
        string Name,
        Sector Sector,
        PlayerKind OwnerKind,
        decimal Capital,
        decimal OverdraftLimit,
        IReadOnlyList<UnitTemplate> Units);

    private sealed record UnitTemplate(string Name, decimal BaselineEmissions, decimal NormalOperatingProfit);
}
