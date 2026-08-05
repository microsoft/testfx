[CmdletBinding()]
param()

$repoRoot = Split-Path $PSScriptRoot -Parent
$globalJsonPath = Join-Path $repoRoot "global.json"
$pipelinePath = Join-Path $repoRoot "azure-pipelines.yml"
$testTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/test-windows-debug-coverage.yml"

$configuration = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
$affectedTests = $configuration.test.affectedTests

if ($null -eq $affectedTests) {
    throw "global.json must define test.affectedTests."
}

foreach ($path in @(
    "changes.ignore",
    "changes.forceAllTests",
    "instrumentation.include",
    "instrumentation.exclude"
)) {
    $value = $affectedTests
    foreach ($segment in $path.Split(".")) {
        $value = $value.$segment
    }

    if ($value -isnot [System.Array] -or $value.Count -eq 0) {
        throw "global.json test.affectedTests.$path must be a non-empty array."
    }
}

$minimumAffectedTestsSdk = [System.Management.Automation.SemanticVersion]"11.0.100-rc.1.26406.108"
$configuredSdk = [System.Management.Automation.SemanticVersion]$configuration.sdk.version
if ($configuredSdk -lt $minimumAffectedTestsSdk) {
    throw "global.json must pin SDK $minimumAffectedTestsSdk or newer for affected-test command support."
}

$globalJsonText = Get-Content -LiteralPath $globalJsonPath -Raw
if ($globalJsonText -match '(?i)"[^"]*(token|secret|password|connectionString|sas)[^"]*"\s*:') {
    throw "global.json must not contain affected-test credentials or secret-bearing settings."
}

$pipeline = Get-Content -LiteralPath $pipelinePath -Raw
$collectCall = [regex]::Match(
    $pipeline,
    '(?s)enableAffectedTests:\s*(?<enabled>true|false)\s+affectedTestsMode:\s*collect')
$runCall = [regex]::Match(
    $pipeline,
    '(?s)enableAffectedTests:\s*(?<enabled>true|false)\s+affectedTestsMode:\s*run')
if (-not $collectCall.Success -or -not $runCall.Success) {
    throw "The pipeline must define explicit main collection and PR selection call sites."
}

$affectedTestsEnabled =
    $collectCall.Groups["enabled"].Value -eq "true" -or
    $runCall.Groups["enabled"].Value -eq "true"
if ($affectedTestsEnabled -and $null -eq $affectedTests.storage) {
    throw "Affected-test pipeline execution requires test.affectedTests.storage."
}

$testTemplate = Get-Content -LiteralPath $testTemplatePath -Raw
foreach ($requiredText in @(
    "Cache@2",
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1",
    "--collect-test-map",
    "--affected-tests",
    '$(Pipeline.Workspace)\affected-test-map',
    "AffectedTestsMapCacheRestored",
    "enableAffectedTests",
    "affectedTestsMode",
    "affectedTestsCacheVersion"
)) {
    if (-not $testTemplate.Contains($requiredText)) {
        throw "The affected-test template is missing '$requiredText'."
    }
}

$disabledBranch = [regex]::Match(
    $testTemplate,
    '(?s)- \$\{\{ if eq\(parameters\.enableAffectedTests, false\) \}\}:.*?(?=\r?\n# These branches)')
if (-not $disabledBranch.Success) {
    throw "The ordinary test fallback branch is missing."
}

if ($disabledBranch.Value -match 'DOTNET_CLI_ENABLE_AFFECTED_TESTS|--collect-test-map|--affected-tests') {
    throw "The ordinary test fallback must not enable affected-test behavior."
}

$pipelineVariables = Get-Content -LiteralPath (Join-Path $repoRoot "eng/pipelines/variables/test-env-vars.yml") -Raw
$outerPipelineConfiguration = $pipeline, $pipelineVariables -join "`n"
foreach ($variableName in @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS",
    "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE"
)) {
    if ($outerPipelineConfiguration.Contains($variableName)) {
        throw "$variableName must be scoped to the affected-test template."
    }
}

$templateWithoutComments = $testTemplate -split '\r?\n' |
    Where-Object { -not $_.TrimStart().StartsWith("#") } |
    Join-String -Separator "`n"
if ($templateWithoutComments.Contains("DOTNET_CLI_TEST_AFFECTED_TESTS_MODE")) {
    throw "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE is SDK-to-extension plumbing and must not be set by the pipeline."
}

$affectedTestsGateCount = [regex]::Matches(
    $templateWithoutComments,
    'DOTNET_CLI_ENABLE_AFFECTED_TESTS').Count
if ($affectedTestsGateCount -ne 2) {
    throw "DOTNET_CLI_ENABLE_AFFECTED_TESTS must appear exactly once in each enabled affected-test branch."
}

$cacheTaskCount = [regex]::Matches($templateWithoutComments, 'task:\s*Cache@2').Count
if ($cacheTaskCount -ne 2) {
    throw "The collect and run branches must each define one Cache@2 map task."
}

$collectBranch = [regex]::Match(
    $templateWithoutComments,
    "(?s)eq\(parameters\.affectedTestsMode, 'collect'\).*?(?=\r?\n- \$\{\{)")
$runBranch = [regex]::Match(
    $templateWithoutComments,
    "(?s)eq\(parameters\.affectedTestsMode, 'run'\).*?(?=\r?\n- \$\{\{)")
if (-not $collectBranch.Success -or
    -not $collectBranch.Value.Contains("DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1") -or
    -not $runBranch.Success -or
    -not $runBranch.Value.Contains("DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1")) {
    throw "The affected-test gate must be scoped to the enabled collect and run branches."
}

$runFallback = [regex]::Match(
    $templateWithoutComments,
    "(?s)displayName:\s*Test \(affected-test fallback\).*?condition:.*?Build\.Reason.*?AffectedTestsMapCacheRestored.*?AffectedTestsSucceeded")
if (-not $runFallback.Success) {
    throw "The run branch must retain a full-test fallback for non-PR runs and affected-test failures."
}

if (-not $testTemplate.Contains("PublishCoverageReport")) {
    throw "Coverage publication must be limited to full-test and collection runs."
}

Write-Output "Affected-test configuration and rollout wiring are valid."
