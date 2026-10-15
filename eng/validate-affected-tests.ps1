[CmdletBinding()]
param(
    [switch]$SelfTest,
    [ValidateSet("true", "false")]
    [string]$EnableAffectedTests,
    [ValidateSet("true", "false")]
    [string]$EnableNonWindowsAffectedTests
)

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

function Assert-CollectionJobIsolation {
    param(
        [string]$Pipeline,
        [string]$CollectBranch,
        [string]$SharedCollectBranch
    )

    $windowsJob = [regex]::Match(
        $Pipeline,
        '(?ms)^      - job: Windows\r?\n(?<job>.*?)(?=^      - job: )').Groups["job"].Value
    $matrix = [regex]::Match(
        $windowsJob,
        '(?ms)^        strategy:\r?\n          matrix:\r?\n(?<matrix>.*?)(?=^        steps:)').Groups["matrix"].Value
    if ($matrix -notmatch 'Release:\s+_BuildConfig:\s*Release\s+_CollectAffectedTests:\s*false\s+_TestArtifactsName:\s*Release' -or
        $matrix -notmatch 'Debug:\s+_BuildConfig:\s*Debug\s+_CollectAffectedTests:\s*false\s+_TestArtifactsName:\s*Debug' -or
        $matrix -notmatch 'AffectedTests:\s+_BuildConfig:\s*Release\s+_CollectAffectedTests:\s*true\s+_TestArtifactsName:\s*AffectedTests' -or
        [regex]::Matches($matrix, '_CollectAffectedTests:\s*true').Count -ne 1) {
        throw "CollectionUsesIndependentJob: Release validation and affected-test collection must use separate Windows matrix entries."
    }

    $collectionGate = '${{ if and(eq(variables[''Build.SourceBranch''], ''refs/heads/main''), eq(parameters.SkipTests, False), eq(parameters.EnableAffectedTests, true)) }}:'
    if (-not $matrix.Contains($collectionGate)) {
        throw "CollectionHonorsRolloutGates: the collection matrix entry must require main, enabled tests, and enabled affected tests."
    }

    if (-not $windowsJob.Contains('enableAffectedTests: ${{ parameters.EnableAffectedTests }}') -or
        -not $Pipeline.Contains('validate-affected-tests.ps1 -SelfTest -EnableAffectedTests ${{ parameters.EnableAffectedTests }}')) {
        throw "AffectedTestsRollbackControlsAllJobs: the root affected-test parameter must control collection and selection."
    }

    if (-not $SharedCollectBranch.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'), ne(variables._XmlDocsOnly, 'true'), eq(variables._CollectAffectedTests, 'true'))") -or
        -not $CollectBranch.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'), ne(variables._XmlDocsOnly, 'true'), ne(variables._CollectAffectedTests, 'true'))") -or
        $CollectBranch.Contains("AffectedTestsCollectionStarted")) {
        throw "CollectionAndValidationAreExclusive: collection and full validation must execute only in their own matrix entries."
    }

    if ($SharedCollectBranch -match '(?m)^\s*continueOnError:\s*true\s*$') {
        throw "CollectionFailureRemainsBlocking: collection failures must not be ignored."
    }

    foreach ($artifact in @(
        'ArtifactName: TestResults_Windows_$(_TestArtifactsName)_Attempt$(System.JobAttempt)',
        'ArtifactName: Integration_Tests_Windows_Binlogs_$(_TestArtifactsName)',
        'ArtifactName: TestResults_Windows_$(_TestArtifactsName)'
    )) {
        if ($windowsJob -notmatch ('(?m)^\s*' + [regex]::Escape($artifact) + '\r?$')) {
            throw "WindowsArtifactsAreIsolated: each matrix entry must have distinct test-result, binlog, and dump artifact names."
        }
    }
}

function Invoke-CollectionJobIsolationSelfTest {
    param(
        [string]$Pipeline,
        [string]$CollectBranch,
        [string]$SharedCollectBranch
    )

    Assert-CollectionJobIsolation $Pipeline $CollectBranch $SharedCollectBranch
    Write-Output "PASS CollectionUsesIndependentJob"

    $cases = @(
        @{
            Name = "MissingCollectionJobIsRejected"
            Pipeline = $Pipeline.Replace("              AffectedTests:", "              RemovedCollection:")
            CollectBranch = $CollectBranch
            ExpectedFailure = "CollectionUsesIndependentJob"
        },
        @{
            Name = "FullValidationInCollectionJobIsRejected"
            Pipeline = $Pipeline
            CollectBranch = $CollectBranch.Replace("ne(variables._CollectAffectedTests, 'true')", "eq(variables._CollectAffectedTests, 'true')")
            ExpectedFailure = "CollectionAndValidationAreExclusive"
        },
        @{
            Name = "CollectionInValidationJobIsRejected"
            Pipeline = $Pipeline
            CollectBranch = $CollectBranch
            SharedCollectBranch = $SharedCollectBranch.Replace("eq(variables._CollectAffectedTests, 'true')", "ne(variables._CollectAffectedTests, 'true')")
            ExpectedFailure = "CollectionAndValidationAreExclusive"
        },
        @{
            Name = "NonMainCollectionIsRejected"
            Pipeline = $Pipeline.Replace("eq(variables['Build.SourceBranch'], 'refs/heads/main'), eq(parameters.SkipTests, False)", "eq(parameters.SkipTests, False)")
            CollectBranch = $CollectBranch
            ExpectedFailure = "CollectionHonorsRolloutGates"
        },
        @{
            Name = "SkipTestsBypassIsRejected"
            Pipeline = $Pipeline.Replace("eq(parameters.SkipTests, False), eq(parameters.EnableAffectedTests, true)", "eq(parameters.EnableAffectedTests, true)")
            CollectBranch = $CollectBranch
            ExpectedFailure = "CollectionHonorsRolloutGates"
        },
        @{
            Name = "DisabledRolloutBypassIsRejected"
            Pipeline = $Pipeline.Replace("eq(parameters.SkipTests, False), eq(parameters.EnableAffectedTests, true)", "eq(parameters.SkipTests, False)")
            CollectBranch = $CollectBranch
            ExpectedFailure = "CollectionHonorsRolloutGates"
        },
        @{
            Name = "DisconnectedRollbackIsRejected"
            Pipeline = $Pipeline.Replace('enableAffectedTests: ${{ parameters.EnableAffectedTests }}', "enableAffectedTests: true")
            CollectBranch = $CollectBranch
            ExpectedFailure = "AffectedTestsRollbackControlsAllJobs"
        },
        @{
            Name = "DisconnectedValidatorRollbackIsRejected"
            Pipeline = $Pipeline.Replace(' -EnableAffectedTests ${{ parameters.EnableAffectedTests }}', "")
            CollectBranch = $CollectBranch
            ExpectedFailure = "AffectedTestsRollbackControlsAllJobs"
        },
        @{
            Name = "IgnoredCollectionFailureIsRejected"
            Pipeline = $Pipeline
            CollectBranch = $CollectBranch
            SharedCollectBranch = $SharedCollectBranch.Replace("displayName: Test and collect affected-test map", "displayName: Test and collect affected-test map`n    continueOnError: true")
            ExpectedFailure = "CollectionFailureRemainsBlocking"
        },
        @{
            Name = "ArtifactCollisionIsRejected"
            Pipeline = $Pipeline.Replace('ArtifactName: Integration_Tests_Windows_Binlogs_$(_TestArtifactsName)', 'ArtifactName: Integration_Tests_Windows_Binlogs_$(_BuildConfig)')
            CollectBranch = $CollectBranch
            ExpectedFailure = "WindowsArtifactsAreIsolated"
        },
        @{
            Name = "TestResultArtifactCollisionIsRejected"
            Pipeline = $Pipeline.Replace('ArtifactName: TestResults_Windows_$(_TestArtifactsName)_Attempt$(System.JobAttempt)', 'ArtifactName: TestResults_Windows_$(_BuildConfig)_Attempt$(System.JobAttempt)')
            CollectBranch = $CollectBranch
            ExpectedFailure = "WindowsArtifactsAreIsolated"
        },
        @{
            Name = "DumpArtifactCollisionIsRejected"
            Pipeline = [regex]::Replace(
                $Pipeline,
                '(?m)^(?<indent> +)ArtifactName: TestResults_Windows_\$\(_TestArtifactsName\)(?<ending>\r?)$',
                '${indent}ArtifactName: TestResults_Windows_$(_BuildConfig)${ending}')
            CollectBranch = $CollectBranch
            ExpectedFailure = "WindowsArtifactsAreIsolated"
        }
    )

    foreach ($case in $cases) {
        $caseSharedCollectBranch = if ($case.ContainsKey("SharedCollectBranch")) { $case.SharedCollectBranch } else { $SharedCollectBranch }
        if ($case.Pipeline -eq $Pipeline -and $case.CollectBranch -eq $CollectBranch -and $caseSharedCollectBranch -eq $SharedCollectBranch) {
            throw "Self-test '$($case.Name)' did not change the configuration under test."
        }

        $rejected = $false
        try {
            Assert-CollectionJobIsolation $case.Pipeline $case.CollectBranch $caseSharedCollectBranch
        } catch {
            if (-not $_.Exception.Message.StartsWith("$($case.ExpectedFailure):", [System.StringComparison]::Ordinal)) {
                throw
            }

            $rejected = $true
        }

        if (-not $rejected) {
            throw "Self-test '$($case.Name)' accepted an unsafe configuration."
        }

        Write-Output "PASS $($case.Name)"
    }

    Write-Output "Collection isolation self-tests: $($cases.Count + 1) passed."
}

function Assert-NonWindowsCollectorSupport {
    param(
        [bool]$AffectedTestsEnabled,
        [bool]$NonWindowsAffectedTestsEnabled,
        [string]$PackageVersion
    )

    if ($AffectedTestsEnabled -and $NonWindowsAffectedTestsEnabled) {
        throw "NonWindowsCollectorSupportRequired: collector '$PackageVersion' has no verified Linux/macOS collection support. Keep EnableNonWindowsAffectedTests=false until a compatible collector and composed OS-specific runs have been validated."
    }
}

function Assert-NonWindowsAffectedTestsWiring {
    param(
        [string]$Pipeline,
        [string]$NonWindowsTemplate,
        [string]$SharedTemplate,
        [string]$LivePublishingTemplate,
        [string]$FinalizePublishingTemplate
    )

    if ($Pipeline -notmatch '(?s)- name: EnableNonWindowsAffectedTests\s+displayName:[^\r\n]+\s+type: boolean\s+default:\s*false' -or
        -not $Pipeline.Contains(' -EnableNonWindowsAffectedTests ${{ parameters.EnableNonWindowsAffectedTests }}')) {
        throw "NonWindowsRolloutIsExplicit: Unix affected tests must default off and pass the actual parameter to the validator."
    }

    $collectionGate = '${{ if and(eq(variables[''Build.SourceBranch''], ''refs/heads/main''), eq(parameters.SkipTests, False), eq(parameters.EnableAffectedTests, true), eq(parameters.EnableNonWindowsAffectedTests, true)) }}:'
    $preflightGate = 'or(eq(${{ and(parameters.EnableAffectedTests, parameters.EnableNonWindowsAffectedTests) }}, false), eq(dependencies.DetectChanges.result, ''Succeeded''))'
    foreach ($osName in "Linux", "MacOS") {
        $job = [regex]::Match(
            $Pipeline,
            '(?ms)^      - job: ' + $osName + '\r?\n(?<job>.*?)(?=^      - job: |^  - template: |\z)').Groups["job"].Value
        $matrix = [regex]::Match(
            $job,
            '(?ms)^        strategy:\r?\n          matrix:\r?\n(?<matrix>.*?)(?=^        steps:)').Groups["matrix"].Value
        if ($matrix -notmatch 'Release:\s+_BuildConfig:\s*Release\s+_CollectAffectedTests:\s*false\s+_TestArtifactsName:\s*Release' -or
            $matrix -notmatch 'AffectedTests:\s+_BuildConfig:\s*Release\s+_CollectAffectedTests:\s*true\s+_TestArtifactsName:\s*AffectedTests' -or
            [regex]::Matches($matrix, '_CollectAffectedTests:\s*true').Count -ne 1) {
            throw "NonWindowsCollectionUsesIndependentJob: $osName validation and collection must have separate matrix entries."
        }

        if (-not $matrix.Contains($collectionGate) -or
            $job -notmatch ([regex]::Escape($collectionGate) + '\r?\n          timeoutInMinutes: 240')) {
            throw "NonWindowsCollectionHonorsRolloutGates: $osName collection and its independent budget must require main, enabled tests, and both rollout parameters."
        }

        $condition = [regex]::Match($job, '(?m)^        condition: (?<condition>[^\r\n]+)').Groups["condition"].Value
        if (-not $condition.StartsWith("and(succeededOrFailed(), $preflightGate, ", [System.StringComparison]::Ordinal)) {
            throw "NonWindowsOptInRequiresSuccessfulPreflight: $osName must require successful DetectChanges for affected tests and retain ordinary full-validation behavior when either rollout parameter is disabled."
        }

        if (-not $job.Contains('enableAffectedTests: ${{ and(parameters.EnableAffectedTests, parameters.EnableNonWindowsAffectedTests) }}')) {
            throw "NonWindowsRollbackControlsAllJobs: $osName selection must honor both rollout parameters."
        }

        if ($job -notmatch '(?s)- \$\{\{ if eq\(parameters\.SkipTests, False\) \}\}:\s+- template: /eng/pipelines/steps/test-non-windows.yml' -or
            $job -notmatch ('(?s)osName:\s*' + $osName + '\s+enableAffectedTests:[^\r\n]+\s+\$\{\{ if eq\(variables\[''Build\.SourceBranch''\], ''refs/heads/main''\) \}\}:\s+affectedTestsMode:\s*collect\s+\$\{\{ else \}\}:\s+affectedTestsMode:\s*run')) {
            throw "NonWindowsModeRoutesByBranch: $osName must collect on main and select on PRs inside the SkipTests guard."
        }
    }

    if (-not $NonWindowsTemplate.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'), ne(variables._CollectAffectedTests, 'true'))") -or
        -not $SharedTemplate.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'), ne(variables._XmlDocsOnly, 'true'), eq(variables._CollectAffectedTests, 'true'))")) {
        throw "NonWindowsCollectionAndValidationAreExclusive: collection cannot execute ordinary validation or consume its failure state."
    }

    if (-not $NonWindowsTemplate.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'), or(ne(variables['Build.Reason'], 'PullRequest'), ne(variables['AffectedTestsSucceeded'], 'true')))") -or
        -not $NonWindowsTemplate.Contains("condition: and(succeeded(), eq(variables._BuildConfig, 'Release'))")) {
        throw "NonWindowsFullFallbackIsRequired: disabled, scheduled, manual, and failed-selection runs must retain ordinary full testing."
    }

    if (-not $NonWindowsTemplate.Contains("solution: NonWindowsTests.slnf") -or
        -not $NonWindowsTemplate.Contains('affectedTestsMode: ${{ parameters.affectedTestsMode }}') -or
        -not $NonWindowsTemplate.Contains("eq(parameters.enableAffectedTests, true), ne(parameters.affectedTestsMode, 'disabled')") -or
        -not $NonWindowsTemplate.Contains("dotnet test --solution NonWindowsTests.slnf") -or
        -not $SharedTemplate.Contains("'--solution'") -or
        -not $SharedTemplate.Contains('${{ parameters.solution }}')) {
        throw "NonWindowsExecutionUsesSharedCommands: Unix must use its solution filter and the shared collection/selection contract."
    }

    $invalidModeGuard = $NonWindowsTemplate.IndexOf('- pwsh: throw "affectedTestsMode must be ''collect'' or ''run'' when enableAffectedTests is true."', [System.StringComparison]::Ordinal)
    $fullTestCommand = $NonWindowsTemplate.IndexOf("- script: dotnet test --solution NonWindowsTests.slnf", [System.StringComparison]::Ordinal)
    if ($invalidModeGuard -lt 0 -or $invalidModeGuard -gt $fullTestCommand) {
        throw "NonWindowsInvalidModeFailsBeforeTesting: invalid enabled configurations must be rejected before ordinary tests run."
    }

    foreach ($artifact in @(
        'ArtifactName: TestResults_${{ parameters.osName }}_$(_TestArtifactsName)_Attempt$(System.JobAttempt)',
        'ArtifactName: Integration_Tests_${{ parameters.osName }}_Binlogs_$(_TestArtifactsName)'
    )) {
        if (-not $NonWindowsTemplate.Contains($artifact)) {
            throw "NonWindowsArtifactsAreIsolated: validation and collection must have distinct test-result and binlog artifacts."
        }
    }

    if (-not $NonWindowsTemplate.Contains("condition: and(always(), ne(variables._CollectAffectedTests, 'true'), ne(variables.TESTFX_AZDO_LIVE_PUBLISHING_SUCCEEDED, 'true'))") -or
        -not $LivePublishingTemplate.Contains("`$env:_COLLECTAFFECTEDTESTS -ne 'true'")) {
        throw "NonWindowsCollectionSuppressesReporting: collection must not enable live publishing or attempt TRX fallback."
    }

    if (-not $LivePublishingTemplate.Contains("Write-Host 'Azure DevOps live test-result publishing is disabled for this job or its OAuth token is unavailable.'") -or
        -not $FinalizePublishingTemplate.Contains("Write-Host 'Azure DevOps live test publishing was not configured.'")) {
        throw "CollectionPublishingMessagesAreAccurate: disabled-publishing messages must not promise TRX fallback for collection jobs."
    }

    if ($SharedTemplate -match '(?m)^\s*continueOnError:\s*true\s*$' -or
        -not $SharedTemplate.Contains('exit $LASTEXITCODE') -or
        -not $SharedTemplate.Contains('$exitCode -in 2, 8')) {
        throw "SharedAffectedTestExitsRemainBlocking: collection exits and real selected-test failures must not be normalized to success."
    }
}

function Invoke-NonWindowsAffectedTestsSelfTest {
    param(
        [string]$Pipeline,
        [string]$NonWindowsTemplate,
        [string]$SharedTemplate,
        [string]$LivePublishingTemplate,
        [string]$FinalizePublishingTemplate,
        [string]$PackageVersion
    )

    Assert-NonWindowsAffectedTestsWiring $Pipeline $NonWindowsTemplate $SharedTemplate $LivePublishingTemplate $FinalizePublishingTemplate
    Write-Output "PASS NonWindowsAffectedTestsWiring"

    $cases = @(
        @{
            Name = "UnixRolloutEnabledByDefaultIsRejected"
            Pipeline = [regex]::Replace($Pipeline, '(?s)(- name: EnableNonWindowsAffectedTests\s+displayName:[^\r\n]+\s+type: boolean\s+default:\s*)false', '${1}true')
            ExpectedFailure = "NonWindowsRolloutIsExplicit"
        },
        @{
            Name = "DisconnectedUnixValidatorRollbackIsRejected"
            Pipeline = $Pipeline.Replace(' -EnableNonWindowsAffectedTests ${{ parameters.EnableNonWindowsAffectedTests }}', "")
            ExpectedFailure = "NonWindowsRolloutIsExplicit"
        },
        @{
            Name = "UnixValidationInCollectionJobIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace("ne(variables._CollectAffectedTests, 'true')", "eq(variables._CollectAffectedTests, 'true')")
            ExpectedFailure = "NonWindowsCollectionAndValidationAreExclusive"
        },
        @{
            Name = "UnixFullFallbackRemovedIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace("ne(variables['AffectedTestsSucceeded'], 'true')", "eq(variables['AffectedTestsSucceeded'], 'true')")
            ExpectedFailure = "NonWindowsFullFallbackIsRequired"
        },
        @{
            Name = "UnixSolutionFilterRemovedIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace("solution: NonWindowsTests.slnf", "solution: TestFx.slnx")
            ExpectedFailure = "NonWindowsExecutionUsesSharedCommands"
        },
        @{
            Name = "UnixInvalidModeGuardRemovedIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace('  - pwsh: throw "affectedTestsMode', '  - pwsh: Write-Host "affectedTestsMode')
            ExpectedFailure = "NonWindowsInvalidModeFailsBeforeTesting"
        },
        @{
            Name = "UnixTestResultArtifactCollisionIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace('TestResults_${{ parameters.osName }}_$(_TestArtifactsName)', 'TestResults_${{ parameters.osName }}_$(_BuildConfig)')
            ExpectedFailure = "NonWindowsArtifactsAreIsolated"
        },
        @{
            Name = "UnixBinlogArtifactCollisionIsRejected"
            NonWindowsTemplate = $NonWindowsTemplate.Replace('Binlogs_$(_TestArtifactsName)', 'Binlogs_$(_BuildConfig)')
            ExpectedFailure = "NonWindowsArtifactsAreIsolated"
        },
        @{
            Name = "UnixCollectionLivePublishingIsRejected"
            LivePublishingTemplate = $LivePublishingTemplate.Replace(" -and `$env:_COLLECTAFFECTEDTESTS -ne 'true'", "")
            ExpectedFailure = "NonWindowsCollectionSuppressesReporting"
        },
        @{
            Name = "CollectionConfigureFallbackPromiseIsRejected"
            LivePublishingTemplate = $LivePublishingTemplate.Replace("its OAuth token is unavailable.'", "its OAuth token is unavailable. The PublishTestResults fallback will be used.'")
            ExpectedFailure = "CollectionPublishingMessagesAreAccurate"
        },
        @{
            Name = "CollectionFinalizeFallbackPromiseIsRejected"
            FinalizePublishingTemplate = $FinalizePublishingTemplate.Replace("live test publishing was not configured.'", "live test publishing was not configured. The PublishTestResults fallback will be used.'")
            ExpectedFailure = "CollectionPublishingMessagesAreAccurate"
        },
        @{
            Name = "IgnoredSharedCollectionFailureIsRejected"
            SharedTemplate = $SharedTemplate.Replace("displayName: Test and collect affected-test map", "displayName: Test and collect affected-test map`n    continueOnError: true")
            ExpectedFailure = "SharedAffectedTestExitsRemainBlocking"
        },
        @{
            Name = "IgnoredSelectedTestFailureIsRejected"
            SharedTemplate = $SharedTemplate.Replace('$exitCode -in 2, 8', '$exitCode -in 8')
            ExpectedFailure = "SharedAffectedTestExitsRemainBlocking"
        }
    )

    foreach ($osName in "Linux", "MacOS") {
        $preflightGate = 'or(eq(${{ and(parameters.EnableAffectedTests, parameters.EnableNonWindowsAffectedTests) }}, false), eq(dependencies.DetectChanges.result, ''Succeeded''))'
        $job = [regex]::Match(
            $Pipeline,
            '(?ms)^      - job: ' + $osName + '\r?\n(?<job>.*?)(?=^      - job: |^  - template: |\z)').Groups["job"].Value
        foreach ($mutation in @(
            @{
                Name = "${osName}CollectionJobRemovedIsRejected"
                Job = $job.Replace("              AffectedTests:", "              RemovedCollection:")
                ExpectedFailure = "NonWindowsCollectionUsesIndependentJob"
            },
            @{
                Name = "${osName}NonMainCollectionIsRejected"
                Job = $job.Replace("eq(variables['Build.SourceBranch'], 'refs/heads/main'), eq(parameters.SkipTests, False)", "eq(parameters.SkipTests, False)")
                ExpectedFailure = "NonWindowsCollectionHonorsRolloutGates"
            },
            @{
                Name = "${osName}SkipTestsBypassIsRejected"
                Job = $job.Replace("eq(parameters.SkipTests, False), eq(parameters.EnableAffectedTests, true)", "eq(parameters.EnableAffectedTests, true)")
                ExpectedFailure = "NonWindowsCollectionHonorsRolloutGates"
            },
            @{
                Name = "${osName}RootRollbackBypassIsRejected"
                Job = $job.Replace("eq(parameters.EnableAffectedTests, true), eq(parameters.EnableNonWindowsAffectedTests, true)", "eq(parameters.EnableNonWindowsAffectedTests, true)")
                ExpectedFailure = "NonWindowsCollectionHonorsRolloutGates"
            },
            @{
                Name = "${osName}UnixRollbackBypassIsRejected"
                Job = $job.Replace(", eq(parameters.EnableNonWindowsAffectedTests, true)", "")
                ExpectedFailure = "NonWindowsCollectionHonorsRolloutGates"
            },
            @{
                Name = "${osName}SelectionRollbackBypassIsRejected"
                Job = $job.Replace('enableAffectedTests: ${{ and(parameters.EnableAffectedTests, parameters.EnableNonWindowsAffectedTests) }}', "enableAffectedTests: true")
                ExpectedFailure = "NonWindowsRollbackControlsAllJobs"
            },
            @{
                Name = "${osName}FailedPreflightBypassIsRejected"
                Job = $job.Replace("$preflightGate, ", "")
                ExpectedFailure = "NonWindowsOptInRequiresSuccessfulPreflight"
            },
            @{
                Name = "${osName}PreflightGateIgnoresRollbackIsRejected"
                Job = $job.Replace($preflightGate, $preflightGate.Replace("and(parameters.EnableAffectedTests, parameters.EnableNonWindowsAffectedTests)", "parameters.EnableNonWindowsAffectedTests"))
                ExpectedFailure = "NonWindowsOptInRequiresSuccessfulPreflight"
            },
            @{
                Name = "${osName}WrongModeIsRejected"
                Job = $job.Replace("affectedTestsMode: collect", "affectedTestsMode: run")
                ExpectedFailure = "NonWindowsModeRoutesByBranch"
            }
        )) {
            $cases += @{
                Name = $mutation.Name
                Pipeline = $Pipeline.Replace($job, $mutation.Job)
                ExpectedFailure = $mutation.ExpectedFailure
            }
        }
    }

    foreach ($case in $cases) {
        $casePipeline = if ($case.ContainsKey("Pipeline")) { $case.Pipeline } else { $Pipeline }
        $caseNonWindowsTemplate = if ($case.ContainsKey("NonWindowsTemplate")) { $case.NonWindowsTemplate } else { $NonWindowsTemplate }
        $caseSharedTemplate = if ($case.ContainsKey("SharedTemplate")) { $case.SharedTemplate } else { $SharedTemplate }
        $caseLivePublishingTemplate = if ($case.ContainsKey("LivePublishingTemplate")) { $case.LivePublishingTemplate } else { $LivePublishingTemplate }
        $caseFinalizePublishingTemplate = if ($case.ContainsKey("FinalizePublishingTemplate")) { $case.FinalizePublishingTemplate } else { $FinalizePublishingTemplate }
        if ($casePipeline -eq $Pipeline -and $caseNonWindowsTemplate -eq $NonWindowsTemplate -and $caseSharedTemplate -eq $SharedTemplate -and $caseLivePublishingTemplate -eq $LivePublishingTemplate -and $caseFinalizePublishingTemplate -eq $FinalizePublishingTemplate) {
            throw "Self-test '$($case.Name)' did not change the configuration under test."
        }

        $rejected = $false
        try {
            Assert-NonWindowsAffectedTestsWiring $casePipeline $caseNonWindowsTemplate $caseSharedTemplate $caseLivePublishingTemplate $caseFinalizePublishingTemplate
        } catch {
            if (-not $_.Exception.Message.StartsWith("$($case.ExpectedFailure):", [System.StringComparison]::Ordinal)) {
                throw
            }

            $rejected = $true
        }

        if (-not $rejected) {
            throw "Self-test '$($case.Name)' accepted an unsafe configuration."
        }

        Write-Output "PASS $($case.Name)"
    }

    foreach ($case in @(
        @{ Name = "UnixDisabledKeepsFullTests"; Root = $true; Unix = $false; Version = $PackageVersion; Reject = $false },
        @{ Name = "RootRollbackOverridesUnixOptIn"; Root = $false; Unix = $true; Version = $PackageVersion; Reject = $false },
        @{ Name = "WindowsOnlyCollectorActivationIsRejected"; Root = $true; Unix = $true; Version = $PackageVersion; Reject = $true },
        @{ Name = "UnverifiedCollectorActivationIsRejected"; Root = $true; Unix = $true; Version = "unverified-version"; Reject = $true }
    )) {
        $rejected = $false
        try {
            Assert-NonWindowsCollectorSupport $case.Root $case.Unix $case.Version
        } catch {
            if (-not $case.Reject -or -not $_.Exception.Message.StartsWith("NonWindowsCollectorSupportRequired:", [System.StringComparison]::Ordinal)) {
                throw
            }

            $rejected = $true
        }

        if ($rejected -ne $case.Reject) {
            throw "Self-test '$($case.Name)' did not enforce the collector support gate."
        }

        Write-Output "PASS $($case.Name)"
    }

    Write-Output "Non-Windows rollout self-tests: $($cases.Count + 5) passed."
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$globalJsonPath = Join-Path $repoRoot "global.json"
$versionsPropsPath = Join-Path $repoRoot "eng/Versions.props"
$pipelinePath = Join-Path $repoRoot "azure-pipelines.yml"
$testTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/test-windows-configuration-tests.yml"
$sharedTestTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/test-affected-tests.yml"
$nonWindowsTestTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/test-non-windows.yml"
$livePublishingTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/configure-azdo-live-test-publishing.yml"
$finalizePublishingTemplatePath = Join-Path $repoRoot "eng/pipelines/steps/finalize-azdo-live-test-publishing.yml"
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
    '(?s)enableAffectedTests:\s*(?<enabled>true|false|\$\{\{\s*parameters\.EnableAffectedTests\s*\}\})\s+\$\{\{ if eq\(variables\[''Build\.SourceBranch''\], ''refs/heads/main''\) \}\}:\s+affectedTestsMode:\s*collect\s+\$\{\{ else \}\}:\s+affectedTestsMode:\s*run')
if (-not $affectedTestsCall.Success) {
    throw "The pipeline must route the Windows test call site to collection on main and selection on PRs."
}

$affectedTestsEnabled = if ($PSBoundParameters.ContainsKey("EnableAffectedTests")) {
    [bool]::Parse($EnableAffectedTests)
} elseif ($affectedTestsCall.Groups["enabled"].Value -in "true", "false") {
    $affectedTestsCall.Groups["enabled"].Value -eq "true"
} else {
    $affectedTestsParameter = [regex]::Match(
        $pipeline,
        '(?s)- name: EnableAffectedTests\s+displayName:[^\r\n]+\s+type: boolean\s+default:\s*(?<enabled>true|false)')
    if (-not $affectedTestsParameter.Success) {
        throw "The root pipeline must define the EnableAffectedTests boolean parameter."
    }

    $affectedTestsParameter.Groups["enabled"].Value -eq "true"
}

$versionsProps = Get-Content -LiteralPath $versionsPropsPath -Raw
$affectedTestsPackageVersionMatch = [regex]::Match(
    $versionsProps,
    '<MicrosoftTestingExtensionsAffectedTestsVersion>(?<version>[^<]+)</MicrosoftTestingExtensionsAffectedTestsVersion>')
if (-not $affectedTestsPackageVersionMatch.Success) {
    throw "eng/Versions.props must define MicrosoftTestingExtensionsAffectedTestsVersion."
}

$affectedTestsPackageVersion = $affectedTestsPackageVersionMatch.Groups["version"].Value
$nonWindowsAffectedTestsEnabled = if ($PSBoundParameters.ContainsKey("EnableNonWindowsAffectedTests")) {
    [bool]::Parse($EnableNonWindowsAffectedTests)
} else {
    $nonWindowsParameter = [regex]::Match(
        $pipeline,
        '(?s)- name: EnableNonWindowsAffectedTests\s+displayName:[^\r\n]+\s+type: boolean\s+default:\s*(?<enabled>true|false)')
    if (-not $nonWindowsParameter.Success) {
        throw "NonWindowsRolloutIsExplicit: the root pipeline must define the EnableNonWindowsAffectedTests boolean parameter."
    }

    [bool]::Parse($nonWindowsParameter.Groups["enabled"].Value)
}
Assert-NonWindowsCollectorSupport $affectedTestsEnabled $nonWindowsAffectedTestsEnabled $affectedTestsPackageVersion

$azureDevOpsStorageVersionsMissingContentRange = @(
    "18.12.0-preview.26473.3",
    "18.12.0-preview.26474.2"
)
if ($affectedTestsEnabled -and $affectedTestsPackageVersion -in $azureDevOpsStorageVersionsMissingContentRange) {
    throw "Affected-test execution must remain disabled with Azure DevOps storage package version '$affectedTestsPackageVersion', which omits the required Content-Range upload header."
}

$bootstrappedSdk = [System.Management.Automation.SemanticVersion]$configuration.tools.dotnet
$selectedSdk = [System.Management.Automation.SemanticVersion]$configuration.sdk.version
if ($bootstrappedSdk -ne $selectedSdk) {
    throw "global.json tools.dotnet and sdk.version must match."
}

$affectedTestsSdkVersionsWithCollectionHandshakeFailure = @(
    [System.Management.Automation.SemanticVersion]"11.0.100-rc.2.26471.109",
    [System.Management.Automation.SemanticVersion]"11.0.100-rtm.26476.107",
    [System.Management.Automation.SemanticVersion]"12.0.100-alpha.1.26506.125"
)
if ($affectedTestsEnabled -and $selectedSdk -in $affectedTestsSdkVersionsWithCollectionHandshakeFailure) {
    throw "Affected-test execution must remain disabled with SDK '$selectedSdk', which reports successful collection children as handshake failures."
}

$minimumAffectedTestsSdk = [System.Management.Automation.SemanticVersion]"12.0.100-alpha.1.26509.101"
if ($affectedTestsEnabled -and $bootstrappedSdk -lt $minimumAffectedTestsSdk) {
    throw "Affected-test execution requires SDK $minimumAffectedTestsSdk or newer with dotnet/sdk#56538."
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
    -Substrings '<PackageVersion Include="Microsoft.Testing.Extensions.AffectedTests" Version="$(MicrosoftTestingExtensionsAffectedTestsVersion)" />' `
    -Message "Directory.Packages.props must use the affected-tests dependency version."
Assert-Containment `
    -Text $directoryPackages `
    -Substrings '<PackageVersion Include="Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps" Version="$(MicrosoftTestingExtensionsAffectedTestsVersion)" />' `
    -Message "Directory.Packages.props must align the Azure DevOps provider with the affected-tests dependency."

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
    "_CopyAffectedTestsRuntimeAssets",
    "_RegisterAffectedTestsRuntimeAssetsForClean",
    "'`$(RuntimeIdentifier)' == 'win-x64'",
    "MicrosoftInstrumentationEngine_x64.dll",
    "msdia140.dll",
    "ctsrun64.dll",
    "Cts_x64.config"
) `
    -ItemMessageFormatter { param($substring) ".NET Framework and RID-specific affected-test applications must preserve '$substring'." }
Assert-Containment `
    -Text $directoryBuildTargets `
    -Substrings @(
    "IsTestingPlatformApplication",
    "EnableMSTestRunner",
    "UseInternalTestFramework"
) `
    -ItemMessageFormatter { param($substring) "Affected-test package references must include projects enabled through $substring." } `
    -SubstringFormatter { param($substring) "'`$($substring)' == 'true'" }

$testDirectoryBuildTargetsPath = Join-Path $repoRoot "test/Directory.Build.targets"
$testDirectoryBuildTargets = Get-Content -LiteralPath $testDirectoryBuildTargetsPath -Raw
Assert-Containment `
    -Text $testDirectoryBuildTargets `
    -Substrings '<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" VersionOverride="$(MicrosoftTestingExtensionsAffectedTestsVersion)"' `
    -Message "Affected-test applications must use the matching preview CodeCoverage dependency without changing the shipping version."

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
$sharedTestTemplate = Get-Content -LiteralPath $sharedTestTemplatePath -Raw
$nonWindowsTestTemplate = Get-Content -LiteralPath $nonWindowsTestTemplatePath -Raw
$livePublishingTemplate = Get-Content -LiteralPath $livePublishingTemplatePath -Raw
$finalizePublishingTemplate = Get-Content -LiteralPath $finalizePublishingTemplatePath -Raw
Assert-Containment `
    -Text $sharedTestTemplate `
    -Substrings @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1",
    'CTS_ACCESSTOKEN: $(System.AccessToken)',
    'CTS_COLLECTIONURI: $(System.CollectionUri)',
    'CTS_TEMPDIRECTORY: $(Agent.TempDirectory)',
    'CTS_BUILDID: $(Build.BuildId)',
    'BUILD_CONTAINERID: $(Build.ContainerId)',
    "--collect-test-map",
    "--affected-tests",
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

$templateWithoutComments = $sharedTestTemplate -split '\r?\n' |
    Where-Object { -not $_.TrimStart().StartsWith("#") } |
    Join-String -Separator "`n"
$windowsTemplateWithoutComments = $testTemplate -split '\r?\n' |
    Where-Object { -not $_.TrimStart().StartsWith("#") } |
    Join-String -Separator "`n"
if ($templateWithoutComments.Contains("DOTNET_CLI_TEST_AFFECTED_TESTS_MODE")) {
    throw "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE is SDK-to-extension plumbing and must not be set by the pipeline."
}

$affectedTestsGateCount = [regex]::Matches(
    $templateWithoutComments,
    'DOTNET_CLI_ENABLE_AFFECTED_TESTS').Count
if ($affectedTestsGateCount -ne 1) {
    throw "DOTNET_CLI_ENABLE_AFFECTED_TESTS must appear exactly once on the shared affected-test step."
}

foreach ($environmentVariable in @(
    "DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1",
    'CTS_ACCESSTOKEN: $(System.AccessToken)',
    'CTS_COLLECTIONURI: $(System.CollectionUri)',
    'CTS_TEMPDIRECTORY: $(Agent.TempDirectory)',
    'CTS_BUILDID: $(Build.BuildId)',
    'BUILD_CONTAINERID: $(Build.ContainerId)',
    'SYSTEM_ACCESSTOKEN: $(System.AccessToken)'
)) {
    if ([regex]::Matches($templateWithoutComments, [regex]::Escape($environmentVariable)).Count -ne 1) {
        throw "Affected-test environment wiring '$environmentVariable' must be declared exactly once."
    }
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

$collectMode = [regex]::Match(
    $templateWithoutComments,
    '(?s)if \(\$affectedTestsMode -eq ''collect''\) \{(?<body>.*?)\r?\n    \}')
$runMode = [regex]::Match(
    $templateWithoutComments,
    '(?s)if \(\$affectedTestsMode -eq ''collect''\).*?else\s*\{(?<body>.*?)\r?\n    \}')
if (-not $collectMode.Success -or
    -not $collectMode.Groups["body"].Value.Contains("-p:TestingPlatformCommandLineArguments=") -or
    -not $collectMode.Groups["body"].Value.Contains("-p:TestRunnerAdditionalArguments=") -or
    -not $runMode.Success -or
    -not $runMode.Groups["body"].Value.Contains("-p:TestRunnerAdditionalArguments=")) {
    throw "Affected-test collection and execution must retain their mode-specific MSBuild arguments."
}

Assert-Containment `
    -Text $templateWithoutComments `
    -Substrings @(
    "'--solution'",
    '${{ parameters.solution }}',
    "'-c'",
    '$(_BuildConfig)',
    "'--no-build'",
    "'-p:UsingDotNetTest=true'",
    "'-p:TestingPlatformCaptureOutput=false'",
    '-bl:$(Build.SourcesDirectory)/artifacts/TestResults/$(_BuildConfig)/$binlogName.binlog',
    "& dotnet test @testArguments"
) `
    -Message "The shared affected-test step must preserve its common dotnet test arguments."

$affectedTestsStepCount = [regex]::Matches($templateWithoutComments, '(?m)^- pwsh: \|$').Count
if ($affectedTestsStepCount -ne 1 -or
    [regex]::Matches($templateWithoutComments, '(?m)^  env:$').Count -ne 1 -or
    -not $templateWithoutComments.Contains("name: CollectAffectedTests") -or
    -not $templateWithoutComments.Contains("name: TestAffected") -or
    -not $templateWithoutComments.Contains("eq(variables._CollectAffectedTests, 'true')") -or
    -not $templateWithoutComments.Contains("eq(variables['Build.Reason'], 'PullRequest')")) {
    throw "Collect and run must share one affected-test step while preserving their names and conditions."
}

$collectSwitch = [regex]::Match(
    $templateWithoutComments,
    '(?s)if \(\$affectedTestsMode -eq ''collect''\) \{\s*\$testArguments \+= ''--collect-test-map''')
$runSwitch = [regex]::Match(
    $templateWithoutComments,
    '(?s)else \{\s*\$testArguments \+= ''--affected-tests''')
if (-not $collectSwitch.Success -or -not $runSwitch.Success) {
    throw "Affected-test execution must select the matching map collection or affected-test option."
}
$windowsCollectBranch = [regex]::Match(
    $windowsTemplateWithoutComments,
    "(?s)eq\(parameters\.affectedTestsMode, 'collect'\).*?(?=\r?\n- \$\{\{)")
if (-not $windowsCollectBranch.Success -or
    -not $windowsCollectBranch.Value.Contains("solution: TestFx.slnx") -or
    [regex]::Matches($testTemplate, '/eng/pipelines/steps/test-affected-tests.yml').Count -ne 2) {
    throw "Windows collection and selection must use the shared affected-test commands with TestFx.slnx."
}

foreach ($requiredCollectArgument in @(
    "-p:TestingPlatformCommandLineArguments=",
    "-p:TestRunnerAdditionalArguments="
)) {
    if (-not $collectMode.Groups["body"].Value.Contains($requiredCollectArgument)) {
        throw "Affected-test collection must suppress incompatible test-host arguments with '$requiredCollectArgument'."
    }
}

if (-not $runMode.Groups["body"].Value.Contains("-p:TestRunnerAdditionalArguments=")) {
    throw "Affected-test execution must suppress the repository's retry arguments."
}

$fullTestStepStart = $windowsCollectBranch.Value.IndexOf("- script: |", [System.StringComparison]::Ordinal)
if ($fullTestStepStart -lt 0) {
    throw "The ordinary Release full test step is missing."
}

$fullTestStepEnd = $windowsCollectBranch.Value.IndexOf("name: Test", $fullTestStepStart, [System.StringComparison]::Ordinal)
if ($fullTestStepEnd -lt 0) {
    throw "The ordinary Release full test step must be named Test."
}

$collectStep = $templateWithoutComments
$fullTestStep = $windowsCollectBranch.Value[$fullTestStepStart..($fullTestStepEnd - 1)] -join ""
if ($collectStep.Contains("PublishCoverageReport") -or
    -not $collectStep.Contains("--collect-test-map") -or
    -not $fullTestStep.Contains("PublishCoverageReport") -or
    $fullTestStep.Contains("--collect-test-map")) {
    throw "Affected-test collection must retain the independent full test and coverage command."
}

if ($SelfTest) {
    Invoke-CollectionJobIsolationSelfTest $pipeline $windowsCollectBranch.Value $templateWithoutComments
    Invoke-NonWindowsAffectedTestsSelfTest $pipeline $nonWindowsTestTemplate $sharedTestTemplate $livePublishingTemplate $finalizePublishingTemplate $affectedTestsPackageVersion
} else {
    Assert-CollectionJobIsolation $pipeline $windowsCollectBranch.Value $templateWithoutComments
    Assert-NonWindowsAffectedTestsWiring $pipeline $nonWindowsTestTemplate $sharedTestTemplate $livePublishingTemplate $finalizePublishingTemplate
}

if (-not $templateWithoutComments.Contains('$exitCode -in 2, 8') -or
    -not $templateWithoutComments.Contains('exit $exitCode')) {
    throw "The affected-test run must preserve exit codes 2 and 8 for failed or all-skipped selections."
}

$runFallback = [regex]::Match(
    $windowsTemplateWithoutComments,
    "(?s)displayName:\s*Test \(affected-test fallback\).*?condition:.*?Build\.Reason.*?AffectedTestsSucceeded")
if (-not $runFallback.Success) {
    throw "The run branch must retain a full-test fallback for non-PR runs and affected-test failures."
}

if (-not $testTemplate.Contains("PublishCoverageReport")) {
    throw "Coverage publication must be limited to full-test runs."
}

Write-Output "Affected-test configuration and rollout wiring are valid."
