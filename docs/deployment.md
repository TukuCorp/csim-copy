# Deploying CarbonSim

This is the operator guide to running the clone on a single server. It covers the host configuration,
the two databases it can keep its data in, TLS behind a reverse proxy, and how to back up and
restore. The training flow itself is in [trainer-runbook.md](trainer-runbook.md).

## 1. What runs

One ASP.NET Core host serves everything: the player screens (Blazor Server), the administrator
console, the JSON endpoints under /api, and the SignalR hub under /hubs. There is no separate worker
and no external queue; a hosted service drives the simulation clock in-process. The engine
(CarbonSim.Engine) is a plain class library with no web or database dependency, and the database
access (CarbonSim.Data) sits underneath the host.

The host keeps live runs in memory. It writes a run to the database when the exercise is created and
when each year closes, so a crash loses the year in progress and nothing before it. The trainer
loads the saved copy back from the console's **Saved exercises** list and plays that year again.
Plan for a host restart between sessions, not during one.

## 2. Running the host

### The internal demo (one Windows box)

    powershell -File scripts/run-demo.ps1

The script publishes a Release build to publish/, then starts it from that folder in the Demo
environment (src/CarbonSim.Web/appsettings.Demo.json: migrations applied, reset codes written to the
log, timestamped single-line logging, no seeded exercise). Everything the session keeps lives in
demo-data/, which git ignores:

| File | What it is |
|---|---|
| demo-data/carbonsim.sqlite | Accounts, claimed companies and saved exercises |
| demo-data/credentials.local.json | The trainer sign-in and the registration PIN. Generated on the first run; delete it to issue new ones |
| demo-data/logs/host-*.log | One log per start, also echoed to the console |

The banner prints the address, the trainer sign-in and the PIN. Stop the host with Ctrl+C. Start it
again with `-NoPublish` to keep the same build and data. Other switches: `-Port` (default 5080),
`-Bind` (default 127.0.0.1; use 0.0.0.0 to serve the LAN), `-DataDir` and `-PublishDir`.
`Get-Help scripts/run-demo.ps1 -Full` lists them.

tests/CarbonSim.Web.Tests/Components/PublishedDemoTests.cs runs this script end to end: a real
publish, a trainer closing year 1, a player registering with the generated PIN, a killed host, a
restart with -NoPublish, and the exercise loaded back into year 2.

### Development

    dotnet run --project src/CarbonSim.Web

This binds http://localhost:5003 in the Development environment, with a known PIN and admin account
(appsettings.Development.json) and the scenario seeded at boot. Do not share that address: its
credentials are in the repository.

Settings come from appsettings.json, then appsettings.{Environment}.json, then environment
variables. A double underscore separates the levels, for example CarbonSim__ConnectionString.

## 3. Configuration reference

Everything the host reads is under the **CarbonSim** section.

| Setting | Default | What it does |
|---|---|---|
| ConnectionString | Data Source=carbonsim.sqlite | The application database. SQLite takes a file path; PostgreSQL takes a libpq connection string. |
| Provider | Sqlite | Which engine opens the connection string: Sqlite or Postgres. Unknown values stop the host at start-up. |
| Schema | Migrate | Migrate applies the shipped migrations (SQLite only). FromModel builds the schema from the model and keeps no migration history, which is what PostgreSQL uses. |
| ScenarioFile | scenarios/vietnam-2024.json | The scenario whose human companies may be claimed. Absolute, or relative: the host looks under the content root first, then in its own folder, where the build copies the shipped scenarios. |
| RegistrationPin | (empty) | The PIN a visitor must quote to register. Empty closes registration, so a deployment that forgets to set it cannot be registered against. |
| AdminEmail / AdminPassword | (empty) | A bootstrap administrator created at start-up. Empty disables it. Use these to get into the console the first time. |
| EmailSender | Log | How password-reset codes are delivered: Log writes them to the host log, InMemory keeps them in memory for a demo or test. |
| ResetCodeLifetimeMinutes | 15 | How long an emailed reset code stays usable. |
| ResetCodeMaxAttempts | 5 | How many wrong codes an open reset accepts before it closes. |
| DisplayCurrency | USD | What the screens show money in: USD (the engine internal unit) or VND. |
| VndPerUsd | 25000 | The rate used when the display currency is VND. The engine never converts. |
| StartDemoSimulation | false | Development only: start the configured scenario as a live run at boot. Leave off in production. |

The display **culture** (English or Vietnamese) is a per-browser cookie written by the language
switch, not a host setting; the host supports both and defaults to English. The display currency is
one choice per deployment, set above.

Getting in the first time:

1. Set AdminEmail and AdminPassword to a real address and a strong password, and set RegistrationPin.
2. Start the host and sign in at /sign-in with the administrator account.
3. Open /admin, build an exercise from the configured scenario, and start it.
4. Give the room the PIN. Players register at /sign-in and claim a company.

Change the bootstrap password after first use, or set AdminEmail and AdminPassword empty once a real
administrator account exists (the console does not create accounts; seeding one at boot is the way in
on a fresh database).

## 4. Databases

### SQLite (the default, one box)

The whole database is one file. Point ConnectionString at a path on a disk that is backed up, for
example Data Source=/var/lib/carbonsim/carbonsim.sqlite, and leave Schema as Migrate. This is the
right choice for a training laptop or a single room.

### PostgreSQL (a server)

SQLite is the shipped migration set, so PostgreSQL is built from the model rather than those
migrations. Set:

    CarbonSim__Provider=Postgres
    CarbonSim__ConnectionString=Host=db;Database=carbonsim;Username=carbonsim;Password=...
    CarbonSim__Schema=FromModel

The host refuses Provider=Postgres with Schema=Migrate, because the shipped migrations are written for
SQLite; the model-built schema is the PostgreSQL path. The data layer is provider-neutral (the engine
never sees a provider name), and the model scripts as PostgreSQL SQL, which the data tests prove.
**Not exercised:** a live PostgreSQL server - the gate has none, so the schema has not been applied to
a real PostgreSQL instance here. The first PostgreSQL deployment should run the host once against a
scratch database and confirm it starts before pointing it at the real one.

## 5. Reaching the demo from other machines

The project ships no container image (Docker was dropped on 2026-10-17). Testers reach the one host
in one of three ways:

- **A tunnel (recommended).** Run the host on 127.0.0.1 and put a tunnel in front of it, for
  example `tailscale serve --bg 5080` for colleagues on the tailnet, or
  `cloudflared tunnel --url http://127.0.0.1:5080` for a public https address. Both terminate TLS,
  pass WebSockets, and connect from this machine, so the host trusts their forwarded headers (see
  section 6). The tunnel's own access control is the first gate and the PIN is the second.
- **The office LAN.** Start with `-Bind 0.0.0.0` and allow the port through Windows Firewall
  (`New-NetFirewallRule -DisplayName CarbonSim -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow`,
  from an elevated prompt). Traffic is plain HTTP, so use this only on a network you trust.
- **A VM.** Install the .NET 9 runtime, copy the publish folder and the script's environment
  variables across, and use either route above.

Keep the box awake for the session: turn off sleep and defer Windows Update restarts.

## 6. TLS and a reverse proxy

The host speaks plain HTTP; terminate TLS at a reverse proxy (nginx, Caddy, IIS, or a cloud load
balancer). Two things matter beyond the certificate:

- **WebSockets.** The player screens and the hub hold long-lived connections. The proxy must allow the
  HTTP Upgrade for SignalR and set generous read and idle timeouts (minutes, not 30 seconds).
- **Forwarded headers.** The host reads X-Forwarded-Proto and X-Forwarded-For from a proxy on this
  machine (loopback) only, so a tunnel or a local nginx that terminates TLS gets the sign-in cookie
  marked secure, and a remote caller sending the same headers is ignored
  (tests/CarbonSim.Web.Tests/ForwardedHeadersTests.cs). A proxy on another machine is not trusted;
  add it to ForwardedHeadersOptions.KnownProxies in Program.cs first.

A minimal nginx location for /hubs is a standard proxy with the Upgrade and Connection headers; the
Blazor circuit needs the same treatment.

## 7. Backup and restore

### SQLite - an online-safe copy

Copying a live SQLite file with the file system can catch a write halfway. Use SQLite own online copy
instead, which nothing but the host needs to run:

    sqlite3 /var/lib/carbonsim/carbonsim.sqlite "VACUUM INTO '/backup/carbonsim-2026-10-04.sqlite'"

The equivalent in the host own stack (no sqlite3 binary needed) is the tested drill in
tests/CarbonSim.Data.Tests/SqliteBackupRestoreTests.cs: it opens a live database, runs VACUUM INTO to
a second file, opens the copy on its own, loads the saved run back out of it, and checks the
leaderboard and clock match. That test is the proof the copy is usable, not just present.

To restore:

1. Stop the host (the run is saved when it is created and at each year end; the year in progress is
   lost either way).
2. Keep the old file: Move-Item carbonsim.sqlite carbonsim.before-restore.sqlite.
3. Put the backup in its place: Copy-Item backup\carbonsim-2026-10-04.sqlite carbonsim.sqlite.
4. Start the host and load the saved exercise from **Saved exercises**.

### PostgreSQL - pg_dump and pg_restore

    # back up (custom format, compressed, restorable table by table)
    pg_dump -Fc -h db -U carbonsim carbonsim > /backup/carbonsim-2026-10-04.dump

    # restore into a fresh database
    createdb -h db -U carbonsim carbonsim_restore
    pg_restore -h db -U carbonsim -d carbonsim_restore /backup/carbonsim-2026-10-04.dump

Then point the host ConnectionString at carbonsim_restore and load the saved exercise. pg_dump takes
a consistent snapshot while the server runs, so the host need not be stopped for the backup itself,
only for the switch to the restored database.

### What a backup contains

The database holds the accounts (people, hashed passwords, claimed companies) and the saved runs. It
does not hold live in-memory runs, so a restore recovers the last saved year, not the year in progress.
See the trainer runbook Recovery section for what a trainer sees after a restart.

## 8. Resetting a tester's password

The host has the reset flow as an API only; there is no reset page yet. With EmailSender set to Log
(the Demo default) the code goes to the host log. From the demo box:

    $base = 'http://127.0.0.1:5080'
    $email = 'tester@example.com'
    Invoke-RestMethod -Method Post -Uri "$base/api/accounts/password-reset/request" -ContentType 'application/json' -Body (@{ email = $email } | ConvertTo-Json)
    Select-String -Path demo-data\logs\*.log -Pattern $email -Context 0,4 | Select-Object -Last 1

The message is logged over several lines; the six-digit code is on the last of them ("Your
verification code is ..."). Set the new password with it and give the password to the tester:

    Invoke-RestMethod -Method Post -Uri "$base/api/accounts/password-reset/confirm" -ContentType 'application/json' -Body (@{ email = $email; code = '123456'; newPassword = 'a-new-password' } | ConvertTo-Json)

The code expires after ResetCodeLifetimeMinutes and closes after ResetCodeMaxAttempts wrong tries.
