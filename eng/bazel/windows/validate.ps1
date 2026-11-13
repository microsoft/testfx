# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param([string] $ArtifactsDirectory)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'Windows consumer validation must run on Windows.' }
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$outputBase = & $bazel info output_base
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the isolated Bazel output root.' }
$outputBase = $outputBase.Replace('/', '\')
if (-not $ArtifactsDirectory) {
    $ArtifactsDirectory = Join-Path (Split-Path $outputBase -Parent) 'windows-validation'
}
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = Join-Path $ArtifactsDirectory $run
New-Item -ItemType Directory -Path $directory | Out-Null
$steps = [Collections.Generic.List[object]]::new()
$consumers = [Collections.Generic.List[object]]::new()
$succeeded = $false
$inputHashes = @(Get-FileHash -Algorithm SHA256 -LiteralPath @(
    (Join-Path $root 'MODULE.bazel'), (Join-Path $root 'BUILD.bazel'),
    (Join-Path $root 'eng\bazel\windows.bzl'), (Join-Path $root 'eng\bazel\patches\windows-targeting.patch'),
    (Join-Path $root 'eng\bazel\patches\public-signing.patch'), (Join-Path $root 'eng\bazel\defs.bzl'),
    (Join-Path $PSScriptRoot 'paket.lock'), (Join-Path $PSScriptRoot 'LibraryProbe.cs'),
    (Join-Path $PSScriptRoot 'targeting_tests.bzl'), (Join-Path $PSScriptRoot 'asset_selection_tests.bzl'),
    (Join-Path $PSScriptRoot 'winrt.globalconfig'), (Join-Path $PSScriptRoot 'windows.MODULE.bazel')
))

function Invoke-BazelStep([string] $Name, [string[]] $Arguments) {
    $log = Join-Path $directory "$Name.log"
    & $bazel @Arguments *> $log
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ Name = $Name; Arguments = $Arguments; ExitCode = $code })
    if ($code -ne 0) {
        Get-Content -LiteralPath $log -Tail 50 | Write-Host
        throw "$Name returned $code. See $log"
    }
    Write-Host "$Name`: exit $code"
}

function Invoke-Consumer([string] $Name, [string] $Executable, [string] $Layout) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($Executable)
    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = $Layout
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['DOTNET_ROOT'] = Join-Path $directory 'missing-runtime'
    $startInfo.Environment['DOTNET_ROOT_X64'] = $startInfo.Environment['DOTNET_ROOT']
    $startInfo.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $process = [Diagnostics.Process]::Start($startInfo)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill($true)
            if (-not $process.WaitForExit(30000)) { throw "$Name did not terminate after cancellation." }
            throw "$Name exceeded the two-minute consumer timeout."
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        $output + $errorOutput | Set-Content -LiteralPath (Join-Path $directory "$Name.log") -Encoding utf8
        $steps.Add([pscustomobject]@{ Name = $Name; Executable = $Executable; ExitCode = $process.ExitCode })
        if ($process.ExitCode -ne 0) { throw "$Name returned $($process.ExitCode). See its retained log." }
        $matches = [regex]::Matches($output, '(?m)^BAZEL_WINDOWS_PROBE: ([^\r\n]+)')
        if ($matches.Count -ne 1) { throw "$Name did not emit exactly one Windows consumer report." }
        return $matches[0].Groups[1].Value | ConvertFrom-Json
    }
    finally {
        $process.Dispose()
    }
}

$probes = @(
    @{ Name = 'windows_probe_packaged_net8'; Scenario = 'packaged'; Major = 8; Platform = '10.0.19041.0' },
    @{ Name = 'windows_probe_packaged_net9'; Scenario = 'packaged'; Major = 9; Platform = '10.0.19041.0' },
    @{ Name = 'windows_probe_packaged_net10'; Scenario = 'packaged'; Major = 10; Platform = '10.0.19041.0' },
    @{ Name = 'windows_probe_packaged_net11'; Scenario = 'packaged'; Major = 11; Platform = '10.0.19041.0' },
    @{ Name = 'windows_probe_desktop'; Scenario = 'desktop'; Major = 8; Platform = '7.0' },
    @{ Name = 'windows_probe_uwp'; Scenario = 'uwp'; Major = 10; Platform = '10.0.17763.0' },
    @{ Name = 'windows_probe_winui'; Scenario = 'winui'; Major = 10; Platform = '10.0.18362.0' }
)
Push-Location $root
try {
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source revision.' }
    $sourceState = @(git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the working tree state.' }
    git diff --binary HEAD "--output=$(Join-Path $directory 'source.patch')"
    if ($LASTEXITCODE -ne 0) { throw 'Could not preserve the tracked source patch.' }
    Invoke-BazelStep 'windows-build' @('build', '//:windows_libraries', '//:windows_probes', '--jobs=2', '--noshow_progress', '--show_result=0')
    Invoke-BazelStep 'windows-targeting' @('test', '//:windows_targeting_test', '//:windows_asset_selection_test', '--jobs=2', '--nocache_test_results', '--noshow_progress', '--show_result=0')
    $executionRoot = & $bazel info execution_root
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
    $executionRoot = $executionRoot.Replace('/', '\')
    $testLogs = & $bazel info bazel-testlogs
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the analysis-test report.' }
    foreach ($test in @('windows_targeting_test', 'windows_asset_selection_test')) {
        $testReport = Join-Path $testLogs.Replace('/', '\') "$test\test.xml"
        [xml]$testXml = Get-Content -LiteralPath $testReport -Raw
        if (@($testXml.SelectNodes('//testcase')).Count -ne 1 -or @($testXml.SelectNodes('//failure | //error | //skipped')).Count -ne 0) {
            throw "$test did not execute successfully."
        }
        Copy-Item -LiteralPath $testReport -Destination (Join-Path $directory "$test.xml")
    }

    foreach ($probe in $probes) {
        $paths = @(& $bazel cquery "//:$($probe.Name)" --output=files --noshow_progress)
        if ($LASTEXITCODE -ne 0) { throw "Could not locate $($probe.Name)'s declared publish files." }
        $executables = @($paths | Where-Object { $_.EndsWith('/TestFx.Bazel.WindowsProbe.exe', [StringComparison]::Ordinal) })
        if ($executables.Count -ne 1) { throw "Expected one real apphost for $($probe.Name)." }
        $original = Join-Path $executionRoot $executables[0].Replace('/', '\')
        $originalLayout = Split-Path $original -Parent
        $layout = Join-Path $directory "$($probe.Name) with spaces!"
        New-Item -ItemType Directory -Path $layout | Out-Null
        foreach ($path in $paths) {
            $source = Join-Path $executionRoot $path.Replace('/', '\')
            $relative = [IO.Path]::GetRelativePath($originalLayout, $source)
            if ($relative.StartsWith('..') -or [IO.Path]::IsPathRooted($relative)) {
                throw "Publish file is outside the declared apphost layout: $source"
            }
            $destination = Join-Path $layout $relative
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
        }
        $report = Invoke-Consumer $probe.Name (Join-Path $layout 'TestFx.Bazel.WindowsProbe.exe') $layout
        if ($report.Scenario -cne $probe.Scenario -or ([Version]$report.Runtime).Major -ne $probe.Major) {
            throw "Consumer scenario/runtime mismatch for $($probe.Name)."
        }
        $expectedCount = if ($probe.Scenario -eq 'packaged') { 2 } elseif ($probe.Scenario -eq 'desktop') { 3 } else { 4 }
        if (@($report.SourceAssemblies).Count -ne $expectedCount) { throw "Missing source assembly evidence for $($probe.Name)." }
        if (@($report.SourceAssemblies | Where-Object PublicKeyToken -CNE 'b03f5f7f11d50a3a').Count -ne 0) {
            throw "Source assembly public-signing identity mismatch for $($probe.Name)."
        }
        foreach ($assembly in @($report.SourceAssemblies) + @($report.RuntimeAssemblies)) {
            $relative = [IO.Path]::GetRelativePath($layout, $assembly.Path)
            if ($relative -cne "$($assembly.Name).dll") { throw "A consumer loaded '$($assembly.Path)' outside its staged layout." }
            $native = Join-Path $originalLayout $relative
            if ((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -cne $assembly.Sha256) {
                throw "Loaded $($assembly.Name) does not match the native producer."
            }
        }
        $windowsAssemblies = @($report.SourceAssemblies | Where-Object {
            $_.Name -in @('Microsoft.Testing.Extensions.PackagedApp', 'MSTest.Windows.UIAutomation') -or
            ($probe.Scenario -in @('uwp', 'winui') -and $_.Name -in @('MSTest.TestFramework', 'MSTest.Uwp', 'MSTest.WinUI', 'MSTestAdapter.PlatformServices', 'MSTest.TestAdapter'))
        })
        foreach ($assembly in $windowsAssemblies) {
            $minimum = if ($probe.Scenario -eq 'winui' -and $assembly.Name -in @('MSTest.TestFramework', 'MSTest.WinUI')) { '10.0.17763.0' } else { $probe.Platform }
            if ($assembly.Framework -cne ".NETCoreApp,Version=v$($probe.Major).0" -or
                $assembly.Platform -cne "Windows$($probe.Platform)" -or $assembly.SupportedOS -cne "windows$minimum") {
                throw "Windows TFM/minimum metadata mismatch in $($assembly.Name)."
            }
        }
        if ($probe.Scenario -eq 'packaged' -and (Test-Path -LiteralPath (Join-Path $layout 'Microsoft.Windows.UI.Xaml.dll'))) {
            throw 'The ordinary WinRT profile leaked the UWP XAML projection.'
        }
        $consumers.Add($report)
    }

    $graphPath = Join-Path $directory 'windows-actions.json'
    & $bazel aquery 'deps(//:windows_libraries)' --output=jsonproto --noshow_progress > $graphPath 2> (Join-Path $directory 'windows-actions.log')
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Windows producer action graph.' }
    $graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json
    $compilerCount = @($graph.actions | Where-Object mnemonic -eq 'CSharpCompile').Count
    if ($compilerCount -lt 16) { throw 'The Windows library matrix is missing compiler actions.' }
    foreach ($action in $graph.actions) {
        foreach ($argument in $action.arguments) {
            if ($argument -match '(?i)(^|[\s/\\])(MSBuild(\.exe|\.dll)?|dotnet(\.exe)?\s+(build|test|msbuild))(\s|$)') {
                throw "An MSBuild invocation appeared in the native producer graph: $argument"
            }
        }
    }
    $succeeded = $true
    Write-Host "Native Windows libraries and seven consumers passed. No app registration or XAML compilation was exercised. Evidence: $directory"
}
finally {
    [pscustomobject]@{
        Succeeded = $succeeded
        Revision = $revision
        WorkingTree = $sourceState
        Inputs = $inputHashes
        Consumers = $consumers
        Steps = $steps
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
    Pop-Location
}
