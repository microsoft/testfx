# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 5.1

[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidUsingWriteHost',
    '',
    Justification = 'The script emits Azure Pipelines logging commands and an explicit preflight report to the pipeline host.'
)]
[CmdletBinding()]
param(
    [ValidateSet('Preflight', 'LeakCheck')]
    [string] $Mode = 'Preflight',

    [switch] $RemoveStaleTestPackages
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# These prefixes are reserved by the three real application-model acceptance assets. Never broaden
# package discovery or cleanup beyond this list: package registration is per-user machine state.
$reservedPackageIdentityPrefixes = @(
    'MSTestClassicUwp',
    'MSTestModernUwp',
    'MTPWinUI'
)

function Get-ReservedTestPackage {
    $packages = @(Get-AppxPackage -ErrorAction Stop)
    return @(
        $packages | Where-Object {
            $packageName = $_.Name
            $reservedPackageIdentityPrefixes.Where(
                { $packageName.StartsWith($_, [StringComparison]::Ordinal) },
                'First'
            ).Count -ne 0
        }
    )
}

function Format-PackageRegistration {
    param([object[]] $Packages)

    return ($Packages | ForEach-Object {
            "$($_.Name) [$($_.PackageFullName)] at '$($_.InstallLocation)'"
        }) -join [Environment]::NewLine
}

function Get-RegistryValueDisplay {
    param(
        [string] $Path,
        [string] $Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return '<key missing>'
    }

    $property = Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $property) {
        return '<value missing>'
    }

    return [string] $property.$Name
}

if ($env:OS -ne 'Windows_NT') {
    throw "Windows application-model acceptance requires Windows. Current OS marker: '$($env:OS)'."
}

if (-not (Get-Command Get-AppxPackage -ErrorAction SilentlyContinue)) {
    throw 'Get-AppxPackage is unavailable. Run this preflight in 64-bit Windows PowerShell on a Windows image with AppX deployment support.'
}

$reservedPackages = @(Get-ReservedTestPackage)
if ($Mode -eq 'LeakCheck') {
    if ($reservedPackages.Count -ne 0) {
        throw (
            "Windows application-model acceptance leaked registrations with reserved test prefixes " +
            "'$($reservedPackageIdentityPrefixes -join "', '")'. Remove only these registrations and investigate the test cleanup failure:" +
            "$([Environment]::NewLine)$(Format-PackageRegistration -Packages $reservedPackages)"
        )
    }

    Write-Host "No package registration remains for reserved test prefixes: $($reservedPackageIdentityPrefixes -join ', ')."
    return
}

$failures = [Collections.Generic.List[string]]::new()
function Add-PreflightFailure {
    param([string] $Message)
    $failures.Add($Message)
    Write-Host "##vso[task.logissue type=error]$Message"
}

$os = Get-CimInstance -ClassName Win32_OperatingSystem
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$currentProcess = Get-Process -Id $PID

Write-Host "Windows application-model preflight environment:"
Write-Host "  OS: $($os.Caption) $($os.Version) (build $($os.BuildNumber))"
Write-Host "  User: $($identity.Name)"
Write-Host "  Elevated: $isAdministrator"
Write-Host "  Process/session: $PID / $($currentProcess.SessionId)"
Write-Host "  SESSIONNAME: $(if ($env:SESSIONNAME) { $env:SESSIONNAME } else { '<missing>' })"
Write-Host "  UserInteractive: $([Environment]::UserInteractive)"

$programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
$vswherePath = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = $null
if (-not (Test-Path -LiteralPath $vswherePath -PathType Leaf)) {
    Add-PreflightFailure "vswhere.exe was not found at '$vswherePath'. Install Visual Studio Installer and a full Visual Studio instance."
}
else {
    $vswhereArguments = @(
        '-latest'
        '-prerelease'
        '-products'
        '*'
        '-requires'
        'Microsoft.Component.MSBuild'
        'Microsoft.VisualStudio.Workload.Universal'
        'Microsoft.VisualStudio.ComponentGroup.UWP.Support'
        '-format'
        'json'
        '-utf8'
    )
    $vswhereOutput = @(& $vswherePath @vswhereArguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        Add-PreflightFailure (
            "vswhere.exe failed with exit code $LASTEXITCODE while requiring desktop MSBuild, " +
            "Microsoft.VisualStudio.Workload.Universal, and Microsoft.VisualStudio.ComponentGroup.UWP.Support. Output: $($vswhereOutput -join ' ')"
        )
    }
    else {
        try {
            $instances = @($vswhereOutput -join [Environment]::NewLine | ConvertFrom-Json)
            if ($instances.Count -ne 1) {
                Add-PreflightFailure (
                    "vswhere.exe found $($instances.Count) matching Visual Studio instances. Install a prerelease-capable " +
                    'Visual Studio 2026 instance with desktop MSBuild and the complete UWP support component group.'
                )
            }
            else {
                $visualStudio = $instances[0]
            }
        }
        catch {
            Add-PreflightFailure (
                "vswhere.exe returned invalid JSON while locating the UWP-capable Visual Studio instance: $($_.Exception.Message). " +
                "Raw output: $($vswhereOutput -join ' ')"
            )
        }
    }
}

if ($null -ne $visualStudio) {
    $installationPath = [string] $visualStudio.installationPath
    $installationVersion = [Version] ([string] $visualStudio.installationVersion)
    $msbuildPath = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
    $vstestCandidates = @(
        (Join-Path $installationPath 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'),
        (Join-Path $installationPath 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe')
    )
    $vstestConsolePath = $vstestCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    $testPlatformDirectory = Join-Path $installationPath 'Common7\IDE\Extensions\TestPlatform'
    $uwpRuntimeProviderFiles = @(
        (Join-Path $testPlatformDirectory 'Extensions\Microsoft.VisualStudio.UwpTestHostRuntimeProvider.dll'),
        (Join-Path $testPlatformDirectory 'Microsoft.VisualStudio.UwpTestHostRuntimeProvider.Deployment.dll')
    )

    Write-Host "  Visual Studio: $($visualStudio.displayName) $installationVersion at '$installationPath'"
    Write-Host "  Desktop MSBuild: '$msbuildPath'"
    Write-Host "  VSTest console: '$(if ($vstestConsolePath) { $vstestConsolePath } else { '<missing>' })'"
    Write-Host "  UWP runtime provider: $($uwpRuntimeProviderFiles -join '; ')"

    if ($installationVersion.Major -lt 18) {
        Add-PreflightFailure (
            "Modern UWP acceptance requires Visual Studio 2026 (18.x or newer), but vswhere selected " +
            "'$($visualStudio.displayName)' version '$installationVersion' at '$installationPath'. Use the purpose-built VS 2026 Windows agent."
        )
    }

    if (-not (Test-Path -LiteralPath $msbuildPath -PathType Leaf)) {
        Add-PreflightFailure "Desktop MSBuild.exe is missing at '$msbuildPath'. Repair Microsoft.Component.MSBuild in '$installationPath'."
    }

    if (-not $vstestConsolePath) {
        Add-PreflightFailure (
            "vstest.console.exe is missing from the selected Visual Studio instance. Checked: $($vstestCandidates -join '; '). " +
            'Install the Visual Studio Test Platform and UWP testing tools.'
        )
    }

    $missingRuntimeProviderFiles = @($uwpRuntimeProviderFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missingRuntimeProviderFiles.Count -ne 0) {
        Add-PreflightFailure (
            "The VSTest UWP runtime provider required for real .build.appxrecipe execution is incomplete. " +
            "Install or repair the UWP testing tools. Missing: $($missingRuntimeProviderFiles -join '; ')"
        )
    }

    $testPlatformUniversalManifest = Join-Path $programFilesX86 (
        "Microsoft SDKs\Windows Kits\10\ExtensionSDKs\TestPlatform.Universal\$($installationVersion.Major).0\SDKManifest.xml"
    )
    Write-Host "  TestPlatform.Universal SDK: '$testPlatformUniversalManifest'"
    if (-not (Test-Path -LiteralPath $testPlatformUniversalManifest -PathType Leaf)) {
        Add-PreflightFailure (
            "The TestPlatform.Universal $($installationVersion.Major).0 extension SDK required by classic UWP is missing at " +
            "'$testPlatformUniversalManifest'. Repair the selected Visual Studio UWP testing tools."
        )
    }
}

$windowsKitsRoot = Join-Path $programFilesX86 'Windows Kits\10'
$classicSdkVersion = [Version] '10.0.16299.0'
$classicSdkFiles = @(
    (Join-Path $windowsKitsRoot "bin\$classicSdkVersion\x64\makeappx.exe"),
    (Join-Path $windowsKitsRoot "bin\$classicSdkVersion\x64\makepri.exe"),
    (Join-Path $windowsKitsRoot "Platforms\UAP\$classicSdkVersion\Platform.xml"),
    (Join-Path $windowsKitsRoot "References\$classicSdkVersion\Windows.Foundation.FoundationContract\3.0.0.0\Windows.Foundation.FoundationContract.winmd"),
    (Join-Path $windowsKitsRoot "UnionMetadata\$classicSdkVersion\Windows.winmd")
)
$missingClassicSdkFiles = @($classicSdkFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
Write-Host "  Classic UWP SDK: $classicSdkVersion at '$windowsKitsRoot'"
if ($missingClassicSdkFiles.Count -ne 0) {
    Add-PreflightFailure (
        "Classic UWP acceptance requires Windows SDK $classicSdkVersion with UWP managed and x64 packaging tools. " +
        "Run eng/install-windows-sdk.ps1 or install the SDK through Visual Studio. Missing: $($missingClassicSdkFiles -join '; ')"
    )
}

$minimumModernSdkVersion = [Version] '10.0.26100.0'
$sdkBinRoot = Join-Path $windowsKitsRoot 'bin'
$installedSdkVersions = @()
if (Test-Path -LiteralPath $sdkBinRoot -PathType Container) {
    $installedSdkVersions = @(
        Get-ChildItem -LiteralPath $sdkBinRoot -Directory | ForEach-Object {
            $parsedVersion = $null
            if ([Version]::TryParse($_.Name, [ref] $parsedVersion)) {
                $parsedVersion
            }
        } | Sort-Object -Descending
    )
}

$modernSdkVersion = $installedSdkVersions | Where-Object {
    $_ -ge $minimumModernSdkVersion -and
    (Test-Path -LiteralPath (Join-Path $sdkBinRoot "$_\x64\makeappx.exe") -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $sdkBinRoot "$_\x64\makepri.exe") -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $windowsKitsRoot "Platforms\UAP\$_\Platform.xml") -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $windowsKitsRoot "UnionMetadata\$_\Windows.winmd") -PathType Leaf)
} | Select-Object -First 1
Write-Host "  Installed Windows SDK bin versions: $(if ($installedSdkVersions.Count) { $installedSdkVersions -join ', ' } else { '<none>' })"
if (-not $modernSdkVersion) {
    Add-PreflightFailure (
        "Modern UWP acceptance requires a complete Windows SDK $minimumModernSdkVersion or newer beneath '$windowsKitsRoot'. " +
        'A capable SDK must include x64 makeappx.exe/makepri.exe, the UAP platform metadata, and UnionMetadata\Windows.winmd. ' +
        "Installed bin versions: $(if ($installedSdkVersions.Count) { $installedSdkVersions -join ', ' } else { '<none>' })."
    )
}
else {
    Write-Host "  Selected Modern UWP SDK: $modernSdkVersion"
}

$appModelUnlockPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
$developerPolicyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Appx'
$localDeveloperMode = Get-RegistryValueDisplay -Path $appModelUnlockPath -Name 'AllowDevelopmentWithoutDevLicense'
$policyDeveloperMode = Get-RegistryValueDisplay -Path $developerPolicyPath -Name 'AllowDevelopmentWithoutDevLicense'
$localTrustedApps = Get-RegistryValueDisplay -Path $appModelUnlockPath -Name 'AllowAllTrustedApps'
$policyTrustedApps = Get-RegistryValueDisplay -Path $developerPolicyPath -Name 'AllowAllTrustedApps'
Write-Host "  Developer Mode local setting: $localDeveloperMode"
Write-Host "  Developer Mode policy setting: $policyDeveloperMode"
Write-Host "  Trusted-app sideload local setting (diagnostic only): $localTrustedApps"
Write-Host "  Trusted-app sideload policy setting (diagnostic only): $policyTrustedApps"

$developerPolicyConfigured = $policyDeveloperMode -notin @('<key missing>', '<value missing>')
$effectiveDeveloperMode = if ($developerPolicyConfigured) { $policyDeveloperMode } else { $localDeveloperMode }
$packageRegistrationEnabled = $effectiveDeveloperMode -eq '1'
if (-not $packageRegistrationEnabled) {
    if ($developerPolicyConfigured) {
        Add-PreflightFailure (
            "Developer Mode policy '$developerPolicyPath\AllowDevelopmentWithoutDevLicense' overrides the local setting " +
            "with '$policyDeveloperMode'. It must be DWORD 1 for unsigned loose package registration."
        )
    }
    elseif (-not $isAdministrator) {
        Add-PreflightFailure (
            'Developer Mode is not enabled, and the agent is not elevated. Enable AllowDevelopmentWithoutDevLicense ' +
            'before running the application-model tests.'
        )
    }
    else {
        try {
            New-Item -Path $appModelUnlockPath -Force | Out-Null
            New-ItemProperty `
                -Path $appModelUnlockPath `
                -Name 'AllowDevelopmentWithoutDevLicense' `
                -PropertyType DWord `
                -Value 1 `
                -Force | Out-Null
            $packageRegistrationEnabled =
                (Get-RegistryValueDisplay -Path $appModelUnlockPath -Name 'AllowDevelopmentWithoutDevLicense') -eq '1'
            Write-Host "  Enabled Developer Mode for this elevated, ephemeral test agent: $packageRegistrationEnabled"
        }
        catch {
            Add-PreflightFailure "Failed to enable Developer Mode for the test agent: $($_.Exception.Message)"
        }
    }
}
if (-not $packageRegistrationEnabled) {
    Add-PreflightFailure 'Developer Mode is required for unsigned loose package registration.'
}

$uacPolicyPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
$enableLua = Get-RegistryValueDisplay -Path $uacPolicyPath -Name 'EnableLUA'
Write-Host "  UAC EnableLUA: $enableLua"
if ($enableLua -ne '1') {
    Add-PreflightFailure "UAC must be enabled for packaged application activation. '$uacPolicyPath\EnableLUA' is '$enableLua'; set it to DWORD 1 and reboot the agent."
}

if (-not [Environment]::UserInteractive) {
    Add-PreflightFailure 'The agent process is not in an interactive user context ([Environment]::UserInteractive is false). Use an auto-logon, interactive purpose-built Windows agent.'
}
if ($currentProcess.SessionId -le 0) {
    Add-PreflightFailure "The agent runs in session $($currentProcess.SessionId). AUMID activation requires a logged-on user session greater than zero."
}
if ($env:SESSIONNAME -eq 'Services') {
    Add-PreflightFailure (
        "SESSIONNAME is 'Services'. AUMID activation requires a Console/RDP interactive user desktop, not the Services session."
    )
}
elseif ([string]::IsNullOrWhiteSpace($env:SESSIONNAME)) {
    Write-Host '  SESSIONNAME is unavailable; UserInteractive, SessionId, Explorer, HKCU, and desktop bounds remain authoritative.'
}
if (-not (Test-Path -LiteralPath 'HKCU:\Software')) {
    Add-PreflightFailure "The current user's registry hive is not loaded. Per-user package registration and AUMID activation require an HKCU profile."
}

$sameSessionExplorer = @(Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $currentProcess.SessionId)
if ($sameSessionExplorer.Count -eq 0) {
    Add-PreflightFailure "No explorer.exe shell is running in agent session $($currentProcess.SessionId). Use a purpose-built auto-logon agent with an interactive desktop."
}

try {
    Add-Type -AssemblyName System.Windows.Forms
    $virtualScreen = [Windows.Forms.SystemInformation]::VirtualScreen
    Write-Host "  Interactive desktop bounds: $($virtualScreen.Width)x$($virtualScreen.Height)"
    if ($virtualScreen.Width -lt 1024 -or $virtualScreen.Height -lt 768) {
        Add-PreflightFailure "The interactive desktop is $($virtualScreen.Width)x$($virtualScreen.Height); Windows app activation requires a usable desktop of at least 1024x768."
    }
}
catch {
    Add-PreflightFailure "Failed to query the interactive desktop bounds: $($_.Exception.Message). Use an agent with a loaded interactive desktop."
}

if ($RemoveStaleTestPackages) {
    foreach ($package in $reservedPackages) {
        Write-Host "Removing stale reserved test registration '$($package.PackageFullName)'."
        try {
            Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
        }
        catch {
            Add-PreflightFailure "Failed to remove stale reserved test registration '$($package.PackageFullName)': $($_.Exception.Message)"
        }
    }

    $remainingPackages = @(Get-ReservedTestPackage)
    if ($remainingPackages.Count -ne 0) {
        Add-PreflightFailure (
            "Reserved test registrations remain after scoped cleanup:$([Environment]::NewLine)" +
            (Format-PackageRegistration -Packages $remainingPackages)
        )
    }
}
elseif ($reservedPackages.Count -ne 0) {
    Add-PreflightFailure (
        "Stale package registrations already use reserved application-model test prefixes. " +
        "Run this script with -RemoveStaleTestPackages before testing:$([Environment]::NewLine)" +
        (Format-PackageRegistration -Packages $reservedPackages)
    )
}

if ($failures.Count -ne 0) {
    throw (
        "Windows application-model preflight failed with $($failures.Count) prerequisite error(s):$([Environment]::NewLine)- " +
        ($failures -join "$([Environment]::NewLine)- ")
    )
}

Write-Host 'Windows application-model preflight passed. The agent is explicitly capable of running the real UWP and packaged WinUI acceptance tests.'
