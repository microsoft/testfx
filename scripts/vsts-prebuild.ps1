# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Sets variables which are used across the build tasks.

param (
    [Parameter(Mandatory)]
    [string] $IsRtmBuild
)

$TFB_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$TFB_ENG_DIR = Join-Path $TFB_ROOT_DIR "eng"
$TFB_VERSION_PREFIX = ([xml](Get-Content $TFB_ENG_DIR\Versions.props)).Project.PropertyGroup.VersionPrefix
$TFB_RELEASE_VERSION_LABEL = ([xml](Get-Content $TFB_ENG_DIR\Versions.props)).Project.PropertyGroup.PreReleaseVersionLabel
$TFB_BUILD_NUMBER = IF ($env:BUILD_BUILDNUMBER -ne $null) { $env:BUILD_BUILDNUMBER -replace "\.", "-" } ELSE { "LOCAL" }
$TFB_NUGET_VERSION_SUFFIX = "$TFB_RELEASE_VERSION_LABEL-$TFB_BUILD_NUMBER"
$TFB_PACKAGE_VERSION = "$TFB_VERSION_PREFIX-$TFB_NUGET_VERSION_SUFFIX"
$TFB_BUILD_VERSION_SUFFIX = "0.0"
$TFB_FIRST_RELEASE_DATE = [DateTime](Get-Date -Year 2016 -Month 05 -Day 01)
$TFB_BRANCH = "LOCALBRANCH"
try {
    $TFB_BRANCH = $env:BUILD_SOURCEBRANCH -replace "^refs/heads/"
    if ([string]::IsNullOrWhiteSpace($TFB_BRANCH)) {
        $TFB_BRANCH = git -C "." rev-parse --abbrev-ref HEAD
    }
}
catch { }

# Set TFB_BUILD_VERSION_SUFFIX
if ($TFB_BUILD_NUMBER -ne "LOCAL") {
    $currentDate = [System.DateTime]::UTCNow

    # The default build number would be of the format $(date:yyyymmdd)$(rev:-rr)
    $revisionNumber = $TFB_BUILD_NUMBER.Split("-")[1]

    $monthDiff = ($currentDate.Month - $TFB_FIRST_RELEASE_DATE.Month) + 12 * ($currentDate.Year - $TFB_FIRST_RELEASE_DATE.Year)
    $TFB_BUILD_VERSION_SUFFIX = $monthDiff.ToString() + $currentDate.ToString("dd") + "." + $revisionNumber
}

# Set RTM configuration
if ($IsRtmBuild -eq "true") {
    $TFB_PACKAGE_VERSION = "$TFB_VERSION_PREFIX"
    $TFB_NUGET_VERSION_SUFFIX = "''"
}

# Dump variables
Get-ChildItem variable:TP* | Format-Table

# Validate RTM config
if ($IsRtmBuild -eq "true" -and (-not $TFB_BRANCH.StartsWith("rel/"))) {
    throw "An RTM build can only be started from a release branch, ``$TFB_BRANCH`` is invalid!"
}

if ($IsRtmBuild -eq "true" -and ($TFB_RELEASE_VERSION_LABEL -ne "release" -and $TFB_RELEASE_VERSION_LABEL -ne "servicing")) {
    throw "An RTM build cannot be based on a ``$TFB_RELEASE_VERSION_LABEL`` build!"
}

# Publish CI variables
Write-Host "##vso[task.setvariable variable=TestAdapterNugetVersion;]$TFB_VERSION_PREFIX"
Write-Host "##vso[task.setvariable variable=TestFrameworkNugetVersion;]$TFB_VERSION_PREFIX"
Write-Host "##vso[task.setvariable variable=NugetVersionSuffix;]$TFB_NUGET_VERSION_SUFFIX"

Write-Host "##vso[task.setvariable variable=BuildVersionPrefix;]14.0"
Write-Host("##vso[task.setvariable variable=BuildVersionSuffix]$TFB_BUILD_VERSION_SUFFIX")
Write-Host "##vso[task.setvariable variable=PackageVersion;]$TFB_PACKAGE_VERSION"
