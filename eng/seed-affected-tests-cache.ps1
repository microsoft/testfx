<#
.SYNOPSIS
Pre-seeds the Azure DevOps affected-test map cache from the newest main build whose map collection succeeded.

.DESCRIPTION
The Azure DevOps artifact storage provider only reads maps from builds whose overall result is "succeeded".
When main is red for unrelated reasons (for example, a failing test job), pull requests keep reading maps from the
last green build. Once a dependency or SDK bump changes the test-module identity, every module falls back to
running all tests.

This script runs before "dotnet test --affected-tests" in pull-request validation. It mirrors the provider's
candidate selection (main builds whose source revision is the PR merge base or one of its ancestors) but also
accepts builds that failed after the "Windows AffectedTests" collection job succeeded. When such a build is newer
than the build the provider would choose, the script downloads its map artifact and writes the provider's local
cache entries (schema 2) so the provider reads those fresher maps instead.

The script fails open: any error is reported as a warning and the provider's own lookup proceeds unchanged.
It depends on the cache layout of Microsoft.Testing.Extensions.AffectedTests.Storage.AzureDevOps; see
docs/affected-test-selection.md before bumping that package.
#>
[CmdletBinding()]
param(
    [string]$Repository = (Split-Path $PSScriptRoot -Parent),
    [string]$CommonCommit,
    [string]$CacheRoot,
    [string]$CollectionUri,
    [string]$CollectionJobName = "Windows AffectedTests",
    [ValidateRange(1, 200)]
    [int]$MaximumBuilds = 50,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$PSNativeCommandUseErrorActionPreference = $false

$script:CacheSchemaVersion = 2
$script:ObjectNamePrefix = "affected-tests/v2/"

function Get-Sha256Hex([byte[]]$Bytes) {
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($Bytes))
}

function Test-AffectedTestsObjectName([string]$ObjectName) {
    $segments = $ObjectName.Split('/')
    return $segments.Count -eq 4 -and
        $segments[0] -ceq "affected-tests" -and
        $segments[1] -ceq "v2" -and
        $segments[2] -match '^[0-9A-Za-z_-]+$' -and
        $segments[3] -match '^[0-9A-Za-z_-]+\.json$'
}

# Mirrors AtomicCacheFile.GetPath: <root>/<repository identity>/<lowercase SHA-256 of the UTF-8 object name>.json.
function Get-AffectedTestsCachePath([string]$Root, [string]$ObjectName) {
    if (-not (Test-AffectedTestsObjectName $ObjectName)) {
        throw "Unexpected affected-test map object name '$ObjectName'."
    }

    $repositoryIdentity = $ObjectName.Split('/')[2]
    $fileName = (Get-Sha256Hex ([System.Text.Encoding]::UTF8.GetBytes($ObjectName))).ToLowerInvariant() + ".json"
    return [System.IO.Path]::Combine($Root, $repositoryIdentity, $fileName)
}

function Write-AtomicFile([string]$Path, [scriptblock]$Writer) {
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    $temporaryPath = "$Path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        $result = & $Writer $temporaryPath
        [System.IO.File]::Move($temporaryPath, $Path, $true)
        return $result
    }
    finally {
        if ([System.IO.File]::Exists($temporaryPath)) {
            [System.IO.File]::Delete($temporaryPath)
        }
    }
}

function Copy-StreamWithSha256([System.IO.Stream]$Source, [string]$Destination) {
    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash([System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $target = [System.IO.FileStream]::new($Destination, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $buffer = [byte[]]::new(1MB)
        while (($read = $Source.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $hash.AppendData($buffer, 0, $read)
            $target.Write($buffer, 0, $read)
        }

        return [Convert]::ToHexString($hash.GetHashAndReset())
    }
    finally {
        $target.Dispose()
        $hash.Dispose()
    }
}

# Writes the same metadata shape as the provider's ArtifactCacheMetadata (System.Text.Json, PascalCase).
function Write-AffectedTestsCacheEntry([System.IO.Stream]$Content, [string]$CachePath, [int]$BuildId, [string]$Baseline) {
    $contentHash = Write-AtomicFile $CachePath { param($path) Copy-StreamWithSha256 $Content $path }
    $metadata = [ordered]@{
        SchemaVersion = $script:CacheSchemaVersion
        BuildId = $BuildId
        CommonCommit = $Baseline
        ContentHash = $contentHash
    } | ConvertTo-Json -Compress
    Write-AtomicFile "$CachePath.metadata" {
        param($path)
        [System.IO.File]::WriteAllText($path, $metadata, [System.Text.UTF8Encoding]::new($false))
    } | Out-Null
}

function Import-AffectedTestsArtifactCache([string]$ArchivePath, [string]$ArtifactName, [string]$Root, [int]$BuildId, [string]$Baseline) {
    $entryPrefix = "$ArtifactName/$script:ObjectNamePrefix"
    $imported = 0
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $entryName = $entry.FullName.Replace('\', '/')
            if (-not $entryName.StartsWith($entryPrefix, [System.StringComparison]::Ordinal) -or $entryName.EndsWith('/')) {
                continue
            }

            $objectName = $entryName.Substring($ArtifactName.Length + 1)
            $cachePath = Get-AffectedTestsCachePath $Root $objectName
            $content = $entry.Open()
            try {
                Write-AffectedTestsCacheEntry $content $cachePath $BuildId $Baseline
            }
            finally {
                $content.Dispose()
            }

            $imported++
        }
    }
    finally {
        $archive.Dispose()
    }

    return $imported
}

# Builds are ordered newest first. The provider reads the newest *succeeded* build whose source revision is the merge
# base or an ancestor of it. Seed only when a newer eligible build failed after its map collection succeeded.
function Select-AffectedTestsSeedBuild([object[]]$Builds, [string]$Baseline, [scriptblock]$IsAncestor, [scriptblock]$GetCollectedArtifact) {
    foreach ($build in $Builds) {
        if ($build.result -eq "canceled" -or [string]::IsNullOrWhiteSpace($build.sourceVersion)) {
            continue
        }

        if (-not ($build.sourceVersion -eq $Baseline -or (& $IsAncestor $build.sourceVersion))) {
            continue
        }

        if ($build.result -eq "succeeded") {
            return [pscustomobject]@{ Build = $build; Artifact = $null; Seed = $false }
        }

        $artifact = & $GetCollectedArtifact $build
        if ($null -ne $artifact) {
            return [pscustomobject]@{ Build = $build; Artifact = $artifact; Seed = $true }
        }
    }

    return [pscustomobject]@{ Build = $null; Artifact = $null; Seed = $false }
}

function Get-OptionalProperty([object]$Object, [string]$Name) {
    if ($null -ne $Object -and $Object.PSObject.Properties[$Name]) {
        return $Object.$Name
    }

    return $null
}

function Resolve-RepositoryPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Repository, $Path))
}

function Invoke-AzureDevOpsRest([string]$Uri) {
    return Invoke-RestMethod -Uri $Uri -Headers $script:Headers -TimeoutSec 100 -MaximumRetryCount 3 -RetryIntervalSec 5
}

function Get-CommonCommit([string]$DefaultBranch) {
    $shortName = $DefaultBranch -replace '^refs/heads/', ''
    foreach ($reference in @($DefaultBranch, $shortName, "origin/$shortName") | Select-Object -Unique) {
        $commit = & git -C $Repository merge-base HEAD $reference 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
            return $commit.Trim()
        }
    }

    return $null
}

function Invoke-AffectedTestsCacheSeed {
    # Non-Windows collection runs in its own matrix job and is disabled by default; its job gate has not been validated.
    if (-not $IsWindows) {
        Write-Host "Affected-test cache seed skipped: seeding is only enabled for Windows selection."
        return
    }

    $storage = Get-OptionalProperty (Get-OptionalProperty (Get-OptionalProperty (Get-Content -LiteralPath (Join-Path $Repository "global.json") -Raw | ConvertFrom-Json) "test") "affectedTests") "storage"
    if ((Get-OptionalProperty $storage "type") -ne "azureDevOpsArtifact") {
        Write-Host "Affected-test cache seed skipped: storage is not azureDevOpsArtifact."
        return
    }

    if ((Get-OptionalProperty $storage "enableLocalCache") -eq $false) {
        Write-Host "Affected-test cache seed skipped: the provider local cache is disabled."
        return
    }

    $project = Get-OptionalProperty $storage "project"
    $definitionId = Get-OptionalProperty $storage "buildDefinitionId"
    $artifactName = Get-OptionalProperty $storage "artifactName"
    if ([string]::IsNullOrEmpty($project) -or $null -eq $definitionId -or [string]::IsNullOrEmpty($artifactName)) {
        throw "global.json affected-test storage is missing project, buildDefinitionId, or artifactName."
    }

    $collection = @($CollectionUri, (Get-OptionalProperty $storage "collectionUri"), $env:CTS_COLLECTIONURI) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ($null -eq $collection) {
        throw "No Azure DevOps collection URI is configured."
    }

    $script:Headers = @{}
    $token = @($env:CTS_ACCESSTOKEN, $env:SYSTEM_ACCESSTOKEN) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
    if ($null -ne $token) {
        $script:Headers["Authorization"] = "Bearer $token"
    }

    $projectUri = "$($collection.TrimEnd('/'))/$([uri]::EscapeDataString($project))"
    $defaultBranch = Get-OptionalProperty $storage "defaultBranch"
    if ([string]::IsNullOrEmpty($defaultBranch)) {
        $defaultBranch = (Invoke-AzureDevOpsRest "$projectUri/_apis/build/definitions/$($definitionId)?api-version=7.1").repository.defaultBranch
    }

    $baseline = if ([string]::IsNullOrWhiteSpace($CommonCommit)) { Get-CommonCommit $defaultBranch } else { $CommonCommit.Trim() }
    if ([string]::IsNullOrEmpty($baseline)) {
        Write-Host "Affected-test cache seed skipped: no merge base with '$defaultBranch'."
        return
    }

    $root = if (-not [string]::IsNullOrWhiteSpace($CacheRoot)) {
        Resolve-RepositoryPath $CacheRoot
    }
    elseif (-not [string]::IsNullOrWhiteSpace((Get-OptionalProperty $storage "localCacheRoot"))) {
        Resolve-RepositoryPath $storage.localCacheRoot
    }
    else {
        $temporaryDirectory = @((Get-OptionalProperty $storage "tempDirectory"), $env:CTS_TEMPDIRECTORY, ".cts/temp") |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1
        [System.IO.Path]::Combine((Resolve-RepositoryPath $temporaryDirectory), "affected-tests", "azure-devops-cache")
    }

    $buildsUri = "$projectUri/_apis/build/builds?definitions=$definitionId&branchName=$([uri]::EscapeDataString($defaultBranch))" +
        "&statusFilter=completed&queryOrder=queueTimeDescending&%24top=$MaximumBuilds&api-version=7.1"
    $builds = @((Invoke-AzureDevOpsRest $buildsUri).value)
    $isAncestor = {
        param($sourceVersion)
        & git -C $Repository merge-base --is-ancestor $sourceVersion $baseline 2>$null
        return $LASTEXITCODE -eq 0
    }
    $getCollectedArtifact = {
        param($build)
        $timeline = Invoke-AzureDevOpsRest "$projectUri/_apis/build/builds/$($build.id)/timeline?api-version=7.1"
        $collected = @($timeline.records | Where-Object { $_.type -eq "Job" -and $_.name -eq $CollectionJobName -and $_.result -eq "succeeded" })
        if ($collected.Count -eq 0) {
            return $null
        }

        $artifacts = @((Invoke-AzureDevOpsRest "$projectUri/_apis/build/builds/$($build.id)/artifacts?api-version=7.1").value)
        return $artifacts | Where-Object { $_.name -eq $artifactName -and $_.resource.type -eq "Container" -and $_.resource.downloadUrl } | Select-Object -First 1
    }

    $selection = Select-AffectedTestsSeedBuild $builds $baseline $isAncestor $getCollectedArtifact
    if ($null -eq $selection.Build) {
        Write-Host "Affected-test cache seed skipped: no eligible '$defaultBranch' build among the latest $($builds.Count) for baseline $baseline."
        return
    }

    if (-not $selection.Seed) {
        Write-Host "Affected-test cache seed skipped: the provider already reads the newest eligible build $($selection.Build.id) ($($selection.Build.sourceVersion))."
        return
    }

    $build = $selection.Build
    $providerBuild = Select-AffectedTestsSeedBuild @($builds | Where-Object { $_.result -eq "succeeded" }) $baseline $isAncestor { $null }
    $providerDescription = if ($null -ne $providerBuild.Build) { "build $($providerBuild.Build.id) ($($providerBuild.Build.sourceVersion))" } else { "an older build" }
    Write-Host ("Affected-test cache seed: build $($build.id) ($($build.sourceVersion)) has result '$($build.result)' but its " +
        "'$CollectionJobName' job succeeded; the provider would read $providerDescription. Seeding $root for baseline $baseline.")

    $archivePath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($root), "seed-$($build.id)-$([guid]::NewGuid().ToString('N')).zip")
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($archivePath)) | Out-Null
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Invoke-WebRequest -Uri $selection.Artifact.resource.downloadUrl -Headers $script:Headers -OutFile $archivePath -MaximumRetryCount 3 -RetryIntervalSec 5
        $imported = Import-AffectedTestsArtifactCache $archivePath $artifactName $root ([int]$build.id) $baseline
    }
    finally {
        if ([System.IO.File]::Exists($archivePath)) {
            [System.IO.File]::Delete($archivePath)
        }
    }

    Write-Host "Affected-test cache seed: imported $imported maps from build $($build.id) in $([int]$timer.Elapsed.TotalSeconds)s."
}

function Assert-SeedEqual($Expected, $Actual, [string]$Name) {
    if ($Expected -cne $Actual) {
        throw "SeedSelfTest ${Name}: expected '$Expected' but was '$Actual'."
    }
}

function Invoke-SeedSelfTest {
    # Ground truth: the 18.13.0-preview.26562.1 provider read and validated a seeded entry at this path.
    $objectName = "affected-tests/v2/6e9a61bde3d41e2c75a44a898550f5810ff47bba60ae06c2fdad553d84a8a654/62558d8296fd836b49403bb180f71149d8d57fb69553788b6b83e918e533cf57.json"
    $expectedFile = "2337d7629a81a4aabf20de525d3406f5e8b02726597a13ba4f8065bd13534bb1.json"
    $root = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "affected-tests-seed-selftest-$([guid]::NewGuid().ToString('N'))")
    Assert-SeedEqual ([System.IO.Path]::Combine($root, "6e9a61bde3d41e2c75a44a898550f5810ff47bba60ae06c2fdad553d84a8a654", $expectedFile)) (Get-AffectedTestsCachePath $root $objectName) "CachePath"
    foreach ($invalid in @("affected-tests/v2/../x.json", "affected-tests/v2/repo/../../x.json", "affected-tests/v1/repo/x.json", "/affected-tests/v2/repo/x.json", "affected-tests/v2/repo/x.txt")) {
        Assert-SeedEqual $false (Test-AffectedTestsObjectName $invalid) "RejectsObjectName($invalid)"
    }
    Write-Output "PASS SeedCachePath"

    $isAncestor = { param($sourceVersion) $sourceVersion -in @("ancestor1", "ancestor2", "ancestor3") }
    $collected = { param($build) if ($build.collected) { [pscustomobject]@{ name = "maps" } } }
    $cases = @(
        @{ Name = "NewestSucceeded"; Expected = "1:False"; Builds = @(
            @{ id = 1; result = "succeeded"; sourceVersion = "ancestor1"; collected = $true },
            @{ id = 2; result = "failed"; sourceVersion = "ancestor2"; collected = $true }) },
        @{ Name = "RedMainCollected"; Expected = "2:True"; Builds = @(
            @{ id = 1; result = "failed"; sourceVersion = "descendant"; collected = $true },
            @{ id = 2; result = "failed"; sourceVersion = "BASELINE"; collected = $true },
            @{ id = 3; result = "succeeded"; sourceVersion = "ancestor3"; collected = $true }) },
        @{ Name = "SkipsFailedCollectionAndCanceled"; Expected = "4:True"; Builds = @(
            @{ id = 1; result = "failed"; sourceVersion = "ancestor1"; collected = $false },
            @{ id = 2; result = "canceled"; sourceVersion = "ancestor1"; collected = $true },
            @{ id = 3; result = "partiallySucceeded"; sourceVersion = "unrelated"; collected = $true },
            @{ id = 4; result = "partiallySucceeded"; sourceVersion = "ancestor2"; collected = $true },
            @{ id = 5; result = "succeeded"; sourceVersion = "ancestor3"; collected = $true }) },
        @{ Name = "NothingEligible"; Expected = ":False"; Builds = @(
            @{ id = 1; result = "failed"; sourceVersion = "ancestor1"; collected = $false }) }
    )
    foreach ($case in $cases) {
        $selection = Select-AffectedTestsSeedBuild @($case.Builds | ForEach-Object { [pscustomobject]$_ }) "baseline" $isAncestor $collected
        Assert-SeedEqual $case.Expected "$($selection.Build.id):$($selection.Seed)" "Select($($case.Name))"
    }
    Write-Output "PASS SeedBuildSelection"

    $archivePath = "$root.zip"
    try {
        $archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entry in @(
                @{ Name = "Maps/$objectName"; Content = '{"map":1}' },
                @{ Name = "Maps/affected-tests/v2/repo/second.json"; Content = '{"map":2}' },
                @{ Name = "Maps/collection.json"; Content = '{}' },
                @{ Name = "Other/affected-tests/v2/repo/ignored.json"; Content = '{}' })) {
                $writer = [System.IO.StreamWriter]::new($archive.CreateEntry($entry.Name).Open(), [System.Text.UTF8Encoding]::new($false))
                try { $writer.Write($entry.Content) } finally { $writer.Dispose() }
            }
        }
        finally {
            $archive.Dispose()
        }

        Assert-SeedEqual 2 (Import-AffectedTestsArtifactCache $archivePath "Maps" $root 42 "abc123") "ImportedCount"
        $cachePath = Get-AffectedTestsCachePath $root $objectName
        Assert-SeedEqual '{"map":1}' ([System.IO.File]::ReadAllText($cachePath)) "CacheContent"
        $contentHash = Get-Sha256Hex ([System.Text.Encoding]::UTF8.GetBytes('{"map":1}'))
        Assert-SeedEqual "{`"SchemaVersion`":2,`"BuildId`":42,`"CommonCommit`":`"abc123`",`"ContentHash`":`"$contentHash`"}" ([System.IO.File]::ReadAllText("$cachePath.metadata")) "CacheMetadata"
        Assert-SeedEqual 4 @(Get-ChildItem -LiteralPath $root -Recurse -File).Count "CacheFileCount"

        Remove-Item -LiteralPath $archivePath -Force
        $archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            $writer = [System.IO.StreamWriter]::new($archive.CreateEntry("Maps/affected-tests/v2/../../escape.json").Open())
            try { $writer.Write('{}') } finally { $writer.Dispose() }
        }
        finally {
            $archive.Dispose()
        }

        $rejected = $false
        try {
            Import-AffectedTestsArtifactCache $archivePath "Maps" $root 42 "abc123" | Out-Null
        }
        catch {
            $rejected = $_.Exception.Message.Contains("Unexpected affected-test map object name")
        }
        Assert-SeedEqual $true $rejected "RejectsTraversalEntry"
        Assert-SeedEqual $false ([System.IO.File]::Exists([System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($root), "escape.json"))) "NoEscapedFile"
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
    }
    Write-Output "PASS SeedCacheImport"
}

if ($SelfTest) {
    Invoke-SeedSelfTest
    exit 0
}

try {
    $Repository = [System.IO.Path]::GetFullPath($Repository)
    Invoke-AffectedTestsCacheSeed
}
catch {
    Write-Host "##vso[task.logissue type=warning]Affected-test cache seed failed; the provider lookup is unchanged: $($_.Exception.Message)"
}

exit 0
