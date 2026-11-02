[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string[]]$SettingsFiles,
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ProjectFile,
    [string]$NuGetPackageRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-PatternMatch {
    param(
        [string[]]$Patterns,
        [string]$Value,
        [bool]$Expected,
        [string]$Message
    )

    $options = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant -bor
        [System.Text.RegularExpressions.RegexOptions]::ECMAScript
    $matched = $false
    foreach ($pattern in $Patterns) {
        if ([regex]::IsMatch($Value, $pattern, $options)) {
            $matched = $true
            break
        }
    }

    if ($matched -ne $Expected) {
        throw "$Message Value: '$Value'."
    }
}

if ($null -eq $SettingsFiles -or $SettingsFiles.Count -eq 0) {
    $objDirectory = Join-Path $RepositoryRoot "artifacts/obj"
    $SettingsFiles = @(Get-ChildItem -LiteralPath $objDirectory -Filter coverage.config -Recurse -File |
        Where-Object { $_.FullName -match "[\\/]$([regex]::Escape($Configuration))[\\/]" } |
        Select-Object -ExpandProperty FullName)
}

if ($SettingsFiles.Count -eq 0) {
    throw "No generated coverage settings were found for '$Configuration'."
}

$separator = if ($RepositoryRoot.StartsWith("/")) { "/" } else { "\" }
$sourceRoot = $RepositoryRoot.TrimEnd("\", "/") + $separator
$projectContexts = @{}

foreach ($settingsFile in $SettingsFiles) {
    $projectMatch = [regex]::Match($settingsFile, '[\\/]artifacts[\\/]obj[\\/](?<project>[^\\/]+)[\\/]')
    $projectName = if ($ProjectFile) {
        [System.IO.Path]::GetFileNameWithoutExtension($ProjectFile)
    } elseif ($projectMatch.Success) {
        $projectMatch.Groups["project"].Value
    } else {
        throw "Supply ProjectFile for coverage settings outside the repository artifacts layout: '$settingsFile'."
    }

    if (-not $projectContexts.ContainsKey($projectName)) {
        $assetsPath = Join-Path $RepositoryRoot "artifacts/obj/$projectName/project.assets.json"
        $restore = if (Test-Path -LiteralPath $assetsPath) {
            (Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json).project.restore
        } else {
            $null
        }
        $owningProject = if ($ProjectFile) { $ProjectFile } elseif ($null -ne $restore) { $restore.projectPath }
        $packagesRoot = if ($NuGetPackageRoot) { $NuGetPackageRoot } elseif ($null -ne $restore) { $restore.packagesPath }
        if (-not $owningProject -or -not $packagesRoot) {
            throw "Coverage validation requires the restored project identity and package root for '$projectName', or explicit ProjectFile and NuGetPackageRoot."
        }

        $project = [System.Xml.XmlDocument]::new()
        $project.Load($owningProject)
        $optIn = $project.SelectSingleNode("/Project/PropertyGroup/CodeCoverageIncludeSourceLinkedProductionCode")
        $expectsLinkedSources = $false
        if ($null -ne $optIn -and -not [bool]::TryParse($optIn.InnerText, [ref]$expectsLinkedSources)) {
            throw "The source-linked coverage opt-in must be an explicit boolean in '$owningProject'."
        }

        $projectContexts[$projectName] = @{
            PackagesRoot = $packagesRoot
            ExpectsLinkedSources = $expectsLinkedSources
        }
    }

    $context = $projectContexts[$projectName]
    $settings = [System.Xml.XmlDocument]::new()
    $settings.Load($settingsFile)
    $moduleIncludes = @($settings.SelectNodes("/Configuration/CodeCoverage/ModulePaths/Include/ModulePath") |
        ForEach-Object { $_.InnerText })
    $moduleExcludes = @($settings.SelectNodes("/Configuration/CodeCoverage/ModulePaths/Exclude/ModulePath") |
        ForEach-Object { $_.InnerText })
    $sourceExcludes = @($settings.SelectNodes("/Configuration/CodeCoverage/Sources/Exclude/Source") |
        ForEach-Object { $_.InnerText })
    $sourceIncludes = @($settings.SelectNodes("/Configuration/CodeCoverage/Sources/Include/Source") |
        ForEach-Object { $_.InnerText })

    $attributes = $settings.SelectSingleNode("/Configuration/CodeCoverage/Attributes")
    $attributeExcludes = $settings.SelectSingleNode("/Configuration/CodeCoverage/Attributes/Exclude")
    if ($null -eq $attributes -or $attributes.GetAttribute("mergeDefaults") -ine "False" -or
        $null -eq $attributeExcludes -or $attributeExcludes.HasChildNodes) {
        throw "Production coverage must not exclude attribute-marked bodies in '$settingsFile'."
    }

    $doesNotReturn = $settings.SelectSingleNode("/Configuration/DoesNotReturnAttribute")
    if ($null -eq $doesNotReturn -or $doesNotReturn.InnerText -ine "None") {
        throw "Coverage must retain reachable assertion-scope continuations in '$settingsFile'."
    }

    foreach ($pattern in $moduleIncludes) {
        if (-not $pattern.EndsWith('\.dll$') -and -not $pattern.EndsWith('\.exe$')) {
            throw "The assembly regex lost its literal extension in '$settingsFile': '$pattern'."
        }

        $assemblyName = $pattern.Substring(2, $pattern.Length - 3).Replace('\.', '.')
        Assert-PatternMatch $moduleIncludes $assemblyName $true "A referenced production assembly is missing from '$settingsFile'."
        Assert-PatternMatch @($pattern) $assemblyName.Replace(".", "X") $false "Assembly-name punctuation is not escaped in '$settingsFile'."
    }

    foreach ($assembly in @(
        "Microsoft.Testing.TestInfrastructure.dll",
        "C:\test-output\Microsoft.Testing.TestInfrastructure.dll",
        "/test-output/Microsoft.Testing.TestInfrastructure.dll"
    )) {
        Assert-PatternMatch $moduleExcludes $assembly $true "Test infrastructure must be excluded by '$settingsFile'."
    }

    Assert-PatternMatch $moduleExcludes "Microsoft.Testing.TestInfrastructure.Tools.dll" $false "The infrastructure exclusion is too broad in '$settingsFile'."

    foreach ($relativePath in @(
        "test/Utilities/Microsoft.Testing.TestInfrastructure/AcceptanceSourceGen.cs",
        "artifacts/obj/Generated.cs"
    )) {
        $path = $sourceRoot + $relativePath.Replace("/", $separator)
        Assert-PatternMatch $sourceExcludes $path $true "Infrastructure or generated sources are not excluded by '$settingsFile'."
    }

    $packageSeparator = if ($context.PackagesRoot.StartsWith("/")) { "/" } else { "\" }
    $packagePath = $context.PackagesRoot.TrimEnd("\", "/") + $packageSeparator +
        "coverage-validation/1.0.0/src/Source.cs".Replace("/", $packageSeparator)
    Assert-PatternMatch $sourceExcludes $packagePath $true "NuGet package sources are not excluded by '$settingsFile'."

    foreach ($path in @(
        "C:\coverage-validation\src\Polyfills\Index.cs",
        "/coverage-validation/src/Polyfills/Index.cs"
    )) {
        Assert-PatternMatch $sourceExcludes $path $false "Compatibility bodies must remain measurable in '$settingsFile'."
    }

    foreach ($path in @(
        "C:\coverage-validation\src\Polyfills\Ensure.cs",
        "/coverage-validation/src/Polyfills/Ensure.cs",
        "C:\coverage-validation\src\Polyfills\ProcessExtensions.cs",
        "/coverage-validation/src/Polyfills/ProcessExtensions.cs"
    )) {
        Assert-PatternMatch $sourceExcludes $path $false "Validation and process-lifetime logic must remain measurable in '$settingsFile'."
    }

    $productionPath = $sourceRoot + "src/Platform/Microsoft.Testing.Platform/Helpers/TaskExtensions.cs".Replace("/", $separator)
    Assert-PatternMatch $sourceExcludes $productionPath $false "Production sources must remain measurable in '$settingsFile'."

    $includeTestAssembly = $settings.SelectSingleNode("/Configuration/IncludeTestAssembly")
    if ($context.ExpectsLinkedSources) {
        if ($sourceIncludes.Count -eq 0) {
            throw "Source-linked coverage must include production sources in '$settingsFile'."
        }

        if ($null -eq $includeTestAssembly -or $includeTestAssembly.InnerText -ine "True") {
            throw "Source-linked coverage must enable its assembly in '$settingsFile'."
        }

        Assert-PatternMatch $sourceIncludes $productionPath $true "Linked production sources must remain measurable in '$settingsFile'."
        Assert-PatternMatch $sourceIncludes "/_/src/Platform/Microsoft.Testing.Platform/Helpers/TaskExtensions.cs" $true "Remapped production sources must remain measurable in '$settingsFile'."
        $testPath = $sourceRoot + "test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestApplicationBuilderTests.cs".Replace("/", $separator)
        Assert-PatternMatch $sourceIncludes $testPath $false "Test implementation must not contribute to source-linked coverage in '$settingsFile'."
        Assert-PatternMatch $sourceIncludes "/_/test/UnitTests/Microsoft.Testing.Platform.UnitTests/TestApplicationBuilderTests.cs" $false "Remapped tests must not contribute to source-linked coverage in '$settingsFile'."
    } elseif ($sourceIncludes.Count -ne 0 -or ($null -ne $includeTestAssembly -and $includeTestAssembly.InnerText -ieq "True")) {
        throw "Ordinary coverage must not include test assemblies or source-linked include rules in '$settingsFile'."
    }
}

Write-Host "Validated $($SettingsFiles.Count) generated code coverage settings file(s)."
