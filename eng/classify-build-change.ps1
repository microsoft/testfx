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

# Keep this allowlist intentionally narrow. These paths configure GitHub-only automation, AI-tool guidance,
# or repository administration and are not consumed by the product build. Build pipelines, dependency configuration,
# general documentation, and unknown paths must remain product-affecting so classification fails closed.
$infrastructureDirectories = @(
    ".agents/skills/",
    ".copilot/curate/",
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
    "AGENTS.md",
    "eng/vendored-files.md"
)

function Test-SamplesAffectingPath {
    param([string]$Path)

    return (
        $Path.StartsWith("samples/", [System.StringComparison]::Ordinal) -or
        $Path.Equals("eng/build-samples.ps1", [System.StringComparison]::Ordinal) -or
        $Path.Equals("eng/test-samples.ps1", [System.StringComparison]::Ordinal) -or
        $Path.Equals("eng/test-samples-output.ps1", [System.StringComparison]::Ordinal) -or
        $Path.Equals("eng/samples-tools.ps1", [System.StringComparison]::Ordinal))
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

        $engDirectory = Join-Path $repository "eng"
        [System.IO.Directory]::CreateDirectory($engDirectory) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $engDirectory "test-samples-output.ps1"), "Write-Output 'Sample checks'`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Sample output checks")
        $sampleChecksRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $samplesRevision $sampleChecksRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=false;hasSamplesChanges=true;infrastructureOnly=false") {
            throw "Git self-test expected a sample-output-check-only diff to run sample validation but got '$formattedClassification'."
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

        $skillsDirectory = [System.IO.Path]::Combine($repository, ".agents", "skills", "msbuild")
        $curateDirectory = [System.IO.Path]::Combine($repository, ".copilot", "curate")
        [System.IO.Directory]::CreateDirectory($skillsDirectory) | Out-Null
        [System.IO.Directory]::CreateDirectory($curateDirectory) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $skillsDirectory "SKILL.md"), "# MSBuild guidance`n")
        [System.IO.File]::WriteAllText((Join-Path $curateDirectory "manifest.yml"), "version: 1`n")
        [System.IO.File]::WriteAllText((Join-Path $repository "AGENTS.md"), "Repository guidance`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Curated guidance")
        $guidanceRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $productRevision $guidanceRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=false;hasSamplesChanges=false;infrastructureOnly=true") {
            throw "Git self-test expected a curated-guidance-only diff but got '$formattedClassification'."
        }

        $guidancePath = Join-Path $skillsDirectory "REFERENCE.md"
        [System.IO.File]::Move((Join-Path $skillsDirectory "SKILL.md"), $guidancePath)
        [System.IO.File]::Delete((Join-Path $curateDirectory "manifest.yml"))
        [System.IO.File]::Delete((Join-Path $repository "AGENTS.md"))
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Rename and delete curated guidance")
        $previousRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        $classification = Get-GitDiffClassification $guidanceRevision $previousRevision $repository
        $formattedClassification = Format-Classification $classification
        if ($formattedClassification -cne "hasProductChanges=false;hasSamplesChanges=false;infrastructureOnly=true") {
            throw "Git self-test expected a curated-guidance rename and deletion diff but got '$formattedClassification'."
        }

        $productGuidancePath = Join-Path $productDirectory "Guidance.md"
        $samplesGuidancePath = Join-Path $samplesDirectory "Guidance.md"
        foreach ($moveCase in @(
            @{ Name = "Move guidance into product"; From = $guidancePath; To = $productGuidancePath; Expected = "hasProductChanges=true;hasSamplesChanges=false;infrastructureOnly=false" },
            @{ Name = "Move product into guidance"; From = $productGuidancePath; To = $guidancePath; Expected = "hasProductChanges=true;hasSamplesChanges=false;infrastructureOnly=false" },
            @{ Name = "Move guidance into samples"; From = $guidancePath; To = $samplesGuidancePath; Expected = "hasProductChanges=false;hasSamplesChanges=true;infrastructureOnly=false" },
            @{ Name = "Move samples into guidance"; From = $samplesGuidancePath; To = $guidancePath; Expected = "hasProductChanges=false;hasSamplesChanges=true;infrastructureOnly=false" }
        )) {
            [System.IO.File]::Move($moveCase.From, $moveCase.To)
            $null = Invoke-Git $repository @("add", "--all")
            $null = Invoke-Git $repository @("commit", "--quiet", "-m", $moveCase.Name)
            $revision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
            $classification = Get-GitDiffClassification $previousRevision $revision $repository
            $formattedClassification = Format-Classification $classification
            if ($formattedClassification -cne $moveCase.Expected) {
                throw "Git self-test '$($moveCase.Name)' expected '$($moveCase.Expected)' but got '$formattedClassification'."
            }

            $previousRevision = $revision
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
        @{ Path = ".agents/skills/msbuild/SKILL.md"; Infrastructure = $true; Samples = $false },
        @{ Path = ".agents/skills/msbuild/references/incremental-build.md"; Infrastructure = $true; Samples = $false },
        @{ Path = ".agents/skills/coverage-analysis/scripts/Analyze-Coverage.ps1"; Infrastructure = $true; Samples = $false },
        @{ Path = ".copilot/curate/manifest.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = ".copilot/curate/manifest.lock.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = "AGENTS.md"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/workflows/triage.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/scripts/triage.py"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/policies/LabelManagement.yml"; Infrastructure = $true; Samples = $false },
        @{ Path = ".github/PULL_REQUEST_TEMPLATE.md"; Infrastructure = $true; Samples = $false },
        @{ Path = "eng/vendored-files.md"; Infrastructure = $true; Samples = $false },
        @{ Path = ".agents/skills-extra/SKILL.md"; Infrastructure = $false; Samples = $false },
        @{ Path = ".agents/unknown.txt"; Infrastructure = $false; Samples = $false },
        @{ Path = ".copilot/curate-extra/manifest.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = ".copilot/settings.json"; Infrastructure = $false; Samples = $false },
        @{ Path = "AGENTS.md.extra"; Infrastructure = $false; Samples = $false },
        @{ Path = "docs/AGENTS.md"; Infrastructure = $false; Samples = $false },
        @{ Path = "src/AGENTS.md"; Infrastructure = $false; Samples = $false },
        @{ Path = ".github/dependabot.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "Directory.Build.props"; Infrastructure = $false; Samples = $false },
        @{ Path = "Directory.Packages.props"; Infrastructure = $false; Samples = $false },
        @{ Path = "global.json"; Infrastructure = $false; Samples = $false },
        @{ Path = "NuGet.config"; Infrastructure = $false; Samples = $false },
        @{ Path = "azure-pipelines.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "eng/pipelines/steps/build.yml"; Infrastructure = $false; Samples = $false },
        @{ Path = "eng/vendored-files.json"; Infrastructure = $false; Samples = $false },
        @{ Path = "docs/operational.md"; Infrastructure = $false; Samples = $false },
        @{ Path = "src/Product.cs"; Infrastructure = $false; Samples = $false },
        @{ Path = "unknown/new-location.txt"; Infrastructure = $false; Samples = $false },
        @{ Path = "samples/public/Sample.cs"; Infrastructure = $false; Samples = $true },
        @{ Path = "eng/build-samples.ps1"; Infrastructure = $false; Samples = $true },
        @{ Path = "eng/test-samples.ps1"; Infrastructure = $false; Samples = $true },
        @{ Path = "eng/test-samples-output.ps1"; Infrastructure = $false; Samples = $true },
        @{ Path = "eng/samples-tools.ps1"; Infrastructure = $false; Samples = $true }
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
    Assert-Classification "Curated skills update (#12264)" @(
        ".agents/skills/build-parallelism/SKILL.md",
        ".agents/skills/check-bin-obj-clash/SKILL.md",
        ".agents/skills/csharp-expert/SKILL.md",
        ".agents/skills/csharp-refactoring/SKILL.md",
        ".agents/skills/dotnet-breaking-changes/SKILL.md",
        ".agents/skills/dotnet-breaking-changes/references/internals-visible-to.md",
        ".agents/skills/dotnet-breaking-changes/references/multi-targeting.md",
        ".agents/skills/dotnet-breaking-changes/references/public-api.md",
        ".agents/skills/dotnet-breaking-changes/references/source-generation.md",
        ".agents/skills/msbuild/SKILL.md",
        ".agents/skills/msbuild/references/antipatterns/additional-antipatterns.md",
        ".agents/skills/msbuild/references/build-perf-baseline.md",
        ".agents/skills/msbuild/references/extension-points.md",
        ".agents/skills/msbuild/references/incremental-build.md",
        ".agents/skills/msbuild/references/msbuild-antipatterns.md",
        ".copilot/curate/manifest.lock.yml",
        ".copilot/curate/manifest.yml",
        ".github/instructions/copilot-curate.instructions.md",
        "AGENTS.md"
    ) $false $false $true
    Assert-Classification "Curated guidance and product" @(
        ".agents/skills/msbuild/SKILL.md",
        "src/Product.cs"
    ) $true $false $false
    Assert-Classification "Curated guidance and samples" @(
        "AGENTS.md",
        "samples/public/Sample.cs"
    ) $false $true $false
    Assert-Classification "Curated guidance and build inputs" @(
        ".copilot/curate/manifest.yml",
        "Directory.Build.props",
        "Directory.Packages.props",
        "global.json",
        "NuGet.config",
        "azure-pipelines.yml",
        "eng/pipelines/steps/build.yml"
    ) $true $false $false
    Assert-Classification "Curated guidance and unknown input" @(
        ".copilot/curate/manifest.lock.yml",
        "unknown/new-location.txt"
    ) $true $false $false
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
        "eng/build-samples.ps1",
        "eng/test-samples.ps1",
        "eng/samples-tools.ps1"
    ) $false $true $false
    Assert-Classification "Sample output checks only" @(
        "eng/test-samples-output.ps1"
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
