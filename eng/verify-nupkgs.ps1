[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true)]
    [System.String] $configuration,

    # Rewrites the expected package shapes from the packed packages instead of comparing against them.
    [switch] $updateBaselines
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

$ErrorActionPreference = 'Stop'

function Unzip {
    param([string]$zipfile, [string]$outpath)

    Write-Verbose "Unzipping '$zipfile' to '$outpath'."

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

function Get-PackageShape {
    param([string]$unzipNugetPackageDir, [bool]$excludePdbFiles)

    $root = (Get-Item -LiteralPath $unzipNugetPackageDir).FullName.TrimEnd('\', '/')
    $entries = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        $entry = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
        if ($excludePdbFiles -and $entry.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        # NuGet names the OPC core-properties part with a new GUID on every pack.
        $entries.Add(($entry -replace '^package/services/metadata/core-properties/[^/]+\.psmdcp$', 'package/services/metadata/core-properties/{guid}.psmdcp'))
    }

    $entries.Sort([System.StringComparer]::Ordinal)
    return ,$entries.ToArray()
}

function Read-PackageShapeBaseline {
    param([string]$baselinePath)

    return ,@(Get-Content -LiteralPath $baselinePath | ForEach-Object { $_.Trim() } | Where-Object { $_ -and !$_.StartsWith('#') })
}

function Write-PackageShapeBaseline {
    param([string]$baselinePath, [string]$packageKey, [string[]]$entries, [bool]$excludePdbFiles)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Expected files of the $packageKey NuGet package, validated by eng/verify-nupkgs.ps1 after pack.")
    $lines.Add("# After an intentional package layout change, regenerate with: .\eng\verify-nupkgs.ps1 -configuration <Configuration> -updateBaselines")
    if ($excludePdbFiles) {
        $lines.Add("# PDB files are excluded because this package only contains them in some build modes.")
    }

    $lines.AddRange($entries)
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $baselinePath)
    [System.IO.File]::WriteAllText($baselinePath, ($lines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
}

function Compare-PackageShape {
    param([string]$packageKey, [string]$baselineName, [string[]]$expectedEntries, [string[]]$actualEntries)

    $expected = [System.Collections.Generic.HashSet[string]]::new($expectedEntries, [System.StringComparer]::Ordinal)
    $actual = [System.Collections.Generic.HashSet[string]]::new($actualEntries, [System.StringComparer]::Ordinal)
    $missing = @($expectedEntries | Where-Object { !$actual.Contains($_) })
    $unexpected = @($actualEntries | Where-Object { !$expected.Contains($_) })
    if ($missing.Count -eq 0 -and $unexpected.Count -eq 0) {
        return $null
    }

    $message = "Content of '$packageKey' does not match the expected package shape '$baselineName' ($($missing.Count) missing, $($unexpected.Count) unexpected):"
    foreach ($entry in $missing) {
        $message += "`n    - missing: $entry"
    }

    foreach ($entry in $unexpected) {
        $message += "`n    + unexpected: $entry"
    }

    return $message
}

function Confirm-NugetPackages {
    Write-Verbose "Starting Confirm-NugetPackages."
    # Each package listed here must have its expected file list in eng/package-shapes/<package>.txt.
    $packagesWithShapeBaselines = @(
        "MSTest"
        "MSTest.Analyzers"
        "MSTest.Extensions.Hosting"
        "MSTest.Sdk"
        "MSTest.SourceGeneration"
        "MSTest.TestAdapter"
        "MSTest.TestFramework"
        "MSTest.Windows.UIAutomation"
    )
    # These packages include PDBs only in some build modes; validate their stable non-symbol payload.
    $packagesWithOptionalPdbFiles = @("MSTest.Extensions.Hosting", "MSTest.Sdk", "MSTest.Windows.UIAutomation")
    $baselineDirectory = Join-Path $PSScriptRoot "package-shapes"

    $packageDirectory = Resolve-Path "$PSScriptRoot/../artifacts/packages/$configuration"
    $tmpDirectory = Resolve-Path "$PSScriptRoot/../artifacts/tmp/$configuration"
    $nugetPackages = Get-ChildItem -Filter "*.nupkg" $packageDirectory -Recurse -Exclude "*.symbols.nupkg" | ForEach-Object { $_.FullName }

    Write-Verbose "Unzipping NuGet packages."
    $unzipNugetPackageDirs = @()
    foreach ($nugetPackage in $nugetPackages) {
        $unzipNugetPackageDir = $(Join-Path $tmpDirectory (Get-Item $nugetPackage).BaseName)
        $unzipNugetPackageDirs += $unzipNugetPackageDir

        if (Test-Path -Path $unzipNugetPackageDir) {
            Remove-Item -Force -Recurse $unzipNugetPackageDir
        }

        Unzip $nugetPackage $unzipNugetPackageDir
    }

    $versionPropsXml = [xml](Get-Content $PSScriptRoot\Versions.props)
    $version = $versionPropsXml.Project.PropertyGroup.VersionPrefix | Where-Object { $null -ne $_ } | Select-Object -First 1
    if ($null -eq $version) {
        throw "version is null"
    }

    Write-Verbose "Package version is '$version'."

    Write-Verbose "Verifying NuGet packages files."
    $errors = @()
    $hasShapeMismatch = $false
    $verifiedPackageKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($unzipNugetPackageDir in $unzipNugetPackageDirs) {
        try {
            $packageName = (Get-Item $unzipNugetPackageDir).BaseName
            $nuspecFiles = @(Get-ChildItem -Path $unzipNugetPackageDir -Filter "*.nuspec" -File)
            if ($nuspecFiles.Count -ne 1) {
                $errors += "Expected exactly one nuspec in '$packageName', actual: $($nuspecFiles.Count)"
            }
            else {
                $nuspec = [xml](Get-Content -Raw $nuspecFiles[0].FullName)
                $metadata = $nuspec.package.metadata

                if ([string]::IsNullOrWhiteSpace([string]$metadata.tags)) {
                    $errors += "Package '$packageName' does not define tags"
                }

                $readme = [string]$metadata.readme
                if ([string]::IsNullOrWhiteSpace($readme)) {
                    $errors += "Package '$packageName' does not define a readme"
                }
                elseif (!(Test-Path -Path (Join-Path $unzipNugetPackageDir $readme) -PathType Leaf)) {
                    $errors += "Package '$packageName' readme '$readme' is not included in the package"
                }

                $icon = [string]$metadata.icon
                if ([string]::IsNullOrWhiteSpace($icon)) {
                    $errors += "Package '$packageName' does not define an icon"
                }
                elseif (!(Test-Path -Path (Join-Path $unzipNugetPackageDir $icon) -PathType Leaf)) {
                    $errors += "Package '$packageName' icon '$icon' is not included in the package"
                }
            }

            $versionIndex = $packageName.LastIndexOf($version)
            if ($versionIndex -lt 0) {
                continue
            }

            $packageKey = $packageName.Substring(0, $versionIndex - 1) # Remove last dot
            Write-Verbose "Verifying package '$packageKey'."
            $null = $verifiedPackageKeys.Add($packageKey)

            if ($packagesWithShapeBaselines -notcontains $packageKey) {
                $errors += "Package '$packageKey' has no expected package shape; add it to `$packagesWithShapeBaselines in eng/verify-nupkgs.ps1 and run the script with -updateBaselines"
                continue
            }

            $excludePdbFiles = $packagesWithOptionalPdbFiles -contains $packageKey
            $actualEntries = Get-PackageShape $unzipNugetPackageDir $excludePdbFiles
            $baselineName = "eng/package-shapes/$packageKey.txt"
            $baselinePath = Join-Path $baselineDirectory "$packageKey.txt"
            if ($updateBaselines) {
                Write-PackageShapeBaseline $baselinePath $packageKey $actualEntries $excludePdbFiles
                Write-Host "Updated '$baselineName' ($($actualEntries.Count) files)."
            }
            elseif (!(Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
                $errors += "Expected package shape '$baselineName' for '$packageKey' was not found"
                $hasShapeMismatch = $true
            }
            else {
                $shapeError = Compare-PackageShape $packageKey $baselineName (Read-PackageShapeBaseline $baselinePath) $actualEntries
                if ($shapeError) {
                    $errors += $shapeError
                    $hasShapeMismatch = $true
                }
            }
        }
        finally {
            if (Test-Path $unzipNugetPackageDir) {
                Remove-Item -Force -Recurse $unzipNugetPackageDir | Out-Null
            }
        }
    }

    $isMSTestProductBuild = $false
    foreach ($expectedPackageKey in $packagesWithShapeBaselines) {
        if ($verifiedPackageKeys.Contains($expectedPackageKey)) {
            $isMSTestProductBuild = $true
            break
        }
    }

    if ($isMSTestProductBuild) {
        foreach ($expectedPackageKey in $packagesWithShapeBaselines) {
            if (!$verifiedPackageKeys.Contains($expectedPackageKey)) {
                $errors += "Expected package '$expectedPackageKey' was not found"
            }
        }

        foreach ($baselineFile in Get-ChildItem -LiteralPath $baselineDirectory -Filter "*.txt" -File -ErrorAction SilentlyContinue) {
            if ($packagesWithShapeBaselines -notcontains $baselineFile.BaseName) {
                $errors += "Expected package shape 'eng/package-shapes/$($baselineFile.Name)' does not belong to a validated package; remove it or add the package to `$packagesWithShapeBaselines"
            }
        }
    }

    if ($hasShapeMismatch) {
        $errors += "If the package layout change is intentional, run '.\eng\verify-nupkgs.ps1 -configuration $configuration -updateBaselines' after packing and commit the updated eng/package-shapes files."
    }

    if ($errors) {
        Write-Error "Validation of NuGet packages failed with $($errors.Count) errors:`n$($errors -join "`n")"
    } else {
        Write-Host "Successfully validated content and metadata of NuGet packages"
    }
}

Confirm-NugetPackages
