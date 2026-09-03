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

function Invoke-NativeTest([string]$selectionOption, [string]$selection, [string]$binaryLogName, [bool]$useUniqueBinaryLog = $false) {
    $binaryLog = if ($requestedBinaryLog) {
        if ($useUniqueBinaryLog) {
            $directory = [System.IO.Path]::GetDirectoryName($requestedBinaryLog)
            $fileName = [System.IO.Path]::GetFileNameWithoutExtension($requestedBinaryLog)
            $extension = [System.IO.Path]::GetExtension($requestedBinaryLog)
            $selectionName = [System.IO.Path]::GetFileNameWithoutExtension($binaryLogName)
            $uniqueFileName = "$fileName.$selectionName$extension"
            if ($directory) { Join-Path $directory $uniqueFileName } else { $uniqueFileName }
        } else {
            $requestedBinaryLog
        }
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
    $useUniqueBinaryLog = $selectedProjects.Count -gt 1
    for ($index = 0; $index -lt $selectedProjects.Count; $index++) {
        $project = $selectedProjects[$index]
        $projectPath = if ([System.IO.Path]::IsPathRooted($project)) { $project } else { Join-Path $repoRoot $project }
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        $binaryLogName = if ($useUniqueBinaryLog) { "{0:D2}.$projectName.binlog" -f ($index + 1) } else { "$projectName.binlog" }
        if ([System.IO.Path]::GetExtension($projectPath) -in ".sln", ".slnf", ".slnx") {
            Invoke-NativeTest "--solution" $projectPath $binaryLogName $useUniqueBinaryLog
            continue
        }

        $isIntegrationTest = $projectName.EndsWith(".IntegrationTests", [System.StringComparison]::OrdinalIgnoreCase)
        $isUnitTest = -not $isIntegrationTest -and
            ($projectName.EndsWith(".UnitTests", [System.StringComparison]::OrdinalIgnoreCase) -or
             $projectName.EndsWith(".Tests", [System.StringComparison]::OrdinalIgnoreCase))
        if (($unit -and $isUnitTest) -or ($integration -and $isIntegrationTest)) {
            Invoke-NativeTest "--project" $projectPath $binaryLogName $useUniqueBinaryLog
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
