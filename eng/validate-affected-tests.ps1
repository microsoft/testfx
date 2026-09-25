[CmdletBinding()]
param()

function Assert-Containment {
    [CmdletBinding(DefaultParameterSetName = "LiteralMessage")]
    param(
        [string]$Text,
        [string[]]$Substrings,
        [Parameter(Mandatory, ParameterSetName = "LiteralMessage")]
        [string]$Message,
        [Parameter(Mandatory, ParameterSetName = "FormattedMessage")]
        [scriptblock]$ItemMessageFormatter,
        [scriptblock]$SubstringFormatter = { param($substring) $substring },
        [switch]$Absent
    )

    foreach ($substring in $Substrings) {
        $expectedSubstring = & $SubstringFormatter $substring
        $containsSubstring = $Text.Contains($expectedSubstring)
        $validationFailed = if ($Absent) { $containsSubstring } else { -not $containsSubstring }
        if ($validationFailed) {
            $formattedMessage = if ($PSCmdlet.ParameterSetName -eq "FormattedMessage") {
                & $ItemMessageFormatter $substring
            } else {
                $Message
            }

            throw $formattedMessage
        }
    }
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$globalJsonPath = Join-Path $repoRoot "global.json"
$pipelinePath = Join-Path $repoRoot "azure-pipelines.yml"
$testTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/test-windows-configuration-tests.yml"
$accessDatabaseInstallerPath = Join-Path $repoRoot "eng/install-access-database-engine.ps1"
$wellKnownEnvironmentVariablesPath = Join-Path $repoRoot "test/Utilities/Microsoft.Testing.TestInfrastructure/WellKnownEnvironmentVariables.cs"

$configuration = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
$affectedTests = $configuration.test.affectedTests

if ($null -eq $affectedTests) {
    throw "global.json must define test.affectedTests."
}

foreach ($path in @(
    "changes.ignore",
    "changes.forceAllTests"
)) {
    $value = $affectedTests
    foreach ($segment in $path.Split(".")) {
        $value = $value.$segment
    }

    if ($value -isnot [System.Array] -or $value.Count -eq 0) {
        throw "global.json test.affectedTests.$path must be a non-empty array."
    }
}

foreach ($requiredForceAllPattern in @(
    "Directory.Build.*",
    "src/**/Directory.Build.*",
    "test/**/Directory.Build.*"
)) {
    if ($requiredForceAllPattern -notin $affectedTests.changes.forceAllTests) {
        throw "global.json test.affectedTests.changes.forceAllTests must include '$requiredForceAllPattern'."
    }
}

$globalJsonText = Get-Content -LiteralPath $globalJsonPath -Raw
if ($globalJsonText -match '(?i)"[^"]*(token|secret|password|connectionString|sas)[^"]*"\s*:') {
    throw "global.json must not contain affected-test credentials or secret-bearing settings."
}

$pipeline = Get-Content -LiteralPath $pipelinePath -Raw
$affectedTestsCall = [regex]::Match(
    $pipeline,
    '(?s)enableAffectedTests:\s*(?<enabled>true|false)\s+\$\{\{ if eq\(variables\[''Build\.SourceBranch''\], ''refs/heads/main''\) \}\}:\s+affectedTestsMode:\s*collect\s+\$\{\{ else \}\}:\s+affectedTestsMode:\s*run')
if (-not $affectedTestsCall.Success) {
    throw "The pipeline must route the Windows test call site to collection on main and selection on PRs."
}

$affectedTestsEnabled = $affectedTestsCall.Groups["enabled"].Value -eq "true"

$bootstrappedSdk = [System.Management.Automation.SemanticVersion]$configuration.tools.dotnet
$selectedSdk = [System.Management.Automation.SemanticVersion]$configuration.sdk.version
if ($bootstrappedSdk -ne $selectedSdk) {
    throw "global.json tools.dotnet and sdk.version must match."
}

$lastUnsupportedAffectedTestsSdk = [System.Management.Automation.SemanticVersion]"11.0.100-rc.1.26406.108"
if ($affectedTestsEnabled -and $bootstrappedSdk -le $lastUnsupportedAffectedTestsSdk) {
    throw "Affected-test execution requires an SDK newer than $lastUnsupportedAffectedTestsSdk with dotnet/sdk#55574."
}

if ($null -eq $affectedTests.storage) {
    throw "Affected-test pipeline execution requires test.affectedTests.storage."
}

if ($affectedTests.storage.type -ne "azureDevOpsArtifact" -or
    $affectedTests.storage.project -ne "public" -or
    $affectedTests.storage.buildDefinitionId -ne 209 -or
    $affectedTests.storage.artifactName -ne "TestFx_AffectedTestsMaps") {
    throw "Affected-test pipeline execution must use the testfx Azure DevOps artifact provider configuration."
}

$directoryPackagesPath = Join-Path $repoRoot "Directory.Packages.props"
$directoryPackages = Get-Content -LiteralPath $directoryPackagesPath -Raw
Assert-Containment `
    -Text $directoryPackages `
    -Substrings '<PackageVersion Include="Microsoft.Testing.Extensions.AffectedTests" Version="$(MicrosoftTestingExtensionsCodeCoverageVersion)" />' `
    -Message "Directory.Packages.props must align Microsoft.Testing.Extensions.AffectedTests with the CodeCoverage dependency."
Assert-Containment `
    -Text $directoryPackages `
    -Substrings '<PackageVersion Include="Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps" Version="$(MicrosoftTestingExtensionsCodeCoverageVersion)" />' `
    -Message "Directory.Packages.props must align the Azure DevOps affected-tests provider with the CodeCoverage dependency."

$directoryBuildTargetsPath = Join-Path $repoRoot "Directory.Build.targets"
$directoryBuildTargets = Get-Content -LiteralPath $directoryBuildTargetsPath -Raw
Assert-Containment `
    -Text $directoryBuildTargets `
    -Substrings '<PackageReference Include="Microsoft.Testing.Extensions.AffectedTests"' `
    -Message "MTP test applications must reference Microsoft.Testing.Extensions.AffectedTests."
Assert-Containment `
    -Text $directoryBuildTargets `
    -Substrings '<PackageReference Include="Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps"' `
    -Message "MTP test applications must reference the Azure DevOps affected-tests storage provider."
Assert-Containment `
    -Text $directoryBuildTargets `
    -Substrings @(
    'GeneratePathProperty="true"',
    "_CopyAffectedTestsNetFrameworkRuntimeAssets",
    "MicrosoftInstrumentationEngine_x64.dll",
    "ctsrun64.dll",
    "Cts_x64.config"
) `
    -ItemMessageFormatter { param($substring) ".NET Framework affected-test applications must preserve '$substring'." }
Assert-Containment `
    -Text $directoryBuildTargets `
    -Substrings @(
    "IsTestingPlatformApplication",
    "EnableMSTestRunner",
    "UseInternalTestFramework"
) `
    -ItemMessageFormatter { param($substring) "Affected-test package references must include projects enabled through $substring." } `
    -SubstringFormatter { param($substring) "'`$($substring)' == 'true'" }

$manualEntryPoints = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "test") -Filter "Program.cs" -Recurse -File
    Get-Item -LiteralPath (Join-Path $repoRoot "samples/CtrfPlayground/Mtp/Program.cs")
    Get-Item -LiteralPath (Join-Path $repoRoot "samples/FSharpPlayground/Program.fs")
    Get-Item -LiteralPath (Join-Path $repoRoot "samples/NUnitPlayground/Program.cs")
    Get-Item -LiteralPath (Join-Path $repoRoot "samples/Playground/Program.cs")
)
foreach ($entryPoint in $manualEntryPoints) {
    $entryPointText = Get-Content -LiteralPath $entryPoint.FullName -Raw
    if ($entryPointText.Contains("TestApplication.CreateBuilderAsync(args)") -and
        -not $entryPointText.Contains(".AddAffectedTestsProvider()")) {
        throw "MTP entry point '$($entryPoint.FullName)' must register Microsoft.Testing.Extensions.AffectedTests."
    }
}

$accessDatabaseInstaller = Get-Content -LiteralPath $accessDatabaseInstallerPath -Raw
if (-not $accessDatabaseInstaller.Contains("[System.IO.Path]::GetTempPath()") -or
    $accessDatabaseInstaller.Contains("-OutFile ./accessdatabaseengine_X64.exe")) {
    throw "The Access Database Engine installer must not make the repository dirty before affected-test collection."
}

$testTemplate = Get-Content -LiteralPath $testTemplatePath -Raw
Assert-Containment `
    -Text $testTemplate `
    -Substrings @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1",
    'CTS_ACCESSTOKEN: $(System.AccessToken)',
    'CTS_COLLECTIONURI: $(System.CollectionUri)',
    'CTS_TEMPDIRECTORY: $(Agent.TempDirectory)',
    'CTS_BUILDID: $(Build.BuildId)',
    'BUILD_CONTAINERID: $(Build.ContainerId)',
    "--collect-test-map",
    "--affected-tests",
    "enableAffectedTests",
    "affectedTestsMode"
) `
    -ItemMessageFormatter { param($substring) "The affected-test template is missing '$substring'." }

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
Assert-Containment `
    -Text $outerPipelineConfiguration `
    -Substrings @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS",
    "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE",
    "TESTINGPLATFORM_EXITCODE_IGNORE"
) `
    -ItemMessageFormatter { param($substring) "$substring must be scoped to the affected-test template." } `
    -Absent

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

$wellKnownEnvironmentVariables = Get-Content -LiteralPath $wellKnownEnvironmentVariablesPath -Raw
Assert-Containment `
    -Text $wellKnownEnvironmentVariables `
    -Substrings @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS",
    "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE",
    "TESTINGPLATFORM_EXITCODE_IGNORE"
) `
    -ItemMessageFormatter { param($substring) "Child test processes must not inherit $substring from the outer pipeline invocation." } `
    -SubstringFormatter { param($substring) """$substring""" }

if ($templateWithoutComments.Contains("Cache@2") -or
    $templateWithoutComments.Contains("AffectedTestsMapCacheRestored")) {
    throw "Azure DevOps artifact storage must not be combined with Pipeline Cache map transport."
}

if ($templateWithoutComments.Contains("AffectedTestsCoreOnly") -or
    $templateWithoutComments.Contains("AffectedTestsNonCoreOnly")) {
    throw "Affected-test execution must include every target framework."
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

foreach ($requiredCollectArgument in @(
    "-p:TestingPlatformCommandLineArguments=",
    "-p:TestRunnerAdditionalArguments="
)) {
    if (-not $collectBranch.Value.Contains($requiredCollectArgument)) {
        throw "Affected-test collection must suppress incompatible test-host arguments with '$requiredCollectArgument'."
    }
}

if (-not $runBranch.Value.Contains("-p:TestRunnerAdditionalArguments=")) {
    throw "Affected-test execution must suppress the repository's retry arguments."
}

$collectStepStart = $collectBranch.Value.IndexOf("- script: |", [System.StringComparison]::Ordinal)
if ($collectStepStart -lt 0) {
    throw "The affected-test collection step is missing."
}

$collectStepEnd = $collectBranch.Value.IndexOf("name: CollectAffectedTests", $collectStepStart, [System.StringComparison]::Ordinal)
if ($collectStepEnd -lt 0) {
    throw "The affected-test collection step must be named CollectAffectedTests."
}

$fullTestStepStart = $collectBranch.Value.IndexOf("- script: |", $collectStepEnd, [System.StringComparison]::Ordinal)
if ($fullTestStepStart -lt 0) {
    throw "The normal full test step after affected-test collection is missing."
}

$fullTestStepEnd = $collectBranch.Value.IndexOf("name: Test", $fullTestStepStart, [System.StringComparison]::Ordinal)
if ($fullTestStepEnd -lt 0) {
    throw "The normal full test step after affected-test collection must be named Test."
}

$collectStep = $collectBranch.Value[$collectStepStart..($collectStepEnd - 1)] -join ""
$fullTestStep = $collectBranch.Value[$fullTestStepStart..($fullTestStepEnd - 1)] -join ""
if ($collectStep.Contains("PublishCoverageReport") -or
    -not $collectStep.Contains("--collect-test-map") -or
    -not $fullTestStep.Contains("PublishCoverageReport") -or
    $fullTestStep.Contains("--collect-test-map")) {
    throw "Affected-test collection must be followed by the normal full test and coverage run."
}

if (-not $runBranch.Value.Contains('$exitCode -in 2, 8') -or
    -not $runBranch.Value.Contains('exit $exitCode')) {
    throw "The affected-test run must preserve exit codes 2 and 8 for failed or all-skipped selections."
}

$runFallback = [regex]::Match(
    $templateWithoutComments,
    "(?s)displayName:\s*Test \(affected-test fallback\).*?condition:.*?Build\.Reason.*?AffectedTestsSucceeded")
if (-not $runFallback.Success) {
    throw "The run branch must retain a full-test fallback for non-PR runs and affected-test failures."
}

if (-not $testTemplate.Contains("PublishCoverageReport")) {
    throw "Coverage publication must be limited to full-test runs."
}

Write-Output "Affected-test configuration and rollout wiring are valid."
