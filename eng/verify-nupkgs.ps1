[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true)]
    [System.String] $configuration
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

$ErrorActionPreference = 'Stop'

function Unzip {
    param([string]$zipfile, [string]$outpath)

    Write-Verbose "Unzipping '$zipfile' to '$outpath'."

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

function Confirm-NugetPackages {
    Write-Verbose "Starting Confirm-NugetPackages."
    $expectedNumOfFiles = @{
        "MSTest.Sdk"                            = 13;
        "MSTest.Internal.TestFx.Documentation"  = 10;
        "MSTest.TestFramework"                  = 130;
        "MSTest.TestAdapter"                    = 166;
        "MSTest"                                = 6;
        "MSTest.Analyzers"                      = 10;
    }

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
    foreach ($unzipNugetPackageDir in $unzipNugetPackageDirs) {
        try {
            $packageName = (Get-Item $unzipNugetPackageDir).BaseName
            $versionIndex = $packageName.LastIndexOf($version)
            if ($versionIndex -lt 0) {
                continue
            }

            $packageKey = $packageName.Substring(0, $versionIndex - 1) # Remove last dot
            Write-Verbose "Verifying package '$packageKey'."

            $actualNumOfFiles = (Get-ChildItem -Recurse -File -Path $unzipNugetPackageDir).Count
            if ($expectedNumOfFiles[$packageKey] -ne $actualNumOfFiles) {
                $errors += "Number of files are not equal for '$packageKey', expected: $($expectedNumOfFiles[$packageKey]) actual: $actualNumOfFiles"
            }
        }
        finally {
            if (Test-Path $unzipNugetPackageDir) {
                Remove-Item -Force -Recurse $unzipNugetPackageDir | Out-Null
            }
        }
    }

    if ($errors) {
        Write-Error "Validation of NuGet packages failed with $($errors.Count) errors:`n$($errors -join "`n")"
    } else {
        Write-Host "Successfully validated content of NuGet packages"
    }
}

Confirm-NugetPackages
