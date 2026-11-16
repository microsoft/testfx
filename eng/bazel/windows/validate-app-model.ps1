# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [string] $ArtifactsDirectory,
    [switch] $HostOnly
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'The real Windows application-model probe requires Windows.' }
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$outputBase = & $bazel info output_base
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the Bazel output root.' }
if (-not $ArtifactsDirectory) {
    $ArtifactsDirectory = Join-Path (Split-Path $outputBase.Replace('/', '\') -Parent) 'windows-app-model'
}
$run = [Guid]::NewGuid().ToString('N')
$directory = Join-Path $ArtifactsDirectory $run
$layout = Join-Path $directory 'package layout'
$identity = "TestFx.Bazel.$run"
$steps = [Collections.Generic.List[object]]::new()
$succeeded = $false
$inputHashes = @(Get-FileHash -Algorithm SHA256 -LiteralPath @(
    (Join-Path $root 'MODULE.bazel'), (Join-Path $root 'eng\bazel\defs.bzl'),
    (Join-Path $root 'eng\bazel\windows.bzl'), (Join-Path $PSScriptRoot 'AppModelHost.cs'),
    (Join-Path $PSScriptRoot 'windows.MODULE.bazel'), (Join-Path $PSScriptRoot 'paket.lock'),
    (Join-Path $PSScriptRoot 'validate-app-model.ps1')
))
New-Item -ItemType Directory -Path $directory | Out-Null

function Invoke-Process([string] $Name, [string] $Executable, [string[]] $Arguments, [int] $ExpectedExit = 0, [hashtable] $Environment = @{}, [switch] $StandardOutputOnly) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.WorkingDirectory = $directory
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    foreach ($environmentName in @('TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER', 'TESTINGPLATFORM_PACKAGEDAPP_TARGET', 'MSTEST_APPMODEL_CONTROLLER_EXTENSIONS', 'MSTEST_APPMODEL_CONTROLLER_DOTNETTEST_PIPE')) {
        $null = $info.Environment.Remove($environmentName)
    }
    foreach ($environmentName in $Environment.Keys) { $info.Environment[$environmentName] = $Environment[$environmentName] }
    $process = [Diagnostics.Process]::Start($info)
    try {
        $revision = git rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Could not record the source revision.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit(120000)
        if ($timedOut) {
            $process.Kill($true)
            if (-not $process.WaitForExit(30000)) { throw "Owned process $($process.Id) did not terminate." }
        }
        $output = $stdout.GetAwaiter().GetResult()
        $text = $output + $stderr.GetAwaiter().GetResult()
        $logName = "$($steps.Count)-$Name.log"
        $text | Set-Content -LiteralPath (Join-Path $directory $logName) -Encoding utf8
        $steps.Add([pscustomobject]@{ Name = $Name; Executable = $Executable; Arguments = $Arguments; ExitCode = $process.ExitCode; ExpectedExit = $ExpectedExit; TimedOut = $timedOut; Log = $logName })
        if ($timedOut -or $process.ExitCode -ne $ExpectedExit) {
            throw "$Name returned $($process.ExitCode), expected $ExpectedExit; timeout=$timedOut. See its retained log."
        }
        if ($StandardOutputOnly) { return $output }
        return $text
    }
    finally { $process.Dispose() }
}

function Invoke-Appx([string] $Script, [string] $Name) {
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes("`$ErrorActionPreference = 'Stop'; `$ProgressPreference = 'SilentlyContinue'; " + $Script))
    return Invoke-Process $Name $windowsPowerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) -StandardOutputOnly
}

function Get-OwnedPackage {
    $script = "ConvertTo-Json -InputObject @(Get-AppxPackage -Name '$identity' | Select-Object Name,PackageFullName,PackageFamilyName,InstallLocation)"
    return @(Invoke-Appx $script 'package-registration' | ConvertFrom-Json)
}

function Copy-Publish([string] $Target, [string] $Name, [string] $Destination) {
    $paths = @(& $bazel cquery $Target --output=files --noshow_progress)
    if ($LASTEXITCODE -ne 0) { throw "Could not locate $Target." }
    $executables = @($paths | Where-Object { $_.EndsWith("/$Name.exe", [StringComparison]::Ordinal) })
    if ($executables.Count -ne 1) { throw "Expected one $Name apphost." }
    $original = Join-Path $executionRoot $executables[0].Replace('/', '\')
    $originalLayout = Split-Path $original -Parent
    foreach ($path in $paths) {
        $source = Join-Path $executionRoot $path.Replace('/', '\')
        $relative = [IO.Path]::GetRelativePath($originalLayout, $source)
        if ($relative.StartsWith('..') -or [IO.Path]::IsPathRooted($relative)) { throw "Out-of-layout publish file: $source" }
        $targetPath = Join-Path $Destination $relative
        New-Item -ItemType Directory -Path (Split-Path $targetPath -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $targetPath
    }
    return [pscustomobject]@{ Executable = (Join-Path $Destination "$Name.exe"); Original = $originalLayout }
}

function Assert-Pass([string] $Path) {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $results = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
    $counters = $trx.SelectSingleNode('//*[local-name()="Counters"]')
    if ($results.Count -ne 1 -or $results[0].testName -cne 'TestMethod1' -or $results[0].outcome -cne 'Passed' -or
        [int]$counters.executed -ne 1 -or [int]$counters.passed -ne 1 -or [int]$counters.failed -ne 0) {
        throw "Expected TestMethod1 to execute and pass in $Path."
    }
}

Push-Location $root
try {
    $targets = @('//:windows_app_model')
    if (-not $HostOnly) { $targets += '//:windows_controllers' }
    & $bazel build @targets --jobs=1 --noshow_progress --show_result=0 *> (Join-Path $directory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Native app-model build failed; see build.log.' }
    $executionRoot = & $bazel info execution_root
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
    $executionRoot = $executionRoot.Replace('/', '\')
    $hostLayout = Copy-Publish '//:windows_app_model' 'TestFx.Bazel.AppModelHost' $layout
    if (-not $HostOnly) {
        $controllers = [ordered]@{}
        foreach ($framework in @('net10', 'net11')) {
            $controllers[$framework] = Copy-Publish "//:windows_controller_$framework" 'mstest-appmodel-controller' (Join-Path $directory "controller-$framework")
        }
        $sdkVersion = [regex]::Match((Get-Content -LiteralPath 'MODULE.bazel' -Raw), 'dotnet\.toolchain\(dotnet_version = "([^"]+)"\)').Groups[1].Value
        $sdks = @(Get-ChildItem -LiteralPath (Join-Path $executionRoot 'external') -Directory -Filter "*dotnet_sdk.$sdkVersion`_*" |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'dotnet.exe') })
        if (-not $sdkVersion -or $sdks.Count -ne 1) { throw 'Expected exactly one pinned native SDK for the framework-dependent controller.' }
        $controllerEnvironment = @{ DOTNET_ROOT = $sdks[0].FullName; DOTNET_ROOT_X64 = $sdks[0].FullName; DOTNET_ROLL_FORWARD_TO_PRERELEASE = '1' }
    }
    $pass = @('--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1', '--report-trx', '--report-trx-filename', 'tests.trx')
    $inPlace = Join-Path $directory 'in-place'
    $null = Invoke-Process 'unpackaged-pass' $hostLayout.Executable ($pass + @('--results-directory', $inPlace))
    Assert-Pass (Join-Path $inPlace 'tests.trx')
    $null = Invoke-Process 'unpackaged-failure' $hostLayout.Executable @('--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2') 2
    $null = Invoke-Process 'unpackaged-empty' $hostLayout.Executable @('--filter-uid', 'NoSuchTest', '--minimum-expected-tests', '1') 9
    if (-not $HostOnly) {
        foreach ($framework in $controllers.Keys) {
            $null = Invoke-Process "invalid-controller-bootstrap-$framework" $controllers[$framework].Executable @('--internal-packagedapp-controller-v2') 5 $controllerEnvironment
        }
    }

    $assets = Join-Path $layout 'Assets'
    New-Item -ItemType Directory -Path $assets | Out-Null
    foreach ($file in @('StoreLogo.png', 'Square44x44Logo.scale-200.png', 'Square150x150Logo.scale-200.png')) {
        Copy-Item -LiteralPath (Join-Path $root "samples\public\WinUIMtpPackagedApp\Assets\$file") -Destination (Join-Path $assets $file)
    }
    @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
 xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
 IgnorableNamespaces="uap uap10 rescap">
 <Identity Name="$identity" Publisher="CN=MSTestBazelWindows" Version="1.0.0.0" ProcessorArchitecture="x64" />
 <Properties><DisplayName>Native Bazel app-model probe</DisplayName><PublisherDisplayName>TestFx</PublisherDisplayName><Logo>Assets\StoreLogo.png</Logo></Properties>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
 <Resources><Resource Language="en-us" /></Resources>
 <Applications>
  <Application Id="App" Executable="TestFx.Bazel.AppModelHost.exe" EntryPoint="Windows.FullTrustApplication" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
   <uap:VisualElements DisplayName="Native Bazel app-model probe" Description="Native console app; no XAML pipeline" BackgroundColor="transparent"
    Square150x150Logo="Assets\Square150x150Logo.scale-200.png" Square44x44Logo="Assets\Square44x44Logo.scale-200.png" />
  </Application>
 </Applications>
 <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@ | Set-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Encoding utf8
    if ((Get-OwnedPackage).Count -ne 0) { throw 'The new task identity was already registered.' }
    $suppressed = Join-Path $directory 'suppressed'
    $null = Invoke-Process 'suppressed-launcher' $hostLayout.Executable ($pass + @('--results-directory', $suppressed)) 0 @{ TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER = 'never' }
    Assert-Pass (Join-Path $suppressed 'tests.trx')
    if ((Get-OwnedPackage).Count -ne 0) { throw 'The suppressed launcher registered an app.' }
    $activated = Join-Path $directory 'activated'
    $null = Invoke-Process 'registered-activation' $hostLayout.Executable ($pass + @('--results-directory', $activated))
    Assert-Pass (Join-Path $activated 'tests.trx')
    $marker = Get-Content -LiteralPath (Join-Path $layout 'native-app-model.json') -Raw | ConvertFrom-Json
    $packages = Get-OwnedPackage
    if ($packages.Count -ne 1 -or $marker.PackageName -cne $identity -or
        -not [string]::Equals($packages[0].InstallLocation.TrimEnd('\'), $layout.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($marker.InstalledPath.TrimEnd('\'), $layout.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Activated package identity/install location did not match this run.'
    }
    foreach ($assembly in $marker.Assemblies) {
        if ([IO.Path]::GetRelativePath($layout, $assembly.Path) -cne "$($assembly.Name).dll" -or
            (Get-FileHash -LiteralPath (Join-Path $hostLayout.Original "$($assembly.Name).dll") -Algorithm SHA256).Hash -cne $assembly.Sha256) {
            throw 'Activated host loaded different binaries than the native producer.'
        }
        if ($assembly.Name -ne 'TestFx.Bazel.AppModelHost' -and $assembly.PublicKeyToken -cne 'b03f5f7f11d50a3a') {
            throw 'Activated host did not load the corrected source platform identity.'
        }
    }
    if (-not $HostOnly) {
        $arguments = @('--internal-packagedapp-controller-v1', $hostLayout.Executable, 'msbuild;packagedapp;trx')
        foreach ($framework in $controllers.Keys) {
            $controller = $controllers[$framework]
            $controllerResults = Join-Path $directory "controller-results-$framework"
            $null = Invoke-Process "controller-pass-$framework" $controller.Executable ($arguments + $pass + @('--results-directory', $controllerResults)) 0 $controllerEnvironment
            Assert-Pass (Join-Path $controllerResults 'tests.trx')
            $null = Invoke-Process "controller-failure-$framework" $controller.Executable ($arguments + @('--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2')) 2 $controllerEnvironment
            $null = Invoke-Process "controller-empty-$framework" $controller.Executable ($arguments + @('--filter-uid', 'NoSuchTest', '--minimum-expected-tests', '1')) 9 $controllerEnvironment
            $discovery = Invoke-Process "controller-discovery-no-handshake-$framework" $controller.Executable ($arguments + @('--list-tests')) 0 $controllerEnvironment
            if (-not $discovery.Contains('TestMethod1') -or -not $discovery.Contains('TestMethod2')) { throw 'Controller discovery did not reach the host.' }
        }
    }
    $succeeded = $true
}
finally {
    try {
        $markerPath = Join-Path $layout 'native-app-model.json'
        if (Test-Path -LiteralPath $markerPath) {
            $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
            $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($marker.ProcessId)"
            if ($process -and [string]::Equals($process.ExecutablePath, (Join-Path $layout 'TestFx.Bazel.AppModelHost.exe'), [StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $marker.ProcessId
            }
        }
        foreach ($package in Get-OwnedPackage) {
            if (-not [string]::Equals($package.InstallLocation.TrimEnd('\'), $layout.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to remove a package registered outside this task-owned layout.'
            }
            $null = Invoke-Appx "Remove-AppxPackage -Package '$($package.PackageFullName)'" 'package-cleanup'
        }
        if ((Get-OwnedPackage).Count -ne 0) { throw 'Task-owned package cleanup was incomplete.' }
    }
    finally {
        [pscustomobject]@{ Succeeded = $succeeded; HostOnly = [bool]$HostOnly; Revision = $revision; Inputs = $inputHashes; Identity = $identity; Layout = $layout; Steps = $steps } |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
        Pop-Location
    }
}
Write-Host "Native console package validation passed (host-only=$HostOnly); no XAML compilation was exercised. Evidence: $directory"
