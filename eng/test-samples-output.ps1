[CmdletBinding()]
param(
    [string]$ToolsPath,
    [string]$OutputDirectory,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
if (!$ToolsPath) {
    $ToolsPath = Join-Path $PSScriptRoot "samples-tools.ps1"
}

. $ToolsPath

function Assert-SampleOutputEqual {
    param($Expected, $Actual, [string]$Message)

    if ($Expected -cne $Actual) {
        throw "${Message}: expected '$Expected', actual '$Actual'."
    }
}

function Invoke-SampleOutputCheck {
    param([string]$Name, [scriptblock]$Check)

    try {
        & $Check
        Write-Host "PASS $Name"
        $script:passed++
    }
    catch {
        Write-Host "FAIL ${Name}: $($_.Exception.Message)"
        $script:failed++
    }
}

if (!$OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\tmp\sample-output-tests"
}

$testRoot = Join-Path $OutputDirectory ([guid]::NewGuid().ToString("N"))
$testRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($testRoot)
$null = [System.IO.Directory]::CreateDirectory($testRoot)
$fixture = Join-Path $testRoot "sample fixture.ps1"
$shell = if (Test-Path (Join-Path $PSHOME "powershell.exe")) {
    Join-Path $PSHOME "powershell.exe"
} else {
    Join-Path $PSHOME "pwsh.exe"
}

$fixtureSource = @'
param([string]$Scenario, [string]$StatePath, [string]$Stream, [string]$Failure)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
switch ($Scenario) {
    "success" {
        [Console]::Out.WriteLine("sample stdout")
        [Console]::Error.WriteLine("sample stderr")
    }
    "failure" {
        [Console]::Out.WriteLine("before failure")
        [Console]::Error.WriteLine("failure details")
        exit 17
    }
    "large" {
        for ($i = 0; $i -lt 2048; $i++) {
            [Console]::Out.WriteLine("stdout-$i")
            [Console]::Error.WriteLine("stderr-$i")
        }
    }
    "unicode" {
        [Console]::Out.WriteLine("stdout-" + [char]0xE9)
        [Console]::Error.WriteLine("stderr-" + [char]0x3BB)
    }
    "arguments" {
        $nativeArguments = [Environment]::GetCommandLineArgs()
        $scenarioIndex = [Array]::IndexOf($nativeArguments, "arguments")
        $received = $nativeArguments[($scenarioIndex + 1)..($nativeArguments.Length - 1)]
        [Console]::Out.WriteLine((ConvertTo-Json -InputObject $received -Compress))
    }
    "retry" {
        $attempt = if (Test-Path -LiteralPath $StatePath) { [int][System.IO.File]::ReadAllText($StatePath) + 1 } else { 1 }
        [System.IO.File]::WriteAllText($StatePath, $attempt.ToString())
        if ($attempt -gt 1 -and $Failure -eq "Once") {
            [Console]::Out.WriteLine("retry succeeded")
            break
        }

        if ($attempt -gt 1 -and $Failure -eq "RetryFailure") {
            [Console]::Error.WriteLine("retry failed")
            exit 23
        }

        $message = switch ($Failure) {
            "Ordinary" { "ordinary failure" }
            "NearMarker" { "Handshake failure: 1" }
            default { "Handshake failures: 1" }
        }
        if ($Stream -in @("stdout", "both")) { [Console]::Out.WriteLine($message) }
        if ($Stream -in @("stderr", "both")) { [Console]::Error.WriteLine($message) }
        if ($Failure -ne "Success") { exit 17 }
    }
    default { throw "Unknown fixture scenario '$Scenario'." }
}
exit 0
'@
[System.IO.File]::WriteAllText($fixture, $fixtureSource)
$passed = 0
$failed = 0
$completed = $false

function Start-Sleep {
    param([int]$Seconds)

    $script:retryDelays += $Seconds
}

try {
    Invoke-SampleOutputCheck "TranscriptNames_PreserveDirectoryAndPunctuationIdentity" {
        $cases = @(
            @{ Project = "group.a/Sample.csproj"; Expected = "Sample.csproj.66b9b4ff875c2e4e.Run.attempt1" },
            @{ Project = "group/a/Sample.csproj"; Expected = "Sample.csproj.292851069d62d86b.Run.attempt1" },
            @{ Project = "other/Sample.csproj"; Expected = "Sample.csproj.004ca9d2c2f3a1c4.Run.attempt1" },
            @{ Project = "group/Sample+Tests.csproj"; Expected = "Sample_Tests.csproj.438db73532b21576.Run.attempt1" },
            @{ Project = "group/Sample Tests.csproj"; Expected = "Sample_Tests.csproj.6ab19b9fc3300f12.Run.attempt1" }
        )
        foreach ($case in $cases) {
            Assert-SampleOutputEqual $case.Expected (Get-SampleOutputLogName -Project $case.Project -Mode "Run" -Attempt 1) $case.Project
        }
    }

    Invoke-SampleOutputCheck "TranscriptNames_NormalizePathSeparators" {
        Assert-SampleOutputEqual "Sample.csproj.292851069d62d86b.Run.attempt1" `
            (Get-SampleOutputLogName -Project "group\a\Sample.csproj" -Mode "Run" -Attempt 1) "Windows separators"
    }

    Invoke-SampleOutputCheck "TranscriptNames_DistinguishModesAndAttempts" {
        Assert-SampleOutputEqual "Sample.csproj.292851069d62d86b.VSTest.attempt1" `
            (Get-SampleOutputLogName -Project "group/a/Sample.csproj" -Mode "VSTest" -Attempt 1) "mode"
        Assert-SampleOutputEqual "Sample.csproj.292851069d62d86b.Run.attempt2" `
            (Get-SampleOutputLogName -Project "group/a/Sample.csproj" -Mode "Run" -Attempt 2) "attempt"
    }

    Invoke-SampleOutputCheck "TranscriptNames_RespectReadableNameBoundaries" {
        $cases = @(
            @{ Length = 32; Expected = ("a" * 32) + ".csproj.6d097e6876256b89.Run.attempt1" },
            @{ Length = 33; Expected = ("a" * 33) + ".csproj.a5ee86049be0e511.Run.attempt1" },
            @{ Length = 34; Expected = ("a" * 34) + ".cspro.482b472b79fe1a75.Run.attempt1" }
        )
        foreach ($case in $cases) {
            $project = "folder/$("a" * $case.Length).csproj"
            Assert-SampleOutputEqual $case.Expected (Get-SampleOutputLogName -Project $project -Mode "Run" -Attempt 1) "readable name boundary"
        }
    }

    Invoke-SampleOutputCheck "TranscriptNames_BoundDeepLongPaths" {
        $project = ("long-directory/" * 20) + ("p" * 100) + ".csproj"
        $name = Get-SampleOutputLogName -Project $project -Mode "VisualStudioInvoke" -Attempt 2
        Assert-SampleOutputEqual (("p" * 40) + ".9fe0bd391238c04d.VisualStudioInvoke.attempt2") $name "bounded name"
        Assert-SampleOutputEqual 96 "$name.stdout.log".Length "maximum transcript filename"
    }

    Invoke-SampleOutputCheck "SuccessfulCommand_PreservesExitAndBothStreams" {
        $result = Invoke-SampleProcess -FilePath $shell -Arguments @("-NoProfile", "-File", $fixture, "success") `
            -LogFilePrefix (Join-Path $testRoot "successful") -Description "success" 6>$null
        Assert-SampleOutputEqual 0 $result.ExitCode "exit"
        Assert-SampleOutputEqual "sample stdout$([Environment]::NewLine)" ([System.IO.File]::ReadAllText($result.StandardOutputPath)) "stdout"
        Assert-SampleOutputEqual "sample stderr$([Environment]::NewLine)" ([System.IO.File]::ReadAllText($result.StandardErrorPath)) "stderr"
    }

    Invoke-SampleOutputCheck "SuccessfulCommand_PrintsOnlyItsSummary" {
        $messages = @()
        $null = Invoke-SampleProcess -FilePath $shell -Arguments @("-NoProfile", "-File", $fixture, "success") `
            -LogFilePrefix (Join-Path $testRoot "quiet") -Description "quiet-success" -InformationVariable messages 6>$null
        Assert-SampleOutputEqual 1 $messages.Count "message count"
        if ($messages[0].ToString() -notmatch '^PASS: quiet-success \(\d+\.\ds, exit 0\)\. Transcripts: ') {
            throw "Missing attributed result, duration, exit, or transcript location."
        }
    }

    Invoke-SampleOutputCheck "FailedCommand_ReplaysBothStreamsAndPreservesExit" {
        $messages = @()
        $result = Invoke-SampleProcess -FilePath $shell -Arguments @("-NoProfile", "-File", $fixture, "failure") `
            -LogFilePrefix (Join-Path $testRoot "failed") -Description "failure" -InformationVariable messages 6>$null
        Assert-SampleOutputEqual 17 $result.ExitCode "exit"
        Assert-SampleOutputEqual 17 $global:LASTEXITCODE "native exit"
        Assert-SampleOutputEqual "before failure$([Environment]::NewLine)" ([System.IO.File]::ReadAllText($result.StandardOutputPath)) "stdout"
        Assert-SampleOutputEqual "failure details$([Environment]::NewLine)" ([System.IO.File]::ReadAllText($result.StandardErrorPath)) "stderr"
        $expectedReplay = @("--- stdout: $($result.StandardOutputPath) ---", "before failure", "--- stderr: $($result.StandardErrorPath) ---", "failure details") -join "`n"
        Assert-SampleOutputEqual $expectedReplay (($messages | Select-Object -Skip 1 | ForEach-Object { $_.ToString() }) -join "`n") "replay"
        if ($messages[0].ToString() -notmatch '^FAIL: failure \(\d+\.\ds, exit 17\)\. Transcripts: ') {
            throw "Missing failed-attempt summary."
        }
    }

    Invoke-SampleOutputCheck "LargeConcurrentStreams_AreDrainedBeforeReturning" {
        $result = Invoke-SampleProcess -FilePath $shell -Arguments @("-NoProfile", "-File", $fixture, "large") `
            -LogFilePrefix (Join-Path $testRoot "large") -Description "large" 6>$null
        Assert-SampleOutputEqual 0 $result.ExitCode "exit"
        $expectedStdout = ((0..2047 | ForEach-Object { "stdout-$_" }) -join [Environment]::NewLine) + [Environment]::NewLine
        $expectedStderr = ((0..2047 | ForEach-Object { "stderr-$_" }) -join [Environment]::NewLine) + [Environment]::NewLine
        Assert-SampleOutputEqual $expectedStdout ([System.IO.File]::ReadAllText($result.StandardOutputPath)) "stdout"
        Assert-SampleOutputEqual $expectedStderr ([System.IO.File]::ReadAllText($result.StandardErrorPath)) "stderr"
    }

    Invoke-SampleOutputCheck "NativeArguments_PreserveEmptyQuotesAndTrailingBackslash" {
        $arguments = @("before", "", "with spaces", 'with "quotes"', "C:\path with spaces\", "after")
        $result = Invoke-SampleProcess -FilePath $shell -Arguments (@("-NoProfile", "-File", $fixture, "arguments") + $arguments) `
            -LogFilePrefix (Join-Path $testRoot "arguments") -Description "arguments" 6>$null
        Assert-SampleOutputEqual 0 $result.ExitCode "exit"
        $expectedArguments = (ConvertTo-Json -InputObject $arguments -Compress) + [Environment]::NewLine
        Assert-SampleOutputEqual $expectedArguments ([System.IO.File]::ReadAllText($result.StandardOutputPath)) "values"
    }

    Invoke-SampleOutputCheck "UnicodeOutput_IsPreservedInBothStreams" {
        $result = Invoke-SampleProcess -FilePath $shell -Arguments @("-NoProfile", "-File", $fixture, "unicode") `
            -LogFilePrefix (Join-Path $testRoot "unicode") -Description "unicode" 6>$null
        Assert-SampleOutputEqual 0 $result.ExitCode "exit"
        Assert-SampleOutputEqual ("stdout-" + [char]0xE9 + [Environment]::NewLine) ([System.IO.File]::ReadAllText($result.StandardOutputPath)) "stdout"
        Assert-SampleOutputEqual ("stderr-" + [char]0x3BB + [Environment]::NewLine) ([System.IO.File]::ReadAllText($result.StandardErrorPath)) "stderr"
    }

    Invoke-SampleOutputCheck "EmptyArgumentList_LaunchesTheCommand" {
        $result = Invoke-SampleProcess -FilePath (Join-Path $env:SystemRoot "System32\where.exe") `
            -LogFilePrefix (Join-Path $testRoot "no-arguments") -Description "no-arguments" 6>$null
        Assert-SampleOutputEqual 2 $result.ExitCode "exit"
    }

    Invoke-SampleOutputCheck "LiveCommandLaunchFailure_DoesNotReusePreviousExit" {
        $missingCommand = Join-Path $testRoot "missing.exe"
        $global:LASTEXITCODE = 0
        $script:retryDelays = @()
        $failure = $null
        try {
            Invoke-SampleProcessWithRetry -FilePath $missingCommand -Project "missing.csproj" -Mode "Run" -RetryHandshakeFailure $true |
                Out-Null
        }
        catch {
            $failure = $_.Exception
        }

        Assert-SampleOutputEqual "System.Management.Automation.CommandNotFoundException" $failure.GetType().FullName "launch failure"
        Assert-SampleOutputEqual $missingCommand $failure.CommandName "failed command"
        Assert-SampleOutputEqual 0 $script:retryDelays.Count "no retry on launch failure"
        Assert-SampleOutputEqual "Stop" $ErrorActionPreference "error preference"
    }

    $retryCases = @(
        @{ Name = "CapturedStdoutHandshake_RetriesOnce"; Capture = $true; Stream = "stdout"; Failure = "Once"; Retry = $true; Attempts = 2; Exit = 0 },
        @{ Name = "CapturedStderrHandshake_RetriesOnce"; Capture = $true; Stream = "stderr"; Failure = "Once"; Retry = $true; Attempts = 2; Exit = 0 },
        @{ Name = "CapturedBothStreamsHandshake_RetriesOnce"; Capture = $true; Stream = "both"; Failure = "Once"; Retry = $true; Attempts = 2; Exit = 0 },
        @{ Name = "CapturedRepeatedHandshake_StopsAfterSecondAttempt"; Capture = $true; Stream = "stderr"; Failure = "Always"; Retry = $true; Attempts = 2; Exit = 17 },
        @{ Name = "CapturedRetryFailure_PreservesSecondExit"; Capture = $true; Stream = "stderr"; Failure = "RetryFailure"; Retry = $true; Attempts = 2; Exit = 23 },
        @{ Name = "CapturedStdoutMarkerOnSuccess_DoesNotRetry"; Capture = $true; Stream = "stdout"; Failure = "Success"; Retry = $true; Attempts = 1; Exit = 0 },
        @{ Name = "CapturedStderrMarkerOnSuccess_DoesNotRetry"; Capture = $true; Stream = "stderr"; Failure = "Success"; Retry = $true; Attempts = 1; Exit = 0 },
        @{ Name = "CapturedStderrHandshakeWithoutOptIn_DoesNotRetry"; Capture = $true; Stream = "stderr"; Failure = "Once"; Retry = $false; Attempts = 1; Exit = 17 },
        @{ Name = "CapturedOrdinaryStderrFailure_DoesNotRetry"; Capture = $true; Stream = "stderr"; Failure = "Ordinary"; Retry = $true; Attempts = 1; Exit = 17 },
        @{ Name = "CapturedNearMarker_DoesNotRetry"; Capture = $true; Stream = "stderr"; Failure = "NearMarker"; Retry = $true; Attempts = 1; Exit = 17 },
        @{ Name = "CapturedEmptyStreams_DoesNotRetry"; Capture = $true; Stream = "none"; Failure = "Once"; Retry = $true; Attempts = 1; Exit = 17 },
        @{ Name = "LiveStdoutHandshake_RetriesOnce"; Capture = $false; Stream = "stdout"; Failure = "Once"; Retry = $true; Attempts = 2; Exit = 0; Output = @("Handshake failures: 1", "retry succeeded") },
        @{ Name = "LiveStderrHandshake_RetriesOnce"; Capture = $false; Stream = "stderr"; Failure = "Once"; Retry = $true; Attempts = 2; Exit = 0; Output = @("Handshake failures: 1", "retry succeeded") },
        @{ Name = "LiveRepeatedStderrHandshake_StopsAfterSecondAttempt"; Capture = $false; Stream = "stderr"; Failure = "Always"; Retry = $true; Attempts = 2; Exit = 17; Output = @("Handshake failures: 1", "Handshake failures: 1") },
        @{ Name = "LiveRetryFailure_PreservesSecondExit"; Capture = $false; Stream = "stderr"; Failure = "RetryFailure"; Retry = $true; Attempts = 2; Exit = 23; Output = @("Handshake failures: 1", "retry failed") },
        @{ Name = "LiveStderrMarkerOnSuccess_DoesNotRetry"; Capture = $false; Stream = "stderr"; Failure = "Success"; Retry = $true; Attempts = 1; Exit = 0; Output = @("Handshake failures: 1") },
        @{ Name = "LiveStdoutHandshakeWithoutOptIn_DoesNotRetry"; Capture = $false; Stream = "stdout"; Failure = "Once"; Retry = $false; Attempts = 1; Exit = 17; Output = @("Handshake failures: 1") },
        @{ Name = "LiveOrdinaryStderrFailure_DoesNotRetry"; Capture = $false; Stream = "stderr"; Failure = "Ordinary"; Retry = $true; Attempts = 1; Exit = 17; Output = @("ordinary failure") },
        @{ Name = "LiveNearMarker_DoesNotRetry"; Capture = $false; Stream = "stderr"; Failure = "NearMarker"; Retry = $true; Attempts = 1; Exit = 17; Output = @("Handshake failure: 1") }
    )
    $caseIndex = 0
    foreach ($case in $retryCases) {
        $caseIndex++
        Invoke-SampleOutputCheck $case.Name {
            $caseDirectory = Join-Path $testRoot "retry-$caseIndex"
            $null = [System.IO.Directory]::CreateDirectory($caseDirectory)
            $statePath = Join-Path $caseDirectory "attempts.txt"
            $captureDirectory = if ($case.Capture) { Join-Path $caseDirectory "logs" } else { $null }
            $project = "nested/sample-output/Fixture.csproj"
            $messages = @()
            $warnings = @()
            $output = [System.Collections.Generic.List[object]]::new()
            $script:retryDelays = @()
            $failure = $null
            try {
                Invoke-SampleProcessWithRetry -FilePath $shell `
                    -Arguments @("-NoProfile", "-File", $fixture, "retry", $statePath, $case.Stream, $case.Failure) `
                    -Project $project -Mode "MtpTest" -OutputDirectory $captureDirectory -RetryHandshakeFailure $case.Retry `
                    -InformationVariable messages -WarningVariable warnings 6>$null 3>$null |
                    ForEach-Object { $output.Add($_) }
            }
            catch {
                $failure = $_.Exception.Message
            }

            $expectedFailure = if ($case.Exit -ne 0) { "Sample validation failed for '$project' with exit code $($case.Exit)." } else { $null }
            Assert-SampleOutputEqual $expectedFailure $failure "failure"
            Assert-SampleOutputEqual $case.Exit $global:LASTEXITCODE "native exit"
            Assert-SampleOutputEqual $case.Attempts ([int][System.IO.File]::ReadAllText($statePath)) "attempt count"
            Assert-SampleOutputEqual "Stop" $ErrorActionPreference "error preference"
            $expectedDelays = if ($case.Attempts -eq 2) { "5" } else { "" }
            Assert-SampleOutputEqual $expectedDelays ($script:retryDelays -join ",") "retry delay"
            Assert-SampleOutputEqual ($case.Attempts - 1) $warnings.Count "retry warning count"
            if ($case.Attempts -eq 2) {
                Assert-SampleOutputEqual "Test host handshake failed for '$project' on attempt 1; retrying once." $warnings[0].ToString() "warning"
            }

            if ($case.Capture) {
                Assert-SampleOutputEqual 0 $output.Count "captured output stays off the live stream"
                $expectedNames = @(foreach ($attempt in 1..$case.Attempts) {
                    "Fixture.csproj.577e7bc9c1f69fcd.MtpTest.attempt$attempt.stderr.log"
                    "Fixture.csproj.577e7bc9c1f69fcd.MtpTest.attempt$attempt.stdout.log"
                }) -join "`n"
                $actualNames = (Get-ChildItem -LiteralPath $captureDirectory -File | Sort-Object Name | ForEach-Object { $_.Name }) -join "`n"
                Assert-SampleOutputEqual $expectedNames $actualNames "transcript names"
                $firstExit = if ($case.Failure -eq "Success") { 0 } else { 17 }
                $firstStatus = if ($firstExit -eq 0) { "PASS" } else { "FAIL" }
                $maxAttempts = if ($case.Retry) { 2 } else { 1 }
                $prefix = Join-Path $captureDirectory "Fixture.csproj.577e7bc9c1f69fcd.MtpTest.attempt1"
                $expectedSummary = "${firstStatus}: $project (MtpTest; attempt 1/$maxAttempts) (DURATION, exit $firstExit). Transcripts: $prefix.{stdout,stderr}.log"
                $actualSummary = $messages[0].ToString() -replace '\(\d+\.\ds, exit ', '(DURATION, exit '
                Assert-SampleOutputEqual $expectedSummary $actualSummary "full project identity in summary"
            }
            else {
                Assert-SampleOutputEqual ($case.Output -join "`n") (($output | ForEach-Object { $_.ToString() }) -join "`n") "live output"
            }
        }
    }

    $completed = $true
}
finally {
    if ($completed -and $failed -eq 0 -and !$KeepArtifacts) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
    else {
        Write-Host "Sample-output check artifacts retained at: $testRoot"
    }
}

Write-Host "Sample-output checks: $passed passed, $failed failed, 0 skipped."
if ($failed -ne 0) {
    exit 1
}

exit 0
