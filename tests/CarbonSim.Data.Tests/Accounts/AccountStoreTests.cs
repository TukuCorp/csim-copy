using CarbonSim.Data.Accounts;
using FluentAssertions;

namespace CarbonSim.Data.Tests.Accounts;

/// <summary>
/// The account table against a real SQLite database built from the migrations, so the two
/// uniqueness rules are tested where they are actually enforced.
/// </summary>
public sealed class AccountStoreTests
{
    [Fact]
    public async Task Addresses_are_stored_normalised_and_found_regardless_of_case()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        PlayerAccount? created = await store.CreateAsync("  Trainer@Example.COM ", "Trainer", "hash-1", AccountRole.Administrator);

        created.Should().NotBeNull();
        created!.Email.Should().Be("trainer@example.com");

        PlayerAccount? found = await store.FindAsync("TRAINER@example.com");

        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Registering_the_same_address_twice_is_refused()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        await store.CreateAsync("player@example.com", "First", "hash-1", AccountRole.Player, "Công ty Điện lực Bình Minh");
        PlayerAccount? second = await store.CreateAsync("PLAYER@example.com", "Second", "hash-2", AccountRole.Player, "Nhà máy Nhiệt điện Đông Xuân");

        second.Should().BeNull("an address may only be registered once");
        (await store.ListAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_company_can_only_be_claimed_by_one_account()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        await store.CreateAsync("first@example.com", "First", "hash-1", AccountRole.Player, "Công ty Điện lực Bình Minh");

        (await store.IsCompanyTakenAsync("Công ty Điện lực Bình Minh")).Should().BeTrue();
        (await store.IsCompanyTakenAsync("Công ty Điện lực Hồng Hà")).Should().BeFalse();

        PlayerAccount? second = await store.CreateAsync("second@example.com", "Second", "hash-2", AccountRole.Player, "Công ty Điện lực Bình Minh");

        second.Should().BeNull("a company is played by one person");
    }

    [Fact]
    public async Task Claims_racing_for_one_company_give_one_account_and_refuse_the_rest()
    {
        using SqliteTestDatabase database = new();

        // Each claim on its own context, as each request has its own; all of them pass the "is it
        // taken" check before any insert lands, so the unique index is what decides.
        // SQLite answers synchronously, so each claim gets a thread of its own to overlap at all.
        PlayerAccount?[] claims = await Task.WhenAll(Enumerable.Range(1, 16).Select(index => Task.Run(async () =>
        {
            await using CarbonSimDbContext context = database.CreateContext();
            EfAccountStore store = new(context);

            return await store.CreateAsync($"racer{index}@example.com", $"Racer {index}", "hash", AccountRole.Player, "Công ty Điện lực Bình Minh");
        })));

        claims.Should().ContainSingle(claim => claim != null, "a company is played by one person");
    }

    [Fact]
    public async Task Reset_attempts_racing_each_other_cannot_share_a_try()
    {
        using SqliteTestDatabase database = new();
        int accountId;

        await using (CarbonSimDbContext context = database.CreateContext())
        {
            EfAccountStore store = new(context);
            accountId = (await store.CreateAsync("player@example.com", "Player", "hash-1", AccountRole.Player))!.Id;
            await store.SetResetCodeAsync(accountId, "code-hash", DateTimeOffset.UtcNow.AddMinutes(15));
        }

        int?[] tries = await Task.WhenAll(Enumerable.Range(1, 16).Select(_ => Task.Run(async () =>
        {
            await using CarbonSimDbContext context = database.CreateContext();
            return await new EfAccountStore(context).ClaimResetAttemptAsync(accountId, maxAttempts: 5);
        })));

        tries.Count(attempt => attempt != null).Should().Be(5, "a lost or shared try is a free guess at the code");

        await using CarbonSimDbContext reader = database.CreateContext();
        (await new EfAccountStore(reader).FindByIdAsync(accountId))!.ResetCodeAttempts.Should().Be(5);
    }

    [Fact]
    public async Task An_open_reset_records_its_code_its_expiry_and_its_failed_attempts()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        PlayerAccount account = (await store.CreateAsync("player@example.com", "Player", "hash-1", AccountRole.Player))!;
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(15);

        (await store.SetResetCodeAsync(account.Id, "code-hash", expires)).Should().BeTrue();

        PlayerAccount? afterSet = await store.FindByIdAsync(account.Id);
        afterSet!.ResetCodeHash.Should().Be("code-hash");
        afterSet.ResetCodeExpiresAtUtc.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
        afterSet.ResetCodeAttempts.Should().Be(0);

        (await store.ClaimResetAttemptAsync(account.Id, maxAttempts: 2)).Should().Be(1);
        (await store.ClaimResetAttemptAsync(account.Id, maxAttempts: 2)).Should().Be(2);
        (await store.ClaimResetAttemptAsync(account.Id, maxAttempts: 2)).Should().BeNull("the reset has no tries left");
    }

    [Fact]
    public async Task Changing_the_password_closes_any_open_reset()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        PlayerAccount account = (await store.CreateAsync("player@example.com", "Player", "hash-1", AccountRole.Player))!;
        await store.SetResetCodeAsync(account.Id, "code-hash", DateTimeOffset.UtcNow.AddMinutes(15));
        await store.ClaimResetAttemptAsync(account.Id, maxAttempts: 5);

        (await store.SetPasswordAsync(account.Id, "hash-2")).Should().BeTrue();

        PlayerAccount? afterChange = await store.FindByIdAsync(account.Id);
        afterChange!.PasswordHash.Should().Be("hash-2");
        afterChange.ResetCodeHash.Should().BeNull("a used code must not be replayable");
        afterChange.ResetCodeExpiresAtUtc.Should().BeNull();
        afterChange.ResetCodeAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Accounts_are_listed_in_the_order_they_registered()
    {
        using SqliteTestDatabase database = new();
        await using CarbonSimDbContext context = database.CreateContext();
        EfAccountStore store = new(context);

        await store.CreateAsync("first@example.com", "First", "hash-1", AccountRole.Administrator);
        await store.CreateAsync("second@example.com", "Second", "hash-2", AccountRole.Player, "Công ty Điện lực Bình Minh");
        await store.CreateAsync("third@example.com", "Third", "hash-3", AccountRole.Player, "Nhà máy Nhiệt điện Đông Xuân");

        IReadOnlyList<PlayerAccount> accounts = await store.ListAsync();

        accounts.Select(account => account.DisplayName).Should().ContainInOrder("First", "Second", "Third");
    }
}
