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

The host keeps live runs in memory. It writes a run to the database when the year closes and after
each administrator control, so a crash loses at most the year in progress; the saved copy is loaded
back from the console **Saved exercises** list. Plan for a host restart between sessions, not during
one.

## 2. Running without a container

    dotnet build CarbonSim.sln
    dotnet run --project src/CarbonSim.Web

The default configuration binds HTTP on the port in src/CarbonSim.Web/Properties/launchSettings.json
(or ASPNETCORE_URLS). Put the settings below in appsettings.json, in environment variables, or in
user secrets. Environment variables with a double underscore override the JSON, for example
CarbonSim__ConnectionString.

## 3. Configuration reference

Everything the host reads is under the **CarbonSim** section.

| Setting | Default | What it does |
|---|---|---|
| ConnectionString | Data Source=carbonsim.sqlite | The application database. SQLite takes a file path; PostgreSQL takes a libpq connection string. |
| Provider | Sqlite | Which engine opens the connection string: Sqlite or Postgres. Unknown values stop the host at start-up. |
| Schema | Migrate | Migrate applies the shipped migrations (SQLite only). FromModel builds the schema from the model and keeps no migration history, which is what PostgreSQL uses. |
| ScenarioFile | ../../scenarios/vietnam-2024.json | The scenario whose human companies may be claimed. Absolute, or relative to the content root. |
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

## 5. Containers

Dockerfile is a multi-stage build: the .NET 9 SDK restores and publishes, and the ASP.NET 9 runtime
image runs the published app as the image unprivileged app user. The scenarios folder is copied in
beside the app, and /data is the one writable place; the container listens on 8080.

docker-compose.yml brings up the host with SQLite in a named volume. The optional postgres profile
adds a PostgreSQL 17 server beside it.

    # SQLite, one box
    $env:CARBONSIM_PIN = "the-room-pin"
    $env:CARBONSIM_ADMIN_EMAIL = "trainer@example.com"
    $env:CARBONSIM_ADMIN_PASSWORD = "a-strong-password"
    docker compose up --build

    # PostgreSQL
    $env:CARBONSIM_DB_PASSWORD = "a-strong-db-password"
    $env:CARBONSIM_PROVIDER = "Postgres"
    $env:CARBONSIM_SCHEMA = "FromModel"
    $env:CARBONSIM_CONNECTION = "Host=postgres;Database=carbonsim;Username=carbonsim;Password=a-strong-db-password"
    docker compose --profile postgres up --build

**Not built here.** Docker is not installed on the machine this was prepared on, so the image was not
built or run and /sign-in was not hit through it. The files are written to the standard shape
(multi-stage, non-root, runtime image, a single exposed port) but the first build should be verified
where Docker exists. `docker compose config` is a safe first check.

## 6. TLS and a reverse proxy

The host speaks plain HTTP; terminate TLS at a reverse proxy (nginx, Caddy, IIS, or a cloud load
balancer). Two things matter beyond the certificate:

- **WebSockets.** The player screens and the hub hold long-lived connections. The proxy must allow the
  HTTP Upgrade for SignalR and set generous read and idle timeouts (minutes, not 30 seconds).
- **Forwarded headers.** If the proxy terminates TLS, forward X-Forwarded-Proto and X-Forwarded-For so
  redirects and links use https. The host cookie is HttpOnly and SameSite=Lax; a secure-cookie flag is
  the proxy job, by forwarding https.

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

1. Stop the host (the saved run is written at year end and after each control; the live year is lost
   either way).
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

