using CarbonSim.Engine.Snapshot;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Data.Persistence;

/// <summary>
/// The saved runs, over the application database. Saving replaces the whole graph in one
/// transaction: the tables cascade from the root row, so a re-save is a delete and an insert
/// rather than a diff of forty tables.
/// </summary>
public sealed class EfSimulationRepository(CarbonSimDbContext context) : ISimulationRepository
{
    private readonly CarbonSimDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task SaveAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SavedRun run = SimulationMapper.ToSavedRun(snapshot);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        SimulationRecord? existing = await _context
            .Simulations.SingleOrDefaultAsync(saved => saved.SimulationId == run.Root.SimulationId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _context.Simulations.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        Add(run);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SimulationSnapshot?> LoadAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        SimulationRecord? root = await _context
            .Simulations.AsNoTracking()
            .SingleOrDefaultAsync(saved => saved.SimulationId == simulationId, cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return null;
        }

        SavedRun run = new()
        {
            Root = root,
            TradingSystems = await _context.TradingSystems.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Parameters = await _context.SimulationParameters.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BausGrowth = await _context.BausGrowthBands.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Sectors = await _context.Sectors.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AbatementMenu = await _context.AbatementMenu.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Players = await _context.Players.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Companies = await _context.Companies.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Units = await _context.Units.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AbatementOptions = await _context.AbatementOptions.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AllocationYears = await _context.AllocationYears.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            UnitAllocations = await _context.UnitAllocations.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            UnitAllocationYears = await _context.UnitAllocationYears.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            ImplementedAbatements = await _context.ImplementedAbatements.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Shutdowns = await _context.UnitShutdowns.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            LedgerAvailable = await _context.LedgerAvailable.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            LedgerEscrowed = await _context.LedgerEscrowed.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            LedgerGrantedYears = await _context.LedgerGrantedYears.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Government = await _context.Government.AsNoTracking().SingleOrDefaultAsync(row => row.SimulationId == simulationId, cancellationToken).ConfigureAwait(false),
            GovernmentReserves = await _context.GovernmentReserves.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            GovernmentIssued = await _context.GovernmentIssued.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            CashMovements = await _context.CashMovements.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Compliances = await _context.CompanyCompliances.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Fines = await _context.Fines.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Reconciled = await _context.ReconciledCompanyYears.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            JournalTrades = await _context.JournalTrades.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Exchange = await _context.Exchanges.AsNoTracking().SingleOrDefaultAsync(row => row.SimulationId == simulationId, cancellationToken).ConfigureAwait(false),
            OrderBooks = await _context.OrderBooks.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Orders = await _context.Orders.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BookTrades = await _context.BookTrades.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            OtcOffers = await _context.OtcOffers.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Auctions = await _context.Auctions.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AuctionLots = await _context.AuctionLots.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AuctionBids = await _context.AuctionBids.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AuctionResults = await _context.AuctionResults.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AuctionAwards = await _context.AuctionAwards.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            AuctionRejectedBids = await _context.AuctionRejectedBids.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            Clock = await _context.Clocks.AsNoTracking().SingleOrDefaultAsync(row => row.SimulationId == simulationId, cancellationToken).ConfigureAwait(false),
            Bots = await _context.Bots.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BotUnits = await _context.BotUnits.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BotTriggerTimes = await _context.BotTriggerTimes.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BotTriggerDone = await _context.BotTriggerDone.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
            BotBidSections = await _context.BotBidSections.AsNoTracking().Where(row => row.SimulationId == simulationId).ToListAsync(cancellationToken).ConfigureAwait(false),
        };

        return SimulationMapper.ToSnapshot(run);
    }

    public async Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context
            .Simulations.AsNoTracking()
            .OrderBy(saved => saved.Name)
            .Select(saved => new SimulationSummary(saved.SimulationId, saved.Name, saved.Seed, saved.State, saved.CurrentYear))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        SimulationRecord? existing = await _context
            .Simulations.SingleOrDefaultAsync(saved => saved.SimulationId == simulationId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        _context.Simulations.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Adds every table of a run at once. The identifiers are all set by the mapper, so the
    /// order these are added in does not matter: the database orders the inserts by foreign key.
    /// </summary>
    private void Add(SavedRun run)
    {
        _context.Simulations.Add(run.Root);
        _context.TradingSystems.AddRange(run.TradingSystems);
        _context.SimulationParameters.AddRange(run.Parameters);
        _context.BausGrowthBands.AddRange(run.BausGrowth);
        _context.Sectors.AddRange(run.Sectors);
        _context.AbatementMenu.AddRange(run.AbatementMenu);
        _context.Players.AddRange(run.Players);
        _context.Companies.AddRange(run.Companies);
        _context.Units.AddRange(run.Units);
        _context.AbatementOptions.AddRange(run.AbatementOptions);
        _context.AllocationYears.AddRange(run.AllocationYears);
        _context.UnitAllocations.AddRange(run.UnitAllocations);
        _context.UnitAllocationYears.AddRange(run.UnitAllocationYears);
        _context.ImplementedAbatements.AddRange(run.ImplementedAbatements);
        _context.UnitShutdowns.AddRange(run.Shutdowns);
        _context.LedgerAvailable.AddRange(run.LedgerAvailable);
        _context.LedgerEscrowed.AddRange(run.LedgerEscrowed);
        _context.LedgerGrantedYears.AddRange(run.LedgerGrantedYears);

        if (run.Government is not null)
        {
            _context.Government.Add(run.Government);
        }

        _context.GovernmentReserves.AddRange(run.GovernmentReserves);
        _context.GovernmentIssued.AddRange(run.GovernmentIssued);
        _context.CashMovements.AddRange(run.CashMovements);
        _context.CompanyCompliances.AddRange(run.Compliances);
        _context.Fines.AddRange(run.Fines);
        _context.ReconciledCompanyYears.AddRange(run.Reconciled);
        _context.JournalTrades.AddRange(run.JournalTrades);

        if (run.Exchange is not null)
        {
            _context.Exchanges.Add(run.Exchange);
        }

        _context.OrderBooks.AddRange(run.OrderBooks);
        _context.Orders.AddRange(run.Orders);
        _context.BookTrades.AddRange(run.BookTrades);
        _context.OtcOffers.AddRange(run.OtcOffers);
        _context.Auctions.AddRange(run.Auctions);
        _context.AuctionLots.AddRange(run.AuctionLots);
        _context.AuctionBids.AddRange(run.AuctionBids);
        _context.AuctionResults.AddRange(run.AuctionResults);
        _context.AuctionAwards.AddRange(run.AuctionAwards);
        _context.AuctionRejectedBids.AddRange(run.AuctionRejectedBids);

        if (run.Clock is not null)
        {
            _context.Clocks.Add(run.Clock);
        }

        _context.Bots.AddRange(run.Bots);
        _context.BotUnits.AddRange(run.BotUnits);
        _context.BotTriggerTimes.AddRange(run.BotTriggerTimes);
        _context.BotTriggerDone.AddRange(run.BotTriggerDone);
        _context.BotBidSections.AddRange(run.BotBidSections);
    }
}
