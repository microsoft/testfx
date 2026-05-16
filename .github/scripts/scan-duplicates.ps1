<#
.SYNOPSIS
    Scans the testfx source code for duplicated code blocks using jscpd.

.DESCRIPTION
    Runs jscpd on the src/ directory and produces a JSON report in artifacts/jscpd/.
    Optionally filters results by a minimum number of duplicated lines or a specific
    subdirectory.

.PARAMETER Path
    The subdirectory under src/ to scan. Defaults to scanning all of src/.

.PARAMETER MinLines
    Minimum number of duplicated lines to report. Defaults to 6.

.PARAMETER MinTokens
    Minimum number of duplicated tokens to report. Defaults to 50.

.PARAMETER OutputDir
    Directory for the JSON report. Defaults to artifacts/jscpd.

.PARAMETER TopN
    Show only the top N results sorted by duplication size. Defaults to 0 (all).

.EXAMPLE
    .\.github\scripts\scan-duplicates.ps1
    .\.github\scripts\scan-duplicates.ps1 -Path "src/Platform/Microsoft.Testing.Platform" -MinLines 10
    .\.github\scripts\scan-duplicates.ps1 -TopN 20
#>
param(
    [string]$Path = "src",
    [int]$MinLines = 6,
    [int]$MinTokens = 50,
    [string]$OutputDir = "artifacts/jscpd",
    [int]$TopN = 0
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
Push-Location $repoRoot

try {
    # Ensure output directory exists
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    Write-Host "=== Scanning for duplicated code ===" -ForegroundColor Cyan
    Write-Host "  Path:      $Path" -ForegroundColor Gray
    Write-Host "  MinLines:  $MinLines" -ForegroundColor Gray
    Write-Host "  MinTokens: $MinTokens" -ForegroundColor Gray
    Write-Host "  Output:    $OutputDir" -ForegroundColor Gray
    Write-Host ""

    # Run jscpd (threshold=100 to avoid failing on duplication percentage)
    $jscpdArgs = @(
        "jscpd"
        $Path
        "--min-lines", $MinLines
        "--min-tokens", $MinTokens
        "--reporters", "json,consoleFull"
        "--output", $OutputDir
        "--format", "csharp"
        "--threshold", "100"
        "--ignore", "**/bin/**,**/obj/**,**/artifacts/**,**/*.Designer.cs,**/*.g.cs,**/*.xlf,**/*.resx,**/PublicAPI.*.txt,**/test/**,**/samples/**,**/formal-verification/**"
    )

    npx @jscpdArgs
    $exitCode = $LASTEXITCODE
    # jscpd exits 1 when over threshold — not an error for our purposes
    if ($exitCode -ne 0) {
        Write-Host "  (jscpd exited with code $exitCode)" -ForegroundColor DarkGray
    }

    # Parse and summarize results
    $reportPath = Join-Path $OutputDir "jscpd-report.json"
    if (Test-Path $reportPath) {
        $report = Get-Content $reportPath -Raw | ConvertFrom-Json

        $duplicates = $report.duplicates
        if ($TopN -gt 0 -and $duplicates.Count -gt $TopN) {
            $duplicates = $duplicates |
                Sort-Object { $_.lines } -Descending |
                Select-Object -First $TopN
        }

        Write-Host ""
        Write-Host "=== Summary ===" -ForegroundColor Cyan
        Write-Host "  Total clones found: $($report.duplicates.Count)" -ForegroundColor Yellow
        Write-Host "  Files with duplicates: $($report.statistics.total.sources)" -ForegroundColor Yellow

        if ($report.statistics.total.PSObject.Properties.Name -contains "percentage") {
            Write-Host "  Duplication percentage: $($report.statistics.total.percentage)%" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "  Report saved to: $reportPath" -ForegroundColor Green
        Write-Host ""

        # Print top findings
        if ($duplicates.Count -gt 0) {
            Write-Host "=== Top Findings ===" -ForegroundColor Cyan
            $rank = 1
            foreach ($dup in $duplicates) {
                $firstFile = $dup.firstFile.name
                $secondFile = $dup.secondFile.name
                $firstStart = $dup.firstFile.startLoc.line
                $firstEnd = $dup.firstFile.endLoc.line
                $secondStart = $dup.secondFile.startLoc.line
                $secondEnd = $dup.secondFile.endLoc.line
                $lines = $dup.lines

                Write-Host "  [$rank] $lines lines duplicated:" -ForegroundColor White
                Write-Host "    A: $firstFile (lines $firstStart-$firstEnd)" -ForegroundColor Gray
                Write-Host "    B: $secondFile (lines $secondStart-$secondEnd)" -ForegroundColor Gray
                Write-Host ""
                $rank++

                if ($TopN -gt 0 -and $rank -gt $TopN) { break }
            }
        }
    }
    else {
        Write-Host "No report generated. jscpd may not have found any duplicates." -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
