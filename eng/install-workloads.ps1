# Script to install dotnet workloads for device testing (Android, iOS, etc.)
# This script should be run after the SDK is installed in .dotnet folder.
#
# The script reads workload versions from eng/Versions.props to ensure
# consistent versions across the repository.
#
# Usage:
#   .\eng\install-workloads.ps1 [workloads...]
#
# Examples:
#   .\eng\install-workloads.ps1                     # Install default workloads (android)
#   .\eng\install-workloads.ps1 android             # Install android workload with dependencies
#   .\eng\install-workloads.ps1 android maui        # Install specific workloads

[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Workloads
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

# Default workloads to install if none specified
$defaultWorkloads = @('android')

if ($Workloads.Count -eq 0) {
    $Workloads = $defaultWorkloads
}

# Find the dotnet installation
$dotnetRoot = Join-Path $repoRoot '.dotnet'

if (-not (Test-Path $dotnetRoot)) {
    Write-Error "Error: .dotnet folder not found at $dotnetRoot"
    Write-Error "Please run .\restore.cmd or .\build.cmd first to install the SDK."
    exit 1
}

$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'

if (-not (Test-Path $dotnetExe)) {
    Write-Error "Error: dotnet executable not found at $dotnetExe"
    exit 1
}

# Read versions from eng/Versions.props
$versionsProps = Join-Path $scriptRoot 'Versions.props'

function Get-VersionFromProps {
    param([string]$PropertyName)
    
    if (Test-Path $versionsProps) {
        $content = Get-Content $versionsProps -Raw
        if ($content -match "<$PropertyName>([^<]+)</$PropertyName>") {
            return $matches[1]
        }
    }
    return $null
}

function Resolve-PropertyReference {
    param([string]$Value)
    
    if ($Value -match '\$\(([^)]+)\)') {
        $refProp = $matches[1]
        return Get-VersionFromProps -PropertyName $refProp
    }
    return $Value
}

# Get workload versions from Versions.props
$androidVersion = Get-VersionFromProps -PropertyName 'MicrosoftAndroidSdkWindowsPackageVersion'
$iosVersion = Resolve-PropertyReference (Get-VersionFromProps -PropertyName 'MicrosoftiOSSdkPackageVersion')
$maccatalystVersion = Resolve-PropertyReference (Get-VersionFromProps -PropertyName 'MicrosoftMacCatalystSdkPackageVersion')

Write-Host "Using dotnet from: $dotnetExe"
$sdkVersion = & $dotnetExe --version
Write-Host "SDK version: $sdkVersion"
Write-Host ""
Write-Host "Workload versions from eng/Versions.props:"
if ($androidVersion) { Write-Host "  Android: $androidVersion" }
if ($iosVersion) { Write-Host "  iOS: $iosVersion" }
if ($maccatalystVersion) { Write-Host "  Mac Catalyst: $maccatalystVersion" }
Write-Host ""

# Expand workload aliases to include dependencies and versions
# Format: "workload[@version]"
$expandedWorkloads = @()
foreach ($workload in $Workloads) {
    switch ($workload) {
        'android' {
            # Android workload requires wasm-tools for some scenarios
            $expandedWorkloads += 'wasm-tools-net10'
            if ($androidVersion) {
                $expandedWorkloads += "android@$androidVersion"
            } else {
                $expandedWorkloads += 'android'
            }
        }
        'ios' {
            if ($iosVersion) {
                $expandedWorkloads += "ios@$iosVersion"
            } else {
                $expandedWorkloads += 'ios'
            }
        }
        'maccatalyst' {
            if ($maccatalystVersion) {
                $expandedWorkloads += "maccatalyst@$maccatalystVersion"
            } else {
                $expandedWorkloads += 'maccatalyst'
            }
        }
        'maui' {
            # MAUI includes android, ios, and other dependencies
            $expandedWorkloads += 'wasm-tools-net10'
            $expandedWorkloads += 'maui'
        }
        default {
            $expandedWorkloads += $workload
        }
    }
}

# Remove duplicates while preserving order (compare base workload name without version)
$seen = @{}
$uniqueWorkloads = @()
foreach ($workload in $expandedWorkloads) {
    $baseWorkload = $workload -replace '@.*$', ''
    if (-not $seen.ContainsKey($baseWorkload)) {
        $seen[$baseWorkload] = $true
        $uniqueWorkloads += $workload
    }
}

# Install each workload
foreach ($workloadSpec in $uniqueWorkloads) {
    # Parse workload@version format
    $parts = $workloadSpec -split '@', 2
    $workload = $parts[0]
    $version = if ($parts.Length -gt 1) { $parts[1] } else { $null }
    
    Write-Host "Installing workload: $workload"
    if ($version) {
        Write-Host "  Version: $version"
    }
    
    try {
        $installArgs = @('workload', 'install', $workload, '--skip-sign-check')
        if ($version) {
            $installArgs += '--version'
            $installArgs += $version
        }
        
        & $dotnetExe @installArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Warning: Failed to install workload '$workload' with version. Trying without specific version..."
            & $dotnetExe workload install $workload --skip-sign-check
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Warning: Failed to install workload '$workload'. It may not be available for this SDK version."
            }
        }
    }
    catch {
        Write-Warning "Warning: Failed to install workload '$workload': $_"
    }
    Write-Host ""
}

Write-Host "Workload installation complete."
Write-Host ""
Write-Host "Installed workloads:"
& $dotnetExe workload list
