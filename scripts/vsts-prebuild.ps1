# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Sets variables which are used across the build tasks.

param (
  [Parameter(Mandatory)]
  [string] $IsRtmBuild
)

$TPB_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$TPB_ENG_DIR = Join-Path $TPB_ROOT_DIR "eng"
$TPB_VERSION_PREFIX = ([xml](Get-Content $TPB_ENG_DIR\Versions.props)).Project.PropertyGroup.VersionPrefix
$TPB_RELEASE_VERSION_LABEL = ([xml](Get-Content $TPB_ENG_DIR\Versions.props)).Project.PropertyGroup.PreReleaseVersionLabel
$TPB_BUILD_NUMBER = IF ($env:BUILD_BUILDNUMBER -ne $null) { $env:BUILD_BUILDNUMBER -replace "\.", "-" } ELSE { "LOCAL" }
$TPB_NUGET_VERSION_SUFFIX = "$TPB_RELEASE_VERSION_LABEL-$TPB_BUILD_NUMBER"
$TPB_PACKAGE_VERSION = "$TPB_VERSION_PREFIX-$TPB_NUGET_VERSION_SUFFIX"
$TPB_BUILD_VERSION_SUFFIX = "0.0"
$TFB_FIRST_RELEASE_DATE = [DateTime](Get-Date -Year 2016 -Month 05 -Day 01)
$TPB_BRANCH = "LOCALBRANCH"
try {
    $TPB_BRANCH = $env:BUILD_SOURCEBRANCH -replace "^refs/heads/"
    if ([string]::IsNullOrWhiteSpace($TPB_BRANCH)) {
        $TPB_BRANCH = git -C "." rev-parse --abbrev-ref HEAD
    }
}
catch { }

# Set TPB_BUILD_VERSION_SUFFIX
if($TPB_BUILD_NUMBER -ne "LOCAL")
{
    $currentDate = [System.DateTime]::UTCNow

    # The default build number would be of the format $(date:yyyymmdd)$(rev:-rr)
    $revisionNumber = $TPB_BUILD_NUMBER.Split("-")[1]

    $monthDiff = ($currentDate.Month - $TFB_FIRST_RELEASE_DATE.Month) + 12*($currentDate.Year - $TFB_FIRST_RELEASE_DATE.Year)
    $TPB_BUILD_VERSION_SUFFIX = $monthDiff.ToString() + $currentDate.ToString("dd") + "." + $revisionNumber
}

# Set RTM configuration
if ($IsRtmBuild -eq "true") {
    $TPB_PACKAGE_VERSION = "$TPB_VERSION_PREFIX"
    $TPB_NUGET_VERSION_SUFFIX = "''"
}

# Dump variables
Get-ChildItem variable:TP* | Format-Table

# Validate RTM config
if ($IsRtmBuild -eq "true" -and (-not $TPB_BRANCH.StartsWith("rel/"))) {
    throw "An RTM build can only be started from a release branch, ``$TPB_BRANCH`` is invalid!"
}

if ($IsRtmBuild -eq "true" -and ($TPB_RELEASE_VERSION_LABEL -ne "release" -and $TPB_RELEASE_VERSION_LABEL -ne "servicing")) {
    throw "An RTM build cannot be based on a ``$TPB_RELEASE_VERSION_LABEL`` build!"
}

# Publish CI variables
Write-Host "##vso[task.setvariable variable=TestAdapterNugetVersion;]$TPB_VERSION_PREFIX"
Write-Host "##vso[task.setvariable variable=TestFrameworkNugetVersion;]$TPB_VERSION_PREFIX"
Write-Host "##vso[task.setvariable variable=NugetVersionSuffix;]$TPB_NUGET_VERSION_SUFFIX"

Write-Host "##vso[task.setvariable variable=BuildVersionPrefix;]14.0"
Write-Host("##vso[task.setvariable variable=BuildVersionSuffix]$TPB_BUILD_VERSION_SUFFIX")
Write-Host "##vso[task.setvariable variable=PackageVersion;]$TPB_PACKAGE_VERSION"
