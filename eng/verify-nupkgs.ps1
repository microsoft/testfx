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
        "MSTest.Internal.TestFx.Documentation"        = 58;
        "MSTest.TestFramework"                        = 21;
        "MSTest.TestAdapter"                          = 602;
        "MSTest"                                      = 16;
    }

    $packageDirectory = Resolve-Path (Join-Path $PSScriptRoot "../artifacts/packages/$configuration")
    $tmpDirectory = Resolve-Path (Join-Path $PSScriptRoot "../artifacts/tmp/$configuration")
    $nugetPackages = Get-ChildItem -Filter "*.nupkg" $packageDirectory -Recurse -Exclude "*.symbols.nupkg" | ForEach-Object { $_.FullName }

    Write-Host "Unzipping NuGet packages."
    $unzipNugetPackageDirs = New-Object System.Collections.Generic.List[System.Object]
    foreach ($nugetPackage in $nugetPackages) {
        $unzipNugetPackageDir = $(Join-Path $tmpDirectory $(Get-Item $nugetPackage).BaseName)
        $unzipNugetPackageDirs.Add($unzipNugetPackageDir)

        if (Test-Path -Path $unzipNugetPackageDir) {
            Remove-Item -Force -Recurse $unzipNugetPackageDir
        }

        Unzip $nugetPackage $unzipNugetPackageDir
    }

    $version = ([xml](Get-Content $PSScriptRoot\Versions.props)).Project.PropertyGroup.VersionPrefix
    Write-Verbose "Package version is '$version'."

    Write-Host "Verifying NuGet packages files."
    $errors = @()
    foreach ($unzipNugetPackageDir in $unzipNugetPackageDirs) {
        try {
            $packageFullName = (Get-Item $unzipNugetPackageDir).BaseName
            $versionIndex = $packageFullName.LastIndexOf($version)
            Write-Verbose "Found $version at index $versionIndex in $packageFullName"

            $packageKey = $packageFullName.Substring(0, $versionIndex - 1) # Remove last dot
            Write-Verbose "Verifying package '$packageKey'."

            $actualNumOfFiles = (Get-ChildItem -Recurse -File -Path $unzipNugetPackageDir).Count
            if ($expectedNumOfFiles[$packageKey] -ne $actualNumOfFiles) {
                $errors += "Number of files are not equal for '$packageKey', expected: $($expectedNumOfFiles[$packageKey]) actual: $actualNumOfFiles"
            }
        }
        finally {
            if ($null -ne $unzipNugetPackageDir -and (Test-Path $unzipNugetPackageDir)) {
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
