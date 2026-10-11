# Rewrites the per-unit allocation factors in the Vietnam scenario.
#
# One unit in ten is allocated on a generous historic basis and the rest on a tighter one; the
# weights are mean-preserving, so the sector cakes are unchanged and only the split between
# companies moves. That mix is what gives the exchange two sides to trade with.
param(
    [string] $Path = (Join-Path (Join-Path $PSScriptRoot '..') 'vietnam-2024.json'),
    [int] $GenerousEvery = 10,
    [decimal] $GenerousFactor = 1.5,
    [decimal] $OtherFactor = 0.9444
)

$resolved = (Resolve-Path -LiteralPath $Path).Path
$raw = [System.IO.File]::ReadAllText($resolved, [System.Text.Encoding]::UTF8)
$separator = if ($raw.Contains([string][char]13 + [char]10)) { [string][char]13 + [char]10 } else { [string][char]10 }
$lines = $raw -split ([string][char]13 + '?' + [char]10)
$out = New-Object System.Collections.Generic.List[string]
$unit = 0

foreach ($line in $lines) {
    if ($line -match '^\s*"baselineEmissions":') { $unit++ }

    if ($line -match '^\s*"allocationFactor":') {
        $factor = if ($GenerousEvery -gt 0 -and $unit % $GenerousEvery -eq 0) { $GenerousFactor } else { $OtherFactor }
        $indent = ($line -replace '"allocationFactor".*$', '')
        $out.Add($indent + '"allocationFactor": ' + $factor.ToString([System.Globalization.CultureInfo]::InvariantCulture) + ',')
    }
    else {
        $out.Add($line)
    }
}

[System.IO.File]::WriteAllText($resolved, ($out -join $separator), (New-Object System.Text.UTF8Encoding($false)))

$json = [System.IO.File]::ReadAllText($resolved, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$units = $json.companies | ForEach-Object { $_.units }

"units=" + $units.Count +
    " generous=" + ($units | Where-Object { $_.allocationFactor -eq $GenerousFactor } | Measure-Object).Count +
    " other=" + ($units | Where-Object { $_.allocationFactor -eq $OtherFactor } | Measure-Object).Count
