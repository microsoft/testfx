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
    Write-Host("##vso[build.updatebuildnumber]$buildNumber")
}

Set_BuildNumber
