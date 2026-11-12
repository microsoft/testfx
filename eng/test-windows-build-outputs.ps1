[CmdletBinding()]
param(
    [string]$HelperPath = (Join-Path $PSScriptRoot "windows-build-outputs.ps1")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Join-Path ([IO.Path]::GetTempPath()) ("testfx-build-outputs-" + [Guid]::NewGuid().ToString("N"))
$producer = Join-Path $root "producer & source"
$archivePath = Join-Path $root "outputs.zip"
$sourceVersion = "a" * 40
$passed = [Collections.Generic.List[string]]::new()
Write-Host "Build-output test root: '$root'."

function Write-FixtureFile([string]$Path, [string]$Content) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Content)
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected '$Expected', got '$Actual'." }
}

function Assert-Failure([scriptblock]$Command, [string]$Message) {
    try { & $Command }
    catch {
        if (-not $_.Exception.Message.Contains($Message)) { throw }
        return
    }
    throw "Expected failure containing '$Message', but the command succeeded."
}

function Invoke-Case([string]$Name, [scriptblock]$Command) {
    & $Command
    $passed.Add($Name)
    Write-Host "PASS $Name"
}

function Import-Fixture([string]$Name, [string]$Path = $archivePath, [string]$Commit = $sourceVersion, [string]$Config = "Release") {
    $consumer = Join-Path $root $Name
    & $HelperPath -Action Import -ArchivePath $Path -Configuration $Config `
        -RepositoryRoot $consumer -NuGetPackagesRoot (Join-Path $consumer ".packages & restored") -SourceVersion $Commit
}

function Edit-Archive([string]$Name, [scriptblock]$Edit) {
    $path = Join-Path $root "$Name.zip"
    Copy-Item -LiteralPath $archivePath -Destination $path
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Update)
    try { & $Edit $zip }
    finally { $zip.Dispose() }
    return $path
}

function Read-Manifest($Zip) {
    $reader = [IO.StreamReader]::new($Zip.GetEntry("windows-build-outputs.json").Open())
    try { return $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
}

function Write-Manifest($Zip, $Manifest) {
    $Zip.GetEntry("windows-build-outputs.json").Delete()
    $writer = [IO.StreamWriter]::new($Zip.CreateEntry("windows-build-outputs.json").Open())
    try { $writer.Write(($Manifest | ConvertTo-Json -Depth 5)) }
    finally { $writer.Dispose() }
}

function Assert-CoverageRoots([string]$Consumer) {
    [xml]$settings = Get-Content -LiteralPath (Join-Path $Consumer $coveragePath) -Raw
    $includes = @($settings.Configuration.CodeCoverage.Sources.Include.Source)
    $excludes = @($settings.Configuration.CodeCoverage.Sources.Exclude.Source)
    Assert-Equal ("^" + [regex]::Escape((Join-Path $Consumer "src") + [IO.Path]::DirectorySeparatorChar) + ".*") $includes[0] "Consumer source-root regex."
    Assert-Equal '^[/\\]_[/\\]src[/\\].*' $includes[1] "Mapped source-root regex."
    Assert-Equal ("^" + [regex]::Escape((Join-Path $Consumer ".packages & restored") + [IO.Path]::DirectorySeparatorChar) + ".*") $excludes[0] "Consumer package-root regex."
    Assert-Equal ("^" + [regex]::Escape((Join-Path $Consumer "test/Utilities") + [IO.Path]::DirectorySeparatorChar) + ".*") $excludes[1] "Consumer nested source-path regex."
}

function Assert-CoverageSourceMappings([string]$Consumer) {
    $path = Join-Path $Consumer $mappingPath
    Assert-Equal $true (Test-Path -LiteralPath $path) "The coverage source mapping must be transported."
    $lines = [IO.File]::ReadAllLines($path)
    $consumerRoot = $Consumer + [IO.Path]::DirectorySeparatorChar
    $project = Join-Path $Consumer "src/Probe/Probe.csproj"
    $nestedProject = Join-Path $Consumer "src/Nested/Nested.csproj"
    $packages = (Join-Path $Consumer ".packages & restored") + [IO.Path]::DirectorySeparatorChar
    Assert-Equal 4 $lines.Count "Source-root mapping count."
    Assert-Equal "$project|$consumerRoot=/_/" $lines[0] "Consumer repository source mapping."
    Assert-Equal "$project|$packages=/_1/" $lines[1] "Consumer package source mapping."
    Assert-Equal "$nestedProject|$(Join-Path $Consumer 'src/Nested')$([IO.Path]::DirectorySeparatorChar)=/_/nested/" $lines[2] "Nested physical path and unchanged virtual path."
    Assert-Equal "$project|C:\unrelated cache\=/_2/" $lines[3] "Unrelated source mappings must remain unchanged."
    Assert-Equal "FFFE" ([Convert]::ToHexString([IO.File]::ReadAllBytes($path)[0..1])) "Preserved UTF-16 mapping encoding."
}

try {
    $binaryPath = "artifacts/bin/Portable.UnitTests/Release/net10.0/Portable.UnitTests.dll"
    $coveragePath = "artifacts/obj/Portable.UnitTests/Release/net10.0/coverage.config"
    $mappingPath = "artifacts/bin/Portable.UnitTests/Release/net10.0/.msCoverageExtensionSourceRootsMapping_Portable.UnitTests"
    $packagePath = "artifacts/packages/Release/Shipping/Microsoft.Testing.Platform.1.0.0.nupkg"
    Write-FixtureFile (Join-Path $producer $binaryPath) "assembly-marker"
    Write-FixtureFile (Join-Path $producer "artifacts/bin/Portable.UnitTests/Release/net10.0/Portable.UnitTests.pdb") "portable-symbol-marker"
    Write-FixtureFile (Join-Path $producer "artifacts/bin/Portable.UnitTests/Release/net10.0/Portable.UnitTests.runtimeconfig.json") '{"runtimeOptions":{}}'
    Write-FixtureFile (Join-Path $producer "artifacts/bin/Portable.UnitTests/Release/net10.0/data/input.txt") "test-data-marker"
    Write-FixtureFile (Join-Path $producer $packagePath) "shipping-package-marker"
    Write-FixtureFile (Join-Path $producer "artifacts/packages/Release/NonShipping/Fixture.1.0.0.nupkg") "nonshipping-package-marker"
    Write-FixtureFile (Join-Path $producer "artifacts/bin/Portable.UnitTests/Debug/net10.0/stale.dll") "excluded-debug-output"
    Write-FixtureFile (Join-Path $producer ".dotnet/sdk/sdk-marker.txt") "excluded-sdk"
    $producerPackages = Join-Path $producer ".packages & original"
    $project = Join-Path $producer "src/Probe/Probe.csproj"
    $nestedProject = Join-Path $producer "src/Nested/Nested.csproj"
    [IO.File]::WriteAllLines((Join-Path $producer $mappingPath), @(
        "$project|$producer$([IO.Path]::DirectorySeparatorChar)=/_/"
        "$project|$producerPackages$([IO.Path]::DirectorySeparatorChar)=/_1/"
        "$nestedProject|$(Join-Path $producer 'src/Nested')$([IO.Path]::DirectorySeparatorChar)=/_/nested/"
        "$project|C:\unrelated cache\=/_2/"
    ), [Text.Encoding]::Unicode)
    Write-FixtureFile (Join-Path $producer "artifacts/obj/Portable.UnitTests/project.assets.json") `
        (@{ project = @{ restore = @{ packagesPath = $producerPackages } } } | ConvertTo-Json -Depth 4)
    $sourceRegex = [Security.SecurityElement]::Escape("^" + [regex]::Escape((Join-Path $producer "src") + [IO.Path]::DirectorySeparatorChar) + ".*")
    $packagesRegex = [Security.SecurityElement]::Escape("^" + [regex]::Escape($producerPackages + [IO.Path]::DirectorySeparatorChar) + ".*")
    $infrastructureRegex = [Security.SecurityElement]::Escape("^" + [regex]::Escape((Join-Path $producer "test/Utilities") + [IO.Path]::DirectorySeparatorChar) + ".*")
    Write-FixtureFile (Join-Path $producer $coveragePath) @"
<Configuration><CodeCoverage><Sources><Include><Source>$sourceRegex</Source><Source>^[/\\]_[/\\]src[/\\].*</Source></Include><Exclude><Source>$packagesRegex</Source><Source>$infrastructureRegex</Source></Exclude></Sources></CodeCoverage></Configuration>
"@

    Invoke-Case "ExportContainsOnlyConfigurationOutputs" {
        & $HelperPath -Action Export -ArchivePath $archivePath -RepositoryRoot $producer -SourceVersion $sourceVersion
        $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $manifest = Read-Manifest $zip
            Assert-Equal 1 $manifest.formatVersion "Manifest format."
            Assert-Equal $sourceVersion $manifest.sourceVersion "Source identity."
            Assert-Equal 8 $manifest.files.Count "Payload inventory."
            Assert-Equal 9 $zip.Entries.Count "Archive inventory including manifest."
            if ($zip.Entries.FullName -match 'Debug|project\.assets\.json|sdk-marker') {
                throw "Restore state, SDKs, or another configuration entered the payload."
            }
        }
        finally { $zip.Dispose() }
    }

    Invoke-Case "RoundTripPreservesBinariesPackagesAndTestData" {
        Import-Fixture "round-trip & relocated"
        $consumer = Join-Path $root "round-trip & relocated"
        Assert-Equal "assembly-marker" ([IO.File]::ReadAllText((Join-Path $consumer $binaryPath))) "Assembly bytes."
        Assert-Equal "portable-symbol-marker" ([IO.File]::ReadAllText((Join-Path $consumer "artifacts/bin/Portable.UnitTests/Release/net10.0/Portable.UnitTests.pdb"))) "Portable symbol bytes."
        Assert-Equal "shipping-package-marker" ([IO.File]::ReadAllText((Join-Path $consumer $packagePath))) "Shipping package bytes."
        Assert-Equal "nonshipping-package-marker" ([IO.File]::ReadAllText((Join-Path $consumer "artifacts/packages/Release/NonShipping/Fixture.1.0.0.nupkg"))) "NonShipping package bytes."
        Assert-Equal "test-data-marker" ([IO.File]::ReadAllText((Join-Path $consumer "artifacts/bin/Portable.UnitTests/Release/net10.0/data/input.txt"))) "Test data."
        Assert-CoverageRoots $consumer
    }

    Invoke-Case "CoverageSourceMappingsUseConsumerPaths" {
        Assert-CoverageSourceMappings (Join-Path $root "round-trip & relocated")
    }

    Invoke-Case "WindowsCoveragePathsUseConsumerDirectorySeparators" {
        $path = Edit-Archive "windows-source-paths" {
            param($zip)
            $manifest = Read-Manifest $zip
            $windowsRepository = 'C:\build & source\repo\'
            $windowsPackages = 'C:\package cache\'
            $reader = [IO.StreamReader]::new($zip.GetEntry($coveragePath).Open())
            try { [xml]$settings = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
            foreach ($node in $settings.SelectNodes("//Sources/Include/Source | //Sources/Exclude/Source")) {
                if ($node.InnerText.Contains([regex]::Escape($manifest.repositoryRoot)) -or
                    $node.InnerText.Contains([regex]::Escape($manifest.nuGetPackagesRoot))) {
                    $node.InnerText = $node.InnerText.
                        Replace([regex]::Escape($manifest.nuGetPackagesRoot), [regex]::Escape($windowsPackages)).
                        Replace([regex]::Escape($manifest.repositoryRoot), [regex]::Escape($windowsRepository)).
                        Replace("/", '\\')
                }
            }
            $reader = [IO.StreamReader]::new($zip.GetEntry($mappingPath).Open())
            try { $mapping = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
            $mappingLines = @($mapping -split '\r?\n' | Where-Object { $_ } | ForEach-Object {
                $parts = $_ -split '=', 2
                $physical = $parts[0].Replace($manifest.nuGetPackagesRoot, $windowsPackages).
                    Replace($manifest.repositoryRoot, $windowsRepository).Replace("/", "\")
                "$physical=$($parts[1])"
            })
            $encoding = [Text.Encoding]::Unicode
            $mappingBytes = $encoding.GetPreamble() + $encoding.GetBytes(($mappingLines -join "`r`n") + "`r`n")
            $zip.GetEntry($mappingPath).Delete()
            $stream = $zip.CreateEntry($mappingPath).Open()
            try { $stream.Write([byte[]]$mappingBytes) }
            finally { $stream.Dispose() }
            $mappingEntry = @($manifest.files | Where-Object { $_.path -eq $mappingPath })[0]
            $mappingEntry.length = $mappingBytes.Length
            $mappingEntry.sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$mappingBytes))
            $manifest.repositoryRoot = $windowsRepository
            $manifest.nuGetPackagesRoot = $windowsPackages
            $bytes = [Text.Encoding]::UTF8.GetBytes($settings.OuterXml)
            $zip.GetEntry($coveragePath).Delete()
            $stream = $zip.CreateEntry($coveragePath).Open()
            try { $stream.Write($bytes) }
            finally { $stream.Dispose() }
            $file = @($manifest.files | Where-Object { $_.path -eq $coveragePath })[0]
            $file.length = $bytes.Length
            $file.sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            Write-Manifest $zip $manifest
        }
        Import-Fixture "windows-source-paths" -Path $path
        Assert-CoverageRoots (Join-Path $root "windows-source-paths")
        Assert-CoverageSourceMappings (Join-Path $root "windows-source-paths")
    }

    Invoke-Case "RejectsStaleSourceBeforeExtraction" {
        Assert-Failure { Import-Fixture "stale-source" -Commit ("b" * 40) } "does not match"
        Assert-Equal $false (Test-Path -LiteralPath (Join-Path $root "stale-source/artifacts")) "A rejected source must not write outputs."
    }
    Invoke-Case "RejectsWrongConfigurationBeforeExtraction" {
        Assert-Failure { Import-Fixture "wrong-configuration" -Config Debug } "does not match"
    }
    Invoke-Case "RejectsUnsupportedManifestVersion" {
        $path = Edit-Archive "unsupported-format" {
            param($zip)
            $manifest = Read-Manifest $zip
            $manifest.formatVersion = 2
            Write-Manifest $zip $manifest
        }
        Assert-Failure { Import-Fixture "unsupported-format" -Path $path } "does not match"
    }
    Invoke-Case "RejectsMissingManifest" {
        $path = Edit-Archive "missing-manifest" { param($zip) $zip.GetEntry("windows-build-outputs.json").Delete() }
        Assert-Failure { Import-Fixture "missing-manifest" -Path $path } "manifest is missing"
    }
    Invoke-Case "RejectsTamperedPayloadBeforeExtraction" {
        $path = Edit-Archive "tampered" {
            param($zip)
            $zip.GetEntry($binaryPath).Delete()
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($binaryPath).Open())
            try { $writer.Write("tampered-marker") }
            finally { $writer.Dispose() }
        }
        Assert-Failure { Import-Fixture "tampered" -Path $path } "hash mismatch"
        Assert-Equal $false (Test-Path -LiteralPath (Join-Path $root "tampered/artifacts")) "A hash failure must not write outputs."
    }
    Invoke-Case "RejectsTraversalBeforeExtraction" {
        $path = Edit-Archive "traversal" {
            param($zip)
            $manifest = Read-Manifest $zip
            $file = @($manifest.files | Where-Object { $_.path -eq $binaryPath })[0]
            $file.path = "artifacts/bin/Portable.UnitTests/Release/../../escape.dll"
            $zip.GetEntry($binaryPath).Delete()
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($file.path).Open())
            try { $writer.Write("assembly-marker") }
            finally { $writer.Dispose() }
            Write-Manifest $zip $manifest
        }
        Assert-Failure { Import-Fixture "traversal" -Path $path } "Invalid payload entry"
    }
    Invoke-Case "RejectsDuplicateEntries" {
        $path = Edit-Archive "duplicate" { param($zip) $zip.CreateEntry($binaryPath) | Out-Null }
        Assert-Failure { Import-Fixture "duplicate" -Path $path } "Duplicate archive entry"
    }
    Invoke-Case "RejectsExistingOutputsWithoutOverwriting" {
        Assert-Failure { Import-Fixture "round-trip & relocated" } "requires clean"
        Assert-Equal "assembly-marker" ([IO.File]::ReadAllText((Join-Path $root "round-trip & relocated/$binaryPath"))) "Existing output must remain intact."
    }
    Invoke-Case "RejectsUnbuiltProducer" {
        Assert-Failure {
            & $HelperPath -Action Export -ArchivePath (Join-Path $root "empty.zip") -RepositoryRoot (Join-Path $root "empty") -SourceVersion $sourceVersion
        } "No Release binaries"
    }
    Write-Host "Passed $($passed.Count) build-output tests."
}
finally {
    if (Test-Path -LiteralPath $root) { [IO.Directory]::Delete($root, $true) }
}
