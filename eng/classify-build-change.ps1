[CmdletBinding(DefaultParameterSetName = "GitDiff")]
param(
    [Parameter(Mandatory, ParameterSetName = "GitDiff")]
    [string]$Base,

    [Parameter(Mandatory, ParameterSetName = "GitDiff")]
    [string]$Head,

    [Parameter(ParameterSetName = "GitDiff")]
    [string]$Repository,

    [Parameter(Mandatory, ParameterSetName = "SelfTest")]
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

# Keep this allowlist intentionally narrow. These paths configure GitHub-only automation or repository
# administration and are not consumed by the product build. Build pipelines, dependency configuration,
# general documentation, and unknown paths must remain product-affecting so classification fails closed.
$infrastructureDirectories = @(
    ".github/agents/",
    ".github/aw/",
    ".github/copilot/",
    ".github/instructions/",
    ".github/ISSUE_TEMPLATE/",
    ".github/policies/",
    ".github/scripts/",
    ".github/skills/",
    ".github/workflows/"
)
$infrastructureFiles = @(
    ".github/copilot-instructions.md",
    ".github/PULL_REQUEST_TEMPLATE.md",
    ".github/release.yml",
    "eng/vendored-files.md"
)

function Test-SamplesAffectingPath {
    param([string]$Path)

    return (
        $Path.StartsWith("samples/", [System.StringComparison]::Ordinal) -or
        $Path.Equals("eng/build-samples.ps1", [System.StringComparison]::Ordinal))
}

function Test-InfrastructureOnlyPath {
    param([string]$Path)

    if ([string]::IsNullOrEmpty($Path)) {
        return $false
    }

    foreach ($directory in $infrastructureDirectories) {
        if ($Path.StartsWith($directory, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $infrastructureFiles -ccontains $Path
}

function Get-Classification {
    param([AllowEmptyCollection()][string[]]$Paths)

    if (@($Paths).Count -eq 0) {
        return [PSCustomObject][ordered]@{
            HasProductChanges = $true
            HasSamplesChanges = $true
            InfrastructureOnly = $false
        }
    }

    $hasProductChanges = $false
    $hasSamplesChanges = $false
    $infrastructureOnly = $true

    foreach ($path in $Paths) {
        if (Test-SamplesAffectingPath $path) {
            $hasSamplesChanges = $true
            $infrastructureOnly = $false
        }
        elseif (-not (Test-InfrastructureOnlyPath $path)) {
            $hasProductChanges = $true
            $infrastructureOnly = $false
        }
    }

    return [PSCustomObject][ordered]@{
        HasProductChanges = $hasProductChanges
        HasSamplesChanges = $hasSamplesChanges
        InfrastructureOnly = $infrastructureOnly
    }
}

function Invoke-Git {
    param(
        [string]$Repository,
        [string[]]$Arguments
    )

    $output = @(& git -C $Repository @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return $output
}

function Get-GitDiffClassification {
    param(
        [string]$BaseRevision,
        [string]$HeadRevision,
        [string]$Repository
    )

    if ([string]::IsNullOrEmpty($Repository)) {
        $repositoryOutput = @(Invoke-Git $PSScriptRoot @("rev-parse", "--show-toplevel"))
        $Repository = $repositoryOutput[0]
    }

    $paths = @(Invoke-Git $Repository @(
        "diff",
        "--name-only",
        "--no-renames",
        "--diff-filter=ACDMRTUXB",
        $BaseRevision,
        $HeadRevision,
        "--"
    ))

    return Get-Classification $paths
}

function Format-Classification {
    param([PSCustomObject]$Classification)

    $hasProductChanges = $Classification.HasProductChanges.ToString().ToLowerInvariant()
    $hasSamplesChanges = $Classification.HasSamplesChanges.ToString().ToLowerInvariant()
    $infrastructureOnly = $Classification.InfrastructureOnly.ToString().ToLowerInvariant()

    return "hasProductChanges=$hasProductChanges;hasSamplesChanges=$hasSamplesChanges;infrastructureOnly=$infrastructureOnly"
}

function Assert-Classification {
    param(
        [string]$Name,
        [string[]]$Paths,
        [bool]$HasProductChanges,
        [bool]$HasSamplesChanges,
        [bool]$InfrastructureOnly
    )

    $actual = Get-Classification $Paths
    if (
        $actual.HasProductChanges -ne $HasProductChanges -or
        $actual.HasSamplesChanges -ne $HasSamplesChanges -or
        $actual.InfrastructureOnly -ne $InfrastructureOnly
    ) {
        throw "Self-test '$Name' expected product=$HasProductChanges, samples=$HasSamplesChanges, infrastructure=$InfrastructureOnly but got $(Format-Classification $actual)."
    }
}

function Invoke-GitDiffSelfTest {
    $repository = Join-Path ([System.IO.Path]::GetTempPath()) "testfx-build-change-classifier-$([System.Guid]::NewGuid().ToString('N'))"
    [System.IO.Directory]::CreateDirectory($repository) | Out-Null

    try {
        $null = Invoke-Git $repository @("init", "--quiet")
        $null = Invoke-Git $repository @("config", "user.name", "Build change classifier")
        $null = Invoke-Git $repository @("config", "user.email", "classifier@example.invalid")

        [System.IO.File]::WriteAllText((Join-Path $repository "README.md"), "Base`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Base")
        $baseRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]

        $workflowDirectory = Join-Path $repository ".github/workflows"
        [System.IO.Directory]::CreateDirectory($workflowDirectory) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $workflowDirectory "triage.yml"), "name: Triage`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Infrastructure")
        $infrastructureRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $baseRevision $infrastructureRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=false;hasSamplesChanges=false;infrastructureOnly=true") {
            throw "Git self-test expected an infrastructure-only diff but got '$formattedClassification'."
        }

        $samplesDirectory = Join-Path $repository "samples"
        [System.IO.Directory]::CreateDirectory($samplesDirectory) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $samplesDirectory "Sample.cs"), "class Sample { }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Samples")
        $samplesRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $infrastructureRevision $samplesRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=false;hasSamplesChanges=true;infrastructureOnly=false") {
            throw "Git self-test expected a samples-only diff but got '$formattedClassification'."
        }

        $productDirectory = Join-Path $repository "src"
        [System.IO.Directory]::CreateDirectory($productDirectory) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $productDirectory "Product.cs"), "class Product { }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Product")
        $productRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $baseRevision $productRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=true;hasSamplesChanges=true;infrastructureOnly=false") {
            throw "Git self-test expected a mixed infrastructure, samples, and product diff but got '$formattedClassification'."
        }

        $classification = Get-GitDiffClassification $productRevision $productRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=true;hasSamplesChanges=true;infrastructureOnly=false") {
            throw "Git self-test expected an empty diff to run full validation but got '$formattedClassification'."
        }

        try {
            $null = Get-GitDiffClassification "missing-revision" $productRevision $repository
            throw "Git self-test expected an invalid revision to fail."
        }
        catch {
            if ($_.Exception.Message -eq "Git self-test expected an invalid revision to fail.") {
                throw
            }
        }
    }
    finally {
        Remove-Item -LiteralPath $repository -Recurse -Force
    }
}

function Invoke-SelfTest {
    foreach ($pathCase in @(
        @{ Path = ".github/workflows/triage.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/scripts/triage.py"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/policies/LabelManagement.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/PULL_REQUEST_TEMPLATE.md"; Infrastructure = $true; Samples = $false },
        @{ Path = "eng/vendored-files.md"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/dependabot.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "azure-pipelines.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "eng/pipelines/steps/build.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "eng/vendored-files.json"; Infrastructure = $false; Samples = $false },
        @{ Path = "docs/operational.md"; Infrastructure = $false; Samples = $false },
        @{ Path = "src/Product.cs"; Infrastructure = $false; Samples = $false },
        @{ Path = "unknown/new-location.txt"; Infrastructure = $false; Samples = $false },
        @{ Path = "samples/public/Sample.cs"; Infrastructure = $false; Samples = $true },
        @{ Path = "eng/build-samples.ps1"; Infrastructure = $false; Samples = $true }
    )) {
        $actualInfrastructure = Test-InfrastructureOnlyPath $pathCase.Path
        $actualSamples = Test-SamplesAffectingPath $pathCase.Path
        if ($actualInfrastructure -ne $pathCase.Infrastructure -or $actualSamples -ne $pathCase.Samples) {
            throw "Path self-test '$($pathCase.Path)' produced infrastructure=$actualInfrastructure, samples=$actualSamples."
        }
    }

    Assert-Classification "Infrastructure only" @(
        ".github/workflows/triage.yml",
        ".github/scripts/triage.py",
        "eng/vendored-files.md"
    ) $false $false $true
    Assert-Classification "Infrastructure and product" @(
        ".github/workflows/triage.yml",
        "src/Product.cs"
    ) $true $false $false
    Assert-Classification "Infrastructure and samples" @(
        ".github/policies/LabelManagement.yml",
        "samples/public/Sample.cs"
    ) $false $true $false
    Assert-Classification "Samples only" @(
        "samples/public/Sample.cs",
        "eng/build-samples.ps1"
    ) $false $true $false
    Assert-Classification "XML documentation path remains product-affecting" @(
        "src/TestFramework/Assert.cs"
    ) $true $false $false
    Assert-Classification "Unknown path" @(
        "unknown/new-location.txt"
    ) $true $false $false
    Assert-Classification "Empty diff" @() $true $true $false

    Invoke-GitDiffSelfTest

    Write-Output "Build change classifier self-tests passed."
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

Write-Output (Format-Classification (Get-GitDiffClassification $Base $Head $Repository))
