# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param([string] $EvidenceRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'These launcher checks exercise the Windows dogfood contract.' }
$root = if ($EvidenceRoot) { $EvidenceRoot } else { Join-Path ([IO.Path]::GetTempPath()) ('dogfood-runner-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $root) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $root | Out-Null
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$RunRoot = $root
$environment = @{}
$commands = [Collections.Generic.List[object]]::new()
$children = [Collections.Generic.List[object]]::new()
$results = [Collections.Generic.List[object]]::new()
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'dogfood.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Runner source does not parse.' }
foreach ($name in @('Start-RecordedProcess', 'Complete-RecordedProcess', 'Invoke-RecordedProcess', 'Invoke-RecordedScript')) {
    $definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    if ($null -eq $definition) { throw "Missing runner function $name." }
    . ([scriptblock]::Create($definition.Extent.Text))
}

function Assert-Equal($Expected, $Actual) {
    if ($Expected -cne $Actual) { throw "Expected <$Expected>, got <$Actual>." }
}

function Test-Case([string] $Name, [scriptblock] $Body) {
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Error = $null })
        Write-Host "PASS $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Error = $_.Exception.Message })
        Write-Host "FAIL $Name`: $($_.Exception.Message)"
    }
}

$argumentsFixture = Join-Path $root 'arguments.ps1'
@'
[Console]::WriteLine((ConvertTo-Json -InputObject @($args) -Compress))
'@ | Set-Content -LiteralPath $argumentsFixture -Encoding utf8
$parametersFixture = Join-Path $root 'parameters.ps1'
@'
param([switch] $Restore, [string] $Configuration, [string] $Projects, [string[]] $Properties)
[ordered]@{ Restore = $Restore.IsPresent; Configuration = $Configuration; Projects = $Projects; Properties = $Properties } | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $parametersFixture -Encoding utf8

try {
    Test-Case 'ScriptPreservesColonBearingBazelOptions' {
        $expected = @('--output_base=Q:\path with spaces!\out', '--@rules_dotnet//dotnet:target_framework=net10.0', '//:protocol_contract_tests', '--disk_cache=Q:\cache')
        $result = Invoke-RecordedScript -Name 'arguments' -PowerShell $pwsh -Script $argumentsFixture -Arguments $expected
        Assert-Equal 1 @($result).Count
        Assert-Equal 0 $result.ExitCode
        $actual = @(Get-Content -LiteralPath $result.Stdout -Raw | ConvertFrom-Json)
        Assert-Equal 4 $actual.Count
        for ($index = 0; $index -lt $expected.Count; $index++) { Assert-Equal $expected[$index] $actual[$index] }
    }
    Test-Case 'WindowsPowerShellPreservesNamedBuildParameters' {
        $properties = @('/m:8', '/p:TargetFramework=net10.0', '/p:ArtifactsBinDir=Q:\path with spaces!\bin\', '/bl:Q:\logs\unique.binlog')
        $result = Invoke-RecordedScript -Name 'parameters' -PowerShell $windowsPowerShell -Script $parametersFixture `
            -Parameters @{ Restore = $true; Configuration = 'Release'; Projects = 'Q:\inputs\unit-tests.slnf'; Properties = $properties }
        Assert-Equal 0 $result.ExitCode
        $actual = Get-Content -LiteralPath $result.Stdout -Raw | ConvertFrom-Json
        Assert-Equal $true $actual.Restore
        Assert-Equal 'Release' $actual.Configuration
        Assert-Equal 'Q:\inputs\unit-tests.slnf' $actual.Projects
        Assert-Equal 4 @($actual.Properties).Count
        for ($index = 0; $index -lt $properties.Count; $index++) { Assert-Equal $properties[$index] $actual.Properties[$index] }
    }
    Test-Case 'ProcessPreservesFailureExitAndBothLogs' {
        $fixture = Join-Path $root 'failure.ps1'
        @'
[Console]::Out.WriteLine('stdout marker')
[Console]::Error.WriteLine('stderr marker')
exit 7
'@ | Set-Content -LiteralPath $fixture -Encoding utf8
        $result = Invoke-RecordedScript -Name 'failure' -PowerShell $pwsh -Script $fixture
        Assert-Equal 7 $result.ExitCode
        Assert-Equal 'stdout marker' (Get-Content -LiteralPath $result.Stdout -Raw).Trim()
        Assert-Equal 'stderr marker' (Get-Content -LiteralPath $result.Stderr -Raw).Trim()
        Assert-Equal 0 $children.Count
    }
    Test-Case 'ProcessStreamsLogsBeforeExitAndJoinsTheChild' {
        $fixture = Join-Path $root 'rendezvous.ps1'
        $release = Join-Path $root 'release'
        @'
param([string] $Release)
[Console]::Out.WriteLine('started marker')
[Console]::Out.Flush()
while (-not (Test-Path -LiteralPath $Release)) { Start-Sleep -Milliseconds 25 }
[Console]::Out.WriteLine('completed marker')
'@ | Set-Content -LiteralPath $fixture -Encoding utf8
        $child = Start-RecordedProcess 'streaming' $pwsh @('-NoProfile', '-File', $fixture, '-Release', $release)
        $joined = $false
        try {
            # The marker is the rendezvous; the allowance only bounds a stalled
            # child startup on a loaded Windows agent, not a performance assertion.
            $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
            while ((Get-Item -LiteralPath $child.Stdout).Length -eq 0) {
                if ($child.Process.HasExited -or [DateTimeOffset]::UtcNow -gt $deadline) { throw 'The child did not stream its startup marker.' }
                Start-Sleep -Milliseconds 25
            }
            Assert-Equal 'started marker' (Get-Content -LiteralPath $child.Stdout -Raw).Trim()
            Assert-Equal $false $child.Process.HasExited
            [IO.File]::WriteAllText($release, 'release')
            $result = Complete-RecordedProcess $child
            $joined = $true
            Assert-Equal 0 $result.ExitCode
            Assert-Equal (@('started marker', 'completed marker') -join "`n") ((Get-Content -LiteralPath $result.Stdout) -join "`n")
            Assert-Equal 0 $children.Count
        }
        finally {
            if (-not $joined) {
                [IO.File]::WriteAllText($release, 'release')
                if (-not $child.Process.WaitForExit(30000)) { $child.Process.Kill($true) }
                $null = Complete-RecordedProcess $child
            }
        }
    }
}
finally {
    foreach ($child in $children.ToArray()) {
        if (-not $child.Process.HasExited) { $child.Process.Kill($true) }
        $null = Complete-RecordedProcess $child
    }
    $report = [ordered]@{ Total = $results.Count; Passed = @($results | Where-Object Passed).Count; Tests = $results }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding utf8
    Write-Host "Runner evidence: $root"
}
if ($results.Count -ne 4 -or @($results | Where-Object { -not $_.Passed }).Count) { exit 1 }
exit 0
