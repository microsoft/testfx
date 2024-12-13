Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Set-AsShipped([Parameter(Mandatory)][string]$Directory) {
    $shippedFilePath = "$Directory/PublicAPI.Shipped.txt"
    $shipped = Get-Content $shippedFilePath -Encoding utf8
    if ($null -eq $shipped) {
        $shipped = @()
    }

    $unshippedFilePath = "$Directory/PublicAPI.Unshipped.txt"
    $unshipped = Get-Content $unshippedFilePath
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

    $shipped | Sort-Object | Where-Object { $_ -notin $removed } | Out-File $shippedFilePath -Encoding utf8
    "#nullable enable" | Out-File $unshippedFilePath -Encoding utf8
}

foreach ($file in Get-ChildItem "$PSScriptRoot\..\src" -Recurse -Include "PublicApi.Shipped.txt") {
    $Directory = Split-Path -parent $file
    Set-AsShipped $Directory
}
