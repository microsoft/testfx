[CmdletBinding(PositionalBinding=$false)]
Param(  
  [Parameter(Mandatory=$true)]
  [System.String] $RevisionNumber
)

$TFB_FirstReleaseDate = [DateTime](Get-Date -Year 2016 -Month 05 -Day 01)

function Set_BuildNumber()
{
    $currentDate = [System.DateTime]::UTCNow
    $monthDiff = ($currentDate.Month - $TFB_FirstReleaseDate.Month) + 12*($currentDate.Year - $TFB_FirstReleaseDate.Year)
    $buildNumber = $monthDiff.ToString() + $currentDate.ToString("dd") + $RevisionNumber
    Write-Verbose("Build number used: " + $buildNumber)
    Write-Host("##vso[build.updatebuildnumber]$buildNumber")
}

Set_BuildNumber
