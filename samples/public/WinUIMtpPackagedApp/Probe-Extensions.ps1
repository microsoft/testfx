# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 5.1
[CmdletBinding()]
param(
    [string] $DotnetPath = (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    [string] $Configuration = 'Release',
    [string] $ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $PSScriptRoot 'TestResults\Extensions'
}

$sdk = Join-Path (Split-Path -Parent $DotnetPath) 'sdk\10.0.401\dotnet.dll'
if (-not (Test-Path -LiteralPath $DotnetPath -PathType Leaf) -or -not (Test-Path -LiteralPath $sdk -PathType Leaf)) {
    throw 'DotnetPath must point to an installation containing .NET SDK 10.0.401.'
}

$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
$null = New-Item -ItemType Directory -Path $ResultsDirectory -Force
$project = Join-Path $PSScriptRoot 'WinUIMtpPackagedApp.csproj'
$dependenciesPath = Join-Path $PSScriptRoot "bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\WinUIMtpPackagedApp.deps.json"
$dependencies = Get-Content -LiteralPath $dependenciesPath -Raw | ConvertFrom-Json
$packages = @($dependencies.libraries.PSObject.Properties.Name | ForEach-Object { $_.Split('/')[0] })
foreach ($extension in @(
    'AzureDevOpsReport', 'CodeCoverage', 'CrashDump', 'CtrfReport', 'Fakes', 'GitHubActionsReport',
    'HangDump', 'HotReload', 'HtmlReport', 'JUnitReport', 'OpenTelemetry', 'PackagedApp', 'Retry', 'TrxReport'
)) {
    if ($packages -cnotcontains "Microsoft.Testing.Extensions.$extension") {
        throw "The built sample is missing Microsoft.Testing.Extensions.$extension. Rebuild with all extensions enabled."
    }
}

$baseArguments = @(
    'exec', $sdk, 'test', '--project', $project, '--no-build', '-c', $Configuration,
    '-a', 'x64', '-p:Platform=x64', '-p:RuntimeIdentifier=win-x64'
)

function Invoke-ProbeCommand {
    param([string[]] $Arguments)

    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell turns redirected native stderr into error records. Keep it in the
        # diagnostic log, then classify the actual process exit code instead of losing the report.
        $ErrorActionPreference = 'Continue'
        $binlog = Join-Path $ResultsDirectory "native-$([Guid]::NewGuid().ToString('N')).binlog"
        $output = @(& $DotnetPath @baseArguments @Arguments "-bl:$binlog" 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    }
}

function Assert-SelectedTrx {
    param([string] $Path, [string] $TestName, [string] $Outcome = 'Passed')

    [xml] $trx = Get-Content -LiteralPath $Path -Raw
    $namespace = [Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $tests = @($trx.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $namespace))
    if ($tests.Count -ne 1 -or $tests[0].testName -cne $TestName -or $tests[0].outcome -cne $Outcome) {
        throw "Expected exactly one $Outcome result named '$TestName' in '$Path'."
    }
}

$probes = @(
    @{ Name = 'filter-trx'; Option = 'filter'; Arguments = @(); Artifact = '' },
    @{ Name = 'coverage'; Option = 'coverage'; Arguments = @('--coverage', '--coverage-output', 'coverage.cobertura.xml', '--coverage-output-format', 'cobertura'); Artifact = 'coverage.cobertura.xml' },
    @{ Name = 'hangdump'; Option = 'hangdump'; Arguments = @('--hangdump', '--hangdump-timeout', '30m'); Artifact = '' },
    @{ Name = 'retry'; Option = 'retry-failed-tests'; Arguments = @('--retry-failed-tests', '1'); Artifact = '' },
    @{ Name = 'crashdump'; Option = 'crashdump'; Arguments = @('--crashdump'); Artifact = '' },
    @{ Name = 'html'; Option = 'report-html'; Arguments = @('--report-html'); Artifact = '' },
    @{ Name = 'ctrf'; Option = 'report-ctrf'; Arguments = @('--report-ctrf'); Artifact = '' },
    @{ Name = 'junit'; Option = 'report-junit'; Arguments = @('--report-junit'); Artifact = '' },
    @{ Name = 'github'; Option = 'report-gh'; Arguments = @('--report-gh'); Artifact = '' },
    @{ Name = 'azdo'; Option = 'report-azdo'; Arguments = @('--report-azdo'); Artifact = '' }
)

$previousLanguage = $env:TESTINGPLATFORM_UI_LANGUAGE
$env:TESTINGPLATFORM_UI_LANGUAGE = 'en-US'
Push-Location $PSScriptRoot
try {
    $help = Invoke-ProbeCommand -Arguments @('--help')
    $help.Output | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'host-help.log') -Encoding UTF8
    if ($help.ExitCode -ne 0 -or $help.Output -notmatch '(?m)^\s+--filter(?:\s|$)') {
        throw "The activated host did not provide its native help. See '$ResultsDirectory\host-help.log'."
    }

    $results = [Collections.Generic.List[object]]::new()
    foreach ($probe in $probes) {
        $directory = Join-Path $ResultsDirectory "$($probe.Name)-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
        $null = New-Item -ItemType Directory -Path $directory
        $trx = Join-Path $directory "$($probe.Name).trx"
        $testName = if ($probe.Name -eq 'retry') { 'RetryFailsFirstAttempt' } else { 'PackageIdentityAndAumidMatchManifest' }
        $arguments = @(
            '--filter', "Name=$testName", '--report-trx', '--report-trx-filename', "$($probe.Name).trx",
            '--results-directory', $directory, '--timeout', '2m'
        ) + $probe.Arguments
        $run = Invoke-ProbeCommand -Arguments $arguments
        $run.Output | Set-Content -LiteralPath (Join-Path $directory 'run.log') -Encoding UTF8
        $advertised = $help.Output -match "(?m)^\s+--$([regex]::Escape($probe.Option))(?:\s|$)"
        $status = 'Failed'
        $detail = 'See run.log.'
        $coveredSampleLines = $null
        if (-not $advertised) {
            $detail = 'The enabled extension is missing from actual-host help.'
        }
        elseif ($run.ExitCode -ne 0 -and $run.Output.Contains("Unknown option '--$($probe.Option)'")) {
            $status = 'Unavailable'
            $detail = 'Advertised by the activated host, but rejected by the packaged controller.'
        }
        elseif ($run.ExitCode -eq 0) {
            try {
                Assert-SelectedTrx -Path $trx -TestName $testName
                if ($probe.Artifact) {
                    $artifact = Join-Path $directory $probe.Artifact
                    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf) -or (Get-Item -LiteralPath $artifact).Length -eq 0) {
                        throw "Missing or empty '$($probe.Artifact)'."
                    }

                    [xml] $coverage = Get-Content -LiteralPath $artifact -Raw
                    $sampleClasses = @($coverage.SelectNodes('//*[local-name()="class"]') |
                        Where-Object { $_.name -like '*WinUIMtpPackagedApp*' })
                    $coveredSampleLines = @($sampleClasses |
                        ForEach-Object { $_.SelectNodes('.//*[local-name()="line"]') } |
                        Where-Object { [int] $_.hits -gt 0 }).Count
                }

                if ($probe.Name -eq 'retry') {
                    $attempts = @(Get-ChildItem -LiteralPath (Join-Path $directory 'Retries') -Filter retry.trx -Recurse | Sort-Object FullName)
                    if ($attempts.Count -ne 2) {
                        throw 'Expected exactly two retry attempts.'
                    }

                    Assert-SelectedTrx -Path $attempts[0].FullName -TestName $testName -Outcome 'Failed'
                    Assert-SelectedTrx -Path $attempts[1].FullName -TestName $testName
                }

                $status = 'Verified'
                $detail = if ($probe.Name -eq 'hangdump') { 'Run succeeds with HangDump enabled; an actual hang/dump is not exercised.' }
                    elseif ($probe.Name -eq 'coverage') { "$coveredSampleLines sample lines have coverage hits; complete coverage accuracy is not asserted." }
                    else { 'Selected result and expected artifacts verified.' }
                if ($probe.Name -eq 'coverage' -and $coveredSampleLines -eq 0) {
                    $status = 'Partial'
                    $detail = 'Command succeeds and writes Cobertura, but no sample lines have coverage hits.'
                }
            }
            catch {
                $detail = $_.Exception.Message
            }
        }

        $results.Add([pscustomobject]@{ Extension = $probe.Name; Advertised = $advertised; Status = $status; ExitCode = $run.ExitCode; CoveredSampleLines = $coveredSampleLines; Directory = [IO.Path]::GetFileName($directory); Detail = $detail })
    }

    if ($results.Where({ $_.Extension -eq 'filter-trx' -and $_.Status -eq 'Verified' }).Count -eq 1) {
        $results.Add([pscustomobject]@{
            Extension = 'packagedapp'; Advertised = $null; Status = 'Verified'; ExitCode = 0; Directory = ''
            Detail = 'The selected test verifies actual package identity, AUMID, executable location and registration.'
        })
    }

    foreach ($scenario in @(
        @{ Name = 'hotreload'; Detail = 'Package present; interactive Hot Reload is not exercised.' },
        @{ Name = 'fakes'; Detail = 'Package present; no licensed shim scenario is exercised.' },
        @{ Name = 'opentelemetry'; Detail = 'Diagnostics producer enabled by the SDK hook; no exporter/backend is configured or exercised.' }
    )) {
        $results.Add([pscustomobject]@{
            Extension = $scenario.Name; Advertised = $null; Status = 'NotExercised'; ExitCode = $null; Directory = ''
            Detail = $scenario.Detail
        })
    }

    ConvertTo-Json -InputObject $results.ToArray() -Depth 3 |
        Set-Content -LiteralPath (Join-Path $ResultsDirectory 'extensions.json') -Encoding UTF8
    $results | Format-Table Extension, Advertised, Status, ExitCode, Detail -AutoSize -Wrap
    if (@($results | Where-Object { $_.Status -in @('Failed', 'Partial') }).Count -ne 0) {
        throw "An extension probe failed or produced incomplete evidence. See '$ResultsDirectory\extensions.json' and the per-extension logs."
    }
}
finally {
    Pop-Location
    $env:TESTINGPLATFORM_UI_LANGUAGE = $previousLanguage
}
