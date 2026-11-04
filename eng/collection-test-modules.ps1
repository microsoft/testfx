function Get-CTSComparisonModule {
    param(
        [string]$Repository,
        [string]$Configuration
    )

    $binDirectory = Join-Path -Path $Repository -ChildPath "artifacts" -AdditionalChildPath "bin"
    $hashes = @{}
    $modules = @(
        foreach ($apphost in Get-ChildItem -LiteralPath $binDirectory -Filter "*.exe" -Recurse -File | Sort-Object FullName) {
            $relativePath = [System.IO.Path]::GetRelativePath($binDirectory, $apphost.FullName).Replace("\", "/")
            $segments = $relativePath.Split("/")
            if ($segments.Length -lt 4 -or $segments[1] -ne $Configuration -or
                $apphost.BaseName -notmatch "(UnitTests|Acceptance\.IntegrationTests|FSharpPlayground|NUnitPlayground|Playground)$" -or
                -not (Test-Path -LiteralPath (Join-Path $apphost.DirectoryName "Microsoft.Testing.Extensions.AffectedTests.dll"))) {
                continue
            }

            $modulePath = $apphost.FullName
            if (Test-Path -LiteralPath (Join-Path $apphost.DirectoryName "$($apphost.BaseName).runtimeconfig.json")) {
                # CTS deduplicates input files by hash. Apphosts can be identical across different test builds.
                $modulePath = Join-Path $apphost.DirectoryName "$($apphost.BaseName).dll"
            }

            $hash = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
            if ($hashes.ContainsKey($hash)) {
                throw "CTS would discard an identically hashed module: '$modulePath' and '$($hashes[$hash])'."
            }

            $hashes[$hash] = $modulePath
            [pscustomobject]@{
                Path = $modulePath
                RelativePath = [System.IO.Path]::GetRelativePath($Repository, $modulePath).Replace("\", "/")
                Project = $segments[0]
                Framework = $segments[2]
                SHA256 = $hash
            }
        }
    )

    if ($modules.Count -eq 0) {
        throw "No affected-test-capable $Configuration test modules were found under '$binDirectory'."
    }

    return $modules
}
