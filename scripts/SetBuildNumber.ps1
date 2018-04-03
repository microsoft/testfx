# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Script to set the build number for official builds.
# We follow the following algorithm to set the build number:
# 1. We get the delta in terms of months from the first release date to the current UTC time.<mdiff>
# 2. We get the current day of the month. <dd>
# 3. We also get the revision number for the current build. <rr>
# 4. The build number is <mdiff><dd>.<rr>

[CmdletBinding(PositionalBinding=$false)]
Param(  
  [Parameter(Mandatory=$true)]
  [Alias("bn")]
  [System.String] $DefinitionBuildNumber
)

$TFB_DefinitionBuildNumber = $DefinitionBuildNumber
$TFB_FirstReleaseDate = [DateTime](Get-Date -Year 2016 -Month 05 -Day 01)

function Set_BuildNumber()
{
    $currentDate = [System.DateTime]::UTCNow
    
    # The default build number would be of the format $(date:yyyymmdd)$(rev:.rr)
    $revisionNumber = $TFB_DefinitionBuildNumber.Split(".")[1]
    
    $monthDiff = ($currentDate.Month - $TFB_FirstReleaseDate.Month) + 12*($currentDate.Year - $TFB_FirstReleaseDate.Year)
    $buildNumber = $monthDiff.ToString() + $currentDate.ToString("dd") + "." + $revisionNumber
    Write-Verbose("Build number used: " + $buildNumber)
    
    # This sets the build number.
    Write-Host("##vso[task.setvariable variable=BuildVersionSuffix]$buildNumber")
}

Set_BuildNumber
