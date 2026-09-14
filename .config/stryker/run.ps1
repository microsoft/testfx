param(
    [Parameter(Mandatory)]
    [string]$Project,

    [Parameter(Mandatory)]
    [string]$Output,

    [int]$Concurrency = 2,

    [string[]]$Mutate = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$manifestPath = Join-Path $PSScriptRoot 'dotnet-tools.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.tools.'dotnet-stryker'.version
$dotnet = Join-Path $repoRoot $(if ($IsWindows) { '.dotnet/dotnet.exe' } else { '.dotnet/dotnet' })
$globalPackages = (& $dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''
$strykerAssembly = Join-Path $globalPackages "dotnet-stryker/$version/tools/net10.0/any/Stryker.CLI.dll"

if (-not (Test-Path $strykerAssembly)) {
    throw "Stryker assembly not found at '$strykerAssembly'. Restore the tool manifest first."
}

$env:MutationTesting = 'true'
$env:NoWarn = 'MSTESTEXP'

Push-Location $repoRoot
try {
    $arguments = @(
        $strykerAssembly,
        '--config-file', (Join-Path $repoRoot 'stryker-config.json'),
        '--solution', (Join-Path $repoRoot 'MutationTesting.slnx'),
        '--project', "$Project.csproj",
        '--concurrency', $Concurrency,
        '--output', $Output,
        '--skip-version-check'
    )

    foreach ($pattern in $Mutate) {
        $arguments += @('--mutate', $pattern)
    }

    & $dotnet @arguments
}
finally {
    Pop-Location
}

exit $LASTEXITCODE
