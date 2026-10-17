<#
.SYNOPSIS
Publishes the CarbonSim host and starts it for an internal demo.

.DESCRIPTION
Builds a Release publish of src/CarbonSim.Web, then runs it from the publish folder in the Demo
environment (appsettings.Demo.json). Everything the session keeps - the SQLite database, the
generated credentials and the host log - goes in the data folder, which is gitignored.

The trainer account and the registration PIN are read from credentials.local.json in the data
folder. On the first run that file does not exist, so it is written with a generated password and
PIN, and the banner prints them. Delete the file to issue new ones.

Stop the host with Ctrl+C. Start it again with -NoPublish to keep the same build; the database,
the accounts and every saved exercise survive, and the trainer loads the exercise back from the
console (Saved exercises -> Load).

.PARAMETER Port
The port to listen on. Default 5080.

.PARAMETER Bind
The address to listen on. Default 127.0.0.1, which is what a tunnel on this machine needs; use
0.0.0.0 to serve the LAN directly.

.PARAMETER DataDir
Where the database, credentials and logs live. Default demo-data at the repository root.

.PARAMETER PublishDir
Where the published build goes. Default publish at the repository root.

.PARAMETER NoPublish
Start the build already in PublishDir instead of publishing again.

.EXAMPLE
powershell -File scripts/run-demo.ps1

.EXAMPLE
powershell -File scripts/run-demo.ps1 -Bind 0.0.0.0 -Port 8080 -NoPublish
#>
[CmdletBinding()]
param(
    [int] $Port = 5080,
    [string] $Bind = '127.0.0.1',
    [string] $DataDir,
    [string] $PublishDir,
    [switch] $NoPublish
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $DataDir) { $DataDir = Join-Path $root 'demo-data' }
if (-not $PublishDir) { $PublishDir = Join-Path $root 'publish' }

$DataDir = [System.IO.Path]::GetFullPath($DataDir)
$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
$logDir = Join-Path $DataDir 'logs'
New-Item -ItemType Directory -Force -Path $DataDir, $logDir | Out-Null

if (-not $NoPublish) {
    Write-Host "Publishing to $PublishDir ..."
    & dotnet publish (Join-Path $root 'src\CarbonSim.Web\CarbonSim.Web.csproj') -c Release -o $PublishDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path (Join-Path $PublishDir 'CarbonSim.Web.dll'))) {
    throw "No published build in $PublishDir. Run without -NoPublish first."
}

# A random string from the OS generator, in characters nobody misreads when copying it off a screen.
function New-Secret([int] $length) {
    $alphabet = 'abcdefghjkmnpqrstuvwxyz23456789'
    $bytes = New-Object byte[] $length
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

$credentialsFile = Join-Path $DataDir 'credentials.local.json'
if (-not (Test-Path $credentialsFile)) {
    [ordered]@{
        adminEmail      = 'trainer@carbonsim.demo'
        adminPassword   = New-Secret 16
        registrationPin = New-Secret 6
    } | ConvertTo-Json | Set-Content -Path $credentialsFile -Encoding UTF8
}
$credentials = Get-Content -Raw -Path $credentialsFile | ConvertFrom-Json

$url = "http://${Bind}:$Port"
$log = Join-Path $logDir ("host-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

$env:ASPNETCORE_ENVIRONMENT = 'Demo'
$env:ASPNETCORE_URLS = $url
$env:CarbonSim__ConnectionString = "Data Source=$(Join-Path $DataDir 'carbonsim.sqlite')"
$env:CarbonSim__AdminEmail = $credentials.adminEmail
$env:CarbonSim__AdminPassword = $credentials.adminPassword
$env:CarbonSim__RegistrationPin = $credentials.registrationPin

Write-Host ''
Write-Host "CarbonSim demo host"
Write-Host "  Listening on     $url"
Write-Host "  Trainer sign-in  $($credentials.adminEmail) / $($credentials.adminPassword)"
Write-Host "  Player PIN       $($credentials.registrationPin)"
Write-Host "  Data             $DataDir"
Write-Host "  Log              $log"
Write-Host "  Stop with Ctrl+C."
Write-Host ''

# Native stderr arrives as error records under 2>&1; the host's own failures must reach the log,
# not stop this script, so errors are not terminating while it runs.
$ErrorActionPreference = 'Continue'
$writer = New-Object System.IO.StreamWriter($log, $true, (New-Object System.Text.UTF8Encoding($false)))
$writer.AutoFlush = $true
Push-Location $PublishDir
try {
    & dotnet .\CarbonSim.Web.dll 2>&1 | ForEach-Object {
        $line = "$_"
        Write-Host $line
        $writer.WriteLine($line)
    }
}
finally {
    Pop-Location
    $writer.Dispose()
}
