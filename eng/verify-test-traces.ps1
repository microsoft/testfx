[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TraceDirectory,
    [switch] $AllowIncomplete
)

$ErrorActionPreference = 'Stop'

if ([string]::Equals($env:OTEL_SDK_DISABLED, 'true', [StringComparison]::OrdinalIgnoreCase)) {
    Write-Host 'OpenTelemetry was explicitly disabled for this job.'
    return
}

$files = @()
if (Test-Path -LiteralPath $TraceDirectory -PathType Container) {
    $files = @(Get-ChildItem -LiteralPath $TraceDirectory -Filter '*.jsonl' -File)
}

if ($files.Count -eq 0) {
    Write-Warning 'No OpenTelemetry trace files were produced. No instrumented test modules may have been selected; trace collection is not proven.'
    return
}

$incomplete = [Collections.Generic.List[string]]::new()
$spanCount = 0L
foreach ($file in $files) {
    $first = Get-Content -LiteralPath $file.FullName -TotalCount 1
    $last = Get-Content -LiteralPath $file.FullName -Tail 1
    try {
        $header = $first | ConvertFrom-Json
        $completion = $last | ConvertFrom-Json
    }
    catch [ArgumentException] {
        $incomplete.Add("$($file.Name): invalid or partial JSON record")
        continue
    }

    $recordCount = 0L
    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
        $recordCount++
    }

    if ($header.recordType -ne 'resource' -or $header.format -ne 'testfx-otel-jsonl' -or $header.schemaVersion -ne 1 `
        -or $completion.recordType -ne 'completion' -or $completion.status -ne 'complete' `
        -or $completion.failedExports -ne 0 -or $null -eq $completion.spanCount -or $completion.spanCount -lt 0 `
        -or $recordCount -ne ($completion.spanCount + 2)) {
        $incomplete.Add("$($file.Name): missing/failed completion, span-count mismatch or unsupported archive header")
        continue
    }

    $spanCount += $completion.spanCount
}

if ($incomplete.Count -gt 0) {
    $message = "Incomplete OpenTelemetry traces (retained for diagnostics):`n" + ($incomplete -join "`n")
    if ($AllowIncomplete) {
        Write-Warning $message
    }
    else {
        throw $message
    }
}

Write-Host "OpenTelemetry archives: $($files.Count) files, $spanCount spans in complete files, $($incomplete.Count) incomplete files."
