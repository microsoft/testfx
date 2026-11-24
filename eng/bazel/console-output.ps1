# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

param([switch] $SelfTest)

function Read-BazelPlainLog([string] $Path) {
    return [regex]::Replace([IO.File]::ReadAllText($Path), '\x1B\[[0-?]*[ -/]*[@-~]', '')
}

if ($SelfTest) {
    $consoleEvidenceBase = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'artifacts\bazel-validation'
    $consoleEvidence = Join-Path $consoleEvidenceBase ('console-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $consoleEvidence -Force
    $escape = [char]27
    $cases = @(
        @{ Name = 'PlainFailure'; Raw = 'failed TestMethod2'; Expected = 'failed TestMethod2' },
        @{ Name = 'ColoredFailure'; Raw = "${escape}[31mfailed${escape}[m TestMethod2"; Expected = 'failed TestMethod2' },
        @{ Name = 'MixedOutcomes'; Raw = "//:pass ${escape}[32mPASSED${escape}[m`n//:fail ${escape}[31mFAILED${escape}[0m"; Expected = "//:pass PASSED`n//:fail FAILED" },
        @{ Name = 'ZeroResults'; Raw = "${escape}[31mtests ran 0, minimum expected 1${escape}[0m"; Expected = 'tests ran 0, minimum expected 1' },
        @{ Name = 'PreservesContent'; Raw = "array[0] = $([char]0xE9)`r`n"; Expected = "array[0] = $([char]0xE9)`r`n" },
        @{ Name = 'RetainsTruncatedEscape'; Raw = "text${escape}[31"; Expected = "text${escape}[31" },
        @{ Name = 'EmptyLog'; Raw = ''; Expected = '' }
    )
    foreach ($case in $cases) {
        $consoleTestPath = Join-Path $consoleEvidence ($case.Name + '.log')
        [IO.File]::WriteAllText($consoleTestPath, $case.Raw)
        $hash = (Get-FileHash -LiteralPath $consoleTestPath -Algorithm SHA256).Hash
        if ((Read-BazelPlainLog $consoleTestPath) -cne $case.Expected) { throw "$($case.Name): unexpected plain-text log." }
        if ((Get-FileHash -LiteralPath $consoleTestPath -Algorithm SHA256).Hash -cne $hash) { throw "$($case.Name): raw log was changed." }
        Write-Host "Passed $($case.Name)"
    }
    Write-Host "Console output checks: $($cases.Count) passed; raw fixtures retained in $consoleEvidence."
}
