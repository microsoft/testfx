[CmdletBinding()]
param(
    [string]$ToolsPath
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

$testRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\tmp\sample-output-tests\$([guid]::NewGuid().ToString('N'))"
$null = [System.IO.Directory]::CreateDirectory($testRoot)
$fixture = Join-Path $testRoot "sample fixture.ps1"
$shell = if (Test-Path (Join-Path $PSHOME "powershell.exe")) {
    Join-Path $PSHOME "powershell.exe"
} else {
    Join-Path $PSHOME "pwsh.exe"
}

$fixtureSource = @'
param([string]$Scenario)
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
    default { throw "Unknown fixture scenario '$Scenario'." }
}
exit 0
'@
[System.IO.File]::WriteAllText($fixture, $fixtureSource)
$passed = 0
$failed = 0
$completed = $false

try {
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

    $completed = $true
}
finally {
    if ($completed -and $failed -eq 0) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
    else {
        Write-Host "Failed-check artifacts retained at: $testRoot"
    }
}

Write-Host "Sample-output checks: $passed passed, $failed failed, 0 skipped."
if ($failed -ne 0) {
    exit 1
}

exit 0
