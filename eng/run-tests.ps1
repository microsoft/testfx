[CmdletBinding(PositionalBinding=$false)]
param(
    [string][Alias('c')]$configuration = "Debug",
    [switch]$unit,
    [switch]$integration,
    [switch]$ci,
    [string]$projects,
    [string[]]$properties
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    Write-Error "The repository-local .NET SDK was not found. Run build.cmd before running tests."
    exit 1
}

if ($ci) {
    $env:DOTNET_ROOT = Join-Path $repoRoot ".dotnet"
    $env:NUGET_PACKAGES = Join-Path $repoRoot ".packages"
}

if (-not $unit -and -not $integration) {
    Write-Error "Specify -unit, -integration, or both."
    exit 1
}

$testResultsDirectory = Join-Path $repoRoot "artifacts\TestResults\$configuration"
New-Item -Path $testResultsDirectory -ItemType Directory -Force | Out-Null

$forwardedProperties = @($properties | Where-Object { $_ -notmatch '^(?:-|/)bl(?::|$)' })
$requestedBinaryLog = $properties | Where-Object { $_ -match '^(?:-|/)bl:' } | Select-Object -Last 1
$requestedBinaryLog = if ($requestedBinaryLog) { $requestedBinaryLog.Substring($requestedBinaryLog.IndexOf(':') + 1).Trim('"') } else { $null }

function Invoke-NativeTest([string]$selectionOption, [string]$selection, [string]$binaryLogName) {
    $binaryLog = if ($requestedBinaryLog) {
        $requestedBinaryLog
    } else {
        Join-Path $testResultsDirectory $binaryLogName
    }

    & $dotnet test $selectionOption $selection `
        --configuration $configuration `
        --no-build `
        "-bl:$binaryLog" `
        @forwardedProperties

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($projects) {
    $selectedProjects = $projects.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($project in $selectedProjects) {
        $projectPath = if ([System.IO.Path]::IsPathRooted($project)) { $project } else { Join-Path $repoRoot $project }
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        $isIntegrationTest = $projectName.EndsWith(".IntegrationTests", [System.StringComparison]::OrdinalIgnoreCase)
        $isUnitTest = -not $isIntegrationTest -and
            ($projectName.EndsWith(".UnitTests", [System.StringComparison]::OrdinalIgnoreCase) -or
             $projectName.EndsWith(".Tests", [System.StringComparison]::OrdinalIgnoreCase))
        if (($unit -and $isUnitTest) -or ($integration -and $isIntegrationTest)) {
            Invoke-NativeTest "--project" $projectPath "$projectName.binlog"
        }
    }

    exit 0
}

$solution = if ($IsWindows -or $env:OS -eq "Windows_NT") {
    if ($unit -and $integration) { "Tests.slnf" }
    elseif ($unit) { "UnitTests.slnf" }
    else { "IntegrationTests.slnf" }
} else {
    if ($unit -and $integration) { "NonWindowsTests.slnf" }
    elseif ($unit) { "NonWindowsUnitTests.slnf" }
    else { "NonWindowsIntegrationTests.slnf" }
}

$solutionPath = Join-Path $repoRoot $solution
Invoke-NativeTest "--solution" $solutionPath "TestStep.binlog"
exit 0
