#Requires -Version 7.0

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Set-AsShipped([Parameter(Mandatory)][string]$Directory) {
    $shippedFilePath = "$Directory/PublicAPI.Shipped.txt"
    [array]$shipped = Get-Content $shippedFilePath -Encoding utf8
    if ($null -eq $shipped) {
        $shipped = @()
    }

    $unshippedFilePath = "$Directory/PublicAPI.Unshipped.txt"
    [array]$unshipped = Get-Content $unshippedFilePath -Encoding utf8
    $removed = @()
    $removedPrefix = "*REMOVED*";
    Write-Host "Processing $Directory"

    foreach ($item in $unshipped) {
        if ($item.Length -gt 0) {
            if ($item.StartsWith($removedPrefix)) {
                $item = $item.Substring($removedPrefix.Length)
                $removed += $item
            }
            elseif ($item -ne "#nullable enable") {
                $shipped += $item
            }
        }
    }

    @("#nullable enable") + ($shipped | Where-Object { ($_ -notin $removed) -and ($_ -ne "#nullable enable") } | Sort-Object) | Out-File $shippedFilePath -Encoding utf8
    "#nullable enable" | Out-File $unshippedFilePath -Encoding utf8
}

foreach ($file in Get-ChildItem "$PSScriptRoot\..\src" -Recurse -Include "PublicApi.Shipped.txt") {
    $Directory = Split-Path -parent $file
    Set-AsShipped $Directory
}
