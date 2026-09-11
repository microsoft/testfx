param(
    [string] $FeedDirectory = (Join-Path $PSScriptRoot '..\.isolated-coverage-feed')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$version = '18.10.1-isolation.9525c65c197a'
$stockHash = '0ABB8709BB3D3F33B79B6FE5CA18D96E30DF31535C3BDF5B74CA21721BEF848E'
$targetHash = '876987F310CC2B7BD471E8C328832DF1B2EA64388D80B2C93AE4D284FBE5897E'
$targetUrl = 'https://raw.githubusercontent.com/microsoft/vstest/9525c65c197a3d4d89ea096f44a275bce24d04ed/src/package/Microsoft.CodeCoverage/Microsoft.CodeCoverage.targets'
$targetEntry = 'build/netstandard2.0/Microsoft.CodeCoverage.targets'
$nuspecEntry = 'Microsoft.CodeCoverage.nuspec'

function Read-EntryBytes($entry) {
    $stream = $entry.Open()
    $memory = [IO.MemoryStream]::new()
    try {
        $stream.CopyTo($memory)
        return ,$memory.ToArray()
    }
    finally {
        $stream.Dispose()
        $memory.Dispose()
    }
}

function Get-BytesHash([byte[]] $bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

[IO.Directory]::CreateDirectory($FeedDirectory) | Out-Null
$FeedDirectory = (Resolve-Path $FeedDirectory).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("isolated-coverage-" + [Guid]::NewGuid())
[IO.Directory]::CreateDirectory($scratch) | Out-Null
try {
    $stockPath = Join-Path $scratch 'stock.nupkg'
    $targetPath = Join-Path $scratch 'Microsoft.CodeCoverage.targets'
    $index = Invoke-RestMethod 'https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json'
    $base = ($index.resources | Where-Object { $_.'@type' -like 'PackageBaseAddress*' } | Select-Object -First 1).'@id'
    Invoke-WebRequest ($base + 'microsoft.codecoverage/18.10.0/microsoft.codecoverage.18.10.0.nupkg') -OutFile $stockPath -MaximumRetryCount 3 -RetryIntervalSec 5
    Invoke-WebRequest $targetUrl -OutFile $targetPath -MaximumRetryCount 3 -RetryIntervalSec 5
    if ((Get-FileHash $stockPath).Hash -cne $stockHash) { throw 'Stock package SHA256 mismatch.' }
    if ((Get-FileHash $targetPath).Hash -cne $targetHash) { throw 'Release target SHA256 mismatch.' }

    $outputPath = Join-Path $scratch "Microsoft.CodeCoverage.$version.nupkg"
    $stock = [IO.Compression.ZipFile]::OpenRead($stockPath)
    try {
        $output = [IO.Compression.ZipFile]::Open($outputPath, [IO.Compression.ZipArchiveMode]::Create)
        try {
            if ($null -eq $stock.GetEntry($targetEntry) -or $null -eq $stock.GetEntry($nuspecEntry)) {
                throw 'Stock package layout changed.'
            }
            foreach ($entry in $stock.Entries) {
                if ($entry.FullName -eq '.signature.p7s') { continue }
                $bytes = Read-EntryBytes $entry
                if ($entry.FullName -eq $targetEntry) {
                    $bytes = [IO.File]::ReadAllBytes($targetPath)
                }
                elseif ($entry.FullName -eq $nuspecEntry) {
                    $text = [Text.Encoding]::UTF8.GetString($bytes)
                    if ([regex]::Matches($text, '<version>18\.10\.0</version>').Count -ne 1) {
                        throw 'Unexpected stock nuspec version.'
                    }
                    $bytes = [Text.Encoding]::UTF8.GetBytes($text.Replace('<version>18.10.0</version>', "<version>$version</version>"))
                }
                $copy = $output.CreateEntry($entry.FullName, [IO.Compression.CompressionLevel]::Optimal)
                # Repacking must not change the collision's timestamp conditions.
                $copy.LastWriteTime = $entry.LastWriteTime
                $copy.ExternalAttributes = $entry.ExternalAttributes
                $stream = $copy.Open()
                try { $stream.Write($bytes, 0, $bytes.Length) }
                finally { $stream.Dispose() }
            }
        }
        finally { $output.Dispose() }

        $repacked = [IO.Compression.ZipFile]::OpenRead($outputPath)
        try {
            if ($repacked.Entries.Count -ne $stock.Entries.Count - 1 -or $null -ne $repacked.GetEntry('.signature.p7s')) {
                throw 'Unexpected repacked entries/signature.'
            }
            foreach ($entry in $stock.Entries) {
                if ($entry.FullName -eq '.signature.p7s') { continue }
                $copy = $repacked.GetEntry($entry.FullName)
                if ($null -eq $copy -or $copy.LastWriteTime.DateTime -ne $entry.LastWriteTime.DateTime) {
                    throw "Entry missing or timestamp changed: $($entry.FullName)"
                }
                $actualHash = Get-BytesHash (Read-EntryBytes $copy)
                if ($entry.FullName -eq $targetEntry) {
                    if ($actualHash -cne $targetHash) { throw 'Repacked target hash mismatch.' }
                }
                elseif ($entry.FullName -ne $nuspecEntry -and $actualHash -cne (Get-BytesHash (Read-EntryBytes $entry))) {
                    throw "Unrelated entry changed: $($entry.FullName)"
                }
            }
            $preservedEntries = $repacked.Entries.Count - 2
        }
        finally { $repacked.Dispose() }
    }
    finally { $stock.Dispose() }

    $destination = Join-Path $FeedDirectory ([IO.Path]::GetFileName($outputPath))
    if (Test-Path $destination) {
        if ((Get-FileHash $destination).Hash -cne (Get-FileHash $outputPath).Hash) {
            throw "The immutable experiment version already exists with different bytes: $destination"
        }
    }
    else {
        [IO.File]::Move($outputPath, $destination)
    }
    Write-Host "ISOLATED_COVERAGE_PACKAGE version=$version source=$destination sha256=$((Get-FileHash $destination).Hash)"
    Write-Host "ISOLATED_COVERAGE_INPUT stockSha256=$stockHash releaseTargetSha256=$targetHash preservedEntries=$preservedEntries preservedTimestamps=true source=$targetUrl"
}
finally {
    Remove-Item $scratch -Recurse -Force
}
