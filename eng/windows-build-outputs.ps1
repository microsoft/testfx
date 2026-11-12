[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Export", "Import")]
    [string]$Action,
    [Parameter(Mandatory)]
    [string]$ArchivePath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$SourceVersion = $env:BUILD_SOURCEVERSION,
    [string]$NuGetPackagesRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$ArchivePath = [IO.Path]::GetFullPath($ArchivePath)
$Configuration = if ($Configuration -ieq "Release") { "Release" } else { "Debug" }
$manifestName = "windows-build-outputs.json"

if (-not $SourceVersion) {
    $SourceVersion = git -C $RepositoryRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw "Cannot identify the source commit." }
}
if ($SourceVersion -notmatch '\A(?:[0-9a-f]{40}|[0-9a-f]{64})\z') {
    throw "SourceVersion must be a full Git commit hash."
}

function Get-OutputFiles {
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    $binRoot = Join-Path $RepositoryRoot "artifacts/bin"
    if (Test-Path -LiteralPath $binRoot) {
        foreach ($project in Get-ChildItem -LiteralPath $binRoot -Directory) {
            $directory = Join-Path $project.FullName $Configuration
            if (Test-Path -LiteralPath $directory) {
                $files.AddRange([IO.FileInfo[]]@(Get-ChildItem -LiteralPath $directory -File -Recurse -Force))
            }
        }
    }

    $packages = Join-Path $RepositoryRoot "artifacts/packages/$Configuration"
    if (Test-Path -LiteralPath $packages) {
        $files.AddRange([IO.FileInfo[]]@(Get-ChildItem -LiteralPath $packages -File -Recurse))
    }

    $objRoot = Join-Path $RepositoryRoot "artifacts/obj"
    if (Test-Path -LiteralPath $objRoot) {
        foreach ($project in Get-ChildItem -LiteralPath $objRoot -Directory) {
            $directory = Join-Path $project.FullName $Configuration
            if (Test-Path -LiteralPath $directory) {
                $files.AddRange([IO.FileInfo[]]@(Get-ChildItem -LiteralPath $directory -Filter coverage.config -File -Recurse))
            }
        }
    }
    return $files.ToArray()
}

function Get-RelativePath([string]$Path) {
    return [IO.Path]::GetRelativePath($RepositoryRoot, $Path).Replace("\", "/")
}

function Get-StreamHash([IO.Stream]$Stream) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Stream))
}

if ($Action -eq "Export") {
    $files = @(Get-OutputFiles)
    if (-not @($files | Where-Object { (Get-RelativePath $_.FullName).StartsWith("artifacts/bin/") }).Count) {
        throw "No $Configuration binaries were found."
    }
    if (-not @($files | Where-Object {
        (Get-RelativePath $_.FullName).StartsWith("artifacts/packages/$Configuration/") -and $_.Extension -eq ".nupkg"
    }).Count) {
        throw "No $Configuration NuGet packages were found."
    }
    $settings = @($files | Where-Object { $_.Name -eq "coverage.config" })
    if ($settings.Count -eq 0) {
        throw "No $Configuration coverage settings were found."
    }

    $packageRoots = @($settings | ForEach-Object {
        $project = (Get-RelativePath $_.FullName).Split("/")[2]
        $assetsPath = Join-Path $RepositoryRoot "artifacts/obj/$project/project.assets.json"
        $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
        $assets.project.restore.packagesPath
    } | Sort-Object -Unique)
    if ($packageRoots.Count -ne 1 -or -not $packageRoots[0]) {
        throw "Coverage settings must use one restored NuGet package root."
    }

    $entries = @($files | Sort-Object FullName | ForEach-Object {
        if ($_.FullName -eq $ArchivePath) {
            throw "The output archive cannot be inside the payload."
        }
        [ordered]@{
            path = Get-RelativePath $_.FullName
            length = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
    $manifest = [ordered]@{
        formatVersion = 1
        sourceVersion = $SourceVersion
        configuration = $Configuration
        repositoryRoot = $RepositoryRoot.TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
        nuGetPackagesRoot = $packageRoots[0].TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
        files = $entries
    }

    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ArchivePath)) | Out-Null
    $stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::Create)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $entries) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, (Join-Path $RepositoryRoot $entry.path), $entry.path,
                [IO.Compression.CompressionLevel]::Fastest) | Out-Null
        }
        $writer = [IO.StreamWriter]::new($archive.CreateEntry($manifestName).Open())
        try { $writer.Write(($manifest | ConvertTo-Json -Depth 5)) }
        finally { $writer.Dispose() }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }
    Write-Host "Exported $($entries.Count) files for $SourceVersion ($Configuration) to '$ArchivePath'."
    return
}

if (-not $NuGetPackagesRoot) {
    throw "Import requires the consumer's NuGetPackagesRoot."
}
$NuGetPackagesRoot = [IO.Path]::GetFullPath($NuGetPackagesRoot)
if (@(Get-OutputFiles).Count -ne 0) {
    throw "Import requires clean $Configuration output directories; existing binaries, packages, or coverage settings were found."
}

$archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
try {
    $archiveEntries = [Collections.Generic.Dictionary[string, IO.Compression.ZipArchiveEntry]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        if (-not $archiveEntries.TryAdd($entry.FullName, $entry)) {
            throw "Duplicate archive entry '$($entry.FullName)'."
        }
    }
    if (-not $archiveEntries.ContainsKey($manifestName)) {
        throw "The build-output manifest is missing."
    }
    $reader = [IO.StreamReader]::new($archiveEntries[$manifestName].Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
    if ($manifest.formatVersion -ne 1 -or $manifest.sourceVersion -ne $SourceVersion -or
        $manifest.configuration -cne $Configuration) {
        throw "The payload format, source commit, or configuration does not match this job."
    }
    if (-not $manifest.repositoryRoot -or -not $manifest.nuGetPackagesRoot -or
        $manifest.files.Count -eq 0 -or $archiveEntries.Count -ne $manifest.files.Count + 1) {
        throw "The build-output manifest is incomplete."
    }

    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $configurationPattern = [regex]::Escape($Configuration)
    $allowedPath = "\Aartifacts/(?:bin/[^/]+/$configurationPattern/.+|packages/$configurationPattern/.+|obj/[^/]+/$configurationPattern/(?:[^/]+/)*coverage\.config)\z"
    foreach ($file in $manifest.files) {
        if ($file.path -cnotmatch $allowedPath -or $file.path.Contains("\") -or
            @($file.path.Split("/") | Where-Object { $_ -in ".", "..", "" }).Count -ne 0 -or
            -not $paths.Add($file.path) -or -not $archiveEntries.ContainsKey($file.path) -or
            $file.sha256 -notmatch '\A[0-9a-f]{64}\z' -or
            $archiveEntries[$file.path].Length -ne $file.length) {
            throw "Invalid payload entry '$($file.path)'."
        }
    }
    if (-not @($manifest.files | Where-Object { $_.path.StartsWith("artifacts/bin/") -and $_.path.EndsWith(".dll") }).Count -or
        -not @($manifest.files | Where-Object { $_.path.StartsWith("artifacts/packages/") -and $_.path.EndsWith(".nupkg") }).Count -or
        -not @($manifest.files | Where-Object { $_.path.StartsWith("artifacts/obj/") -and $_.path.EndsWith("/coverage.config") }).Count) {
        throw "The payload must contain binaries, NuGet packages, and coverage settings."
    }

    # Verify the complete archive before modifying the consumer's output tree.
    foreach ($file in $manifest.files) {
        $stream = $archiveEntries[$file.path].Open()
        try {
            if ((Get-StreamHash $stream) -ne $file.sha256) {
                throw "Payload hash mismatch for '$($file.path)'."
            }
        }
        finally { $stream.Dispose() }
    }
    foreach ($file in $manifest.files) {
        $destination = Join-Path $RepositoryRoot $file.path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($archiveEntries[$file.path], $destination)
    }
}
finally { $archive.Dispose() }

$consumerRepositoryRoot = $RepositoryRoot.TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
$consumerPackagesRoot = $NuGetPackagesRoot.TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
$consumerRepositoryPattern = [regex]::Escape($consumerRepositoryRoot)
$consumerPackagesPattern = [regex]::Escape($consumerPackagesRoot)
$producerRepositoryPattern = [regex]::Escape($manifest.repositoryRoot)
$producerPackagesPattern = [regex]::Escape($manifest.nuGetPackagesRoot)
foreach ($file in $manifest.files | Where-Object { $_.path.EndsWith("/coverage.config") }) {
    $path = Join-Path $RepositoryRoot $file.path
    $settings = [Xml.XmlDocument]::new()
    $settings.PreserveWhitespace = $true
    $settings.XmlResolver = $null
    $settings.Load($path)
    $sources = $settings.SelectNodes("//Sources/Include/Source | //Sources/Exclude/Source")
    if ($sources.Count -eq 0) { throw "Coverage settings '$path' contain no source filters." }
    foreach ($node in $sources) {
        if ($node.InnerText.Contains($producerPackagesPattern) -or $node.InnerText.Contains($producerRepositoryPattern)) {
            $value = $node.InnerText.Replace($producerPackagesPattern, $consumerPackagesPattern).
                Replace($producerRepositoryPattern, $consumerRepositoryPattern)
            if ($manifest.repositoryRoot.EndsWith("\") -and [IO.Path]::DirectorySeparatorChar -eq "/") {
                $value = $value.Replace('\\', '/')
            }
            $node.InnerText = $value
        }
    }
    $settings.Save($path)
}

function Convert-CoverageSourcePath([string]$Path) {
    foreach ($root in (@(
        @{ Producer = $manifest.repositoryRoot; Consumer = $consumerRepositoryRoot }
        @{ Producer = $manifest.nuGetPackagesRoot; Consumer = $consumerPackagesRoot }
    ) | Sort-Object { $_.Producer.Length } -Descending)) {
        $comparison = if ($root.Producer.EndsWith("\")) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if ($Path.StartsWith($root.Producer, $comparison)) {
            $relative = $Path.Substring($root.Producer.Length)
            if ($root.Producer.EndsWith("\") -and [IO.Path]::DirectorySeparatorChar -eq "/") {
                $relative = $relative.Replace("\", "/")
            }
            return $root.Consumer + $relative
        }
    }
    return $Path
}

foreach ($file in $manifest.files | Where-Object {
    [IO.Path]::GetFileName($_.path).StartsWith(".msCoverageExtensionSourceRootsMapping_", [StringComparison]::Ordinal)
}) {
    $path = Join-Path $RepositoryRoot $file.path
    $reader = [IO.StreamReader]::new($path)
    $lines = [Collections.Generic.List[string]]::new()
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.Length -eq 0) {
                $lines.Add($line)
                continue
            }
            # The coverage SDK writes project|physical-root=virtual-root records.
            $projectEnd = $line.IndexOf("|")
            $physicalEnd = $line.LastIndexOf("=")
            if ($projectEnd -le 0 -or $physicalEnd -le $projectEnd + 1 -or $physicalEnd -eq $line.Length - 1) {
                throw "Invalid coverage source-root mapping in '$path'."
            }
            $project = Convert-CoverageSourcePath $line.Substring(0, $projectEnd)
            $physical = Convert-CoverageSourcePath $line.Substring($projectEnd + 1, $physicalEnd - $projectEnd - 1)
            $lines.Add("$project|$physical" + $line.Substring($physicalEnd))
        }
        $encoding = $reader.CurrentEncoding
    }
    finally { $reader.Dispose() }
    [IO.File]::WriteAllLines($path, $lines, $encoding)
}

Write-Host "Imported $($manifest.files.Count) verified files for $SourceVersion ($Configuration); coverage metadata now uses consumer-local paths."
