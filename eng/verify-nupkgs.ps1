[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true)]
    [System.String] $configuration
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Unzip {
    param([string]$zipfile, [string]$outpath)

    Write-Verbose "Unzipping '$zipfile' to '$outpath'."

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

function Test-NuGetPackages {
    Write-Host "Starting Test-NuGetPackages."
    $expectedNumOfFiles = @{
        "MSTest.Internal.TestFx.Documentation"        = 10;
        "MSTest.TestFramework"                        = 93;
        "MSTest.TestAdapter"                          = 112;
        "MSTest"                                      = 5;
    }

    $packageDirectory = Resolve-Path "$PSScriptRoot/../artifacts/packages/$configuration"
    $tmpDirectory = Resolve-Path "$PSScriptRoot/../artifacts/tmp/$configuration"
    $nugetPackages = Get-ChildItem -Filter "*.nupkg" $packageDirectory -Recurse -Exclude "*.symbols.nupkg" | ForEach-Object { $_.FullName }

    Write-Host "Unzipping NuGet packages."
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

    Write-Host "Verifying NuGet packages files."
    $errors = @()
    foreach ($unzipNugetPackageDir in $unzipNugetPackageDirs) {
        try {
            $packageName = (Get-Item $unzipNugetPackageDir).BaseName
            $versionIndex = $packageName.LastIndexOf($version)
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
        Write-Error "There are $($errors.Count) errors:`n$($errors -join "`n")"
    }

    Write-Host "Completed Test-NuGetPackages."
}

Test-NuGetPackages
