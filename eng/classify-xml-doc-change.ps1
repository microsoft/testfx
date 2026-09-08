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

$roslynAssemblies = @(
    (Join-Path $PSHOME "Microsoft.CodeAnalysis.dll"),
    (Join-Path $PSHOME "Microsoft.CodeAnalysis.CSharp.dll")
)
foreach ($assembly in $roslynAssemblies) {
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
        throw "The PowerShell Roslyn assembly '$assembly' is unavailable."
    }
}

Add-Type -Path $roslynAssemblies

function Get-CSharpSyntaxKind {
    param([Microsoft.CodeAnalysis.SyntaxTrivia]$Trivia)

    return [Microsoft.CodeAnalysis.CSharp.SyntaxKind]$Trivia.RawKind
}

function Get-ComparedTrivia {
    param(
        [Microsoft.CodeAnalysis.SyntaxNode]$Root,
        [switch]$Documentation
    )

    $descendIntoChildren = [Func[Microsoft.CodeAnalysis.SyntaxNode, bool]] { $true }
    $result = [System.Collections.Generic.List[string]]::new()

    $tokenIndex = 0
    foreach ($token in $Root.DescendantTokens($descendIntoChildren, $false)) {
        foreach ($side in @("Leading", "Trailing")) {
            $triviaList = if ($side -eq "Leading") { $token.LeadingTrivia } else { $token.TrailingTrivia }
            foreach ($trivia in $triviaList) {
                $kind = Get-CSharpSyntaxKind $trivia
                $isDocumentation = $kind -in @(
                    [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::SingleLineDocumentationCommentTrivia,
                    [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::MultiLineDocumentationCommentTrivia
                )

                if ($Documentation -ne $isDocumentation) {
                    continue
                }

                if (-not $Documentation -and $kind -in @(
                    [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::WhitespaceTrivia,
                    [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::EndOfLineTrivia
                )) {
                    continue
                }

                $text = $trivia.ToFullString()
                $result.Add("${tokenIndex}:${side}:$($trivia.RawKind):$($text.Length):$text")
            }
        }

        $tokenIndex++
    }

    return $result
}

function Test-SequenceEqual {
    param(
        [string[]]$Left,
        [string[]]$Right
    )

    if ($Left.Count -ne $Right.Count) {
        return $false
    }

    for ($index = 0; $index -lt $Left.Count; $index++) {
        if ($Left[$index] -cne $Right[$index]) {
            return $false
        }
    }

    return $true
}

function Test-XmlDocOnlyTextChange {
    param(
        [string]$OldText,
        [string]$NewText
    )

    if ($OldText -ceq $NewText) {
        return $false
    }

    $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithDocumentationMode(
        [Microsoft.CodeAnalysis.DocumentationMode]::Diagnose)
    $oldTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($OldText, $parseOptions)
    $newTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($NewText, $parseOptions)

    $cancellationToken = [System.Threading.CancellationToken]::None
    $hasParseErrors =
        @($oldTree.GetDiagnostics($cancellationToken) | Where-Object Severity -eq Error).Count -ne 0 -or
        @($newTree.GetDiagnostics($cancellationToken) | Where-Object Severity -eq Error).Count -ne 0
    if ($hasParseErrors) {
        return $false
    }

    $oldRoot = $oldTree.GetRoot()
    $newRoot = $newTree.GetRoot()
    if (-not [Microsoft.CodeAnalysis.CSharp.SyntaxFactory]::AreEquivalent($oldRoot, $newRoot)) {
        return $false
    }

    $oldNonDocumentationTrivia = @(Get-ComparedTrivia $oldRoot)
    $newNonDocumentationTrivia = @(Get-ComparedTrivia $newRoot)
    if (-not (Test-SequenceEqual $oldNonDocumentationTrivia $newNonDocumentationTrivia)) {
        return $false
    }

    $oldDocumentation = @(Get-ComparedTrivia $oldRoot -Documentation)
    $newDocumentation = @(Get-ComparedTrivia $newRoot -Documentation)
    if (@($oldDocumentation + $newDocumentation | Where-Object { $_ -cmatch '<\s*include\b' }).Count -ne 0) {
        return $false
    }

    return -not (Test-SequenceEqual $oldDocumentation $newDocumentation)
}

function Test-EligiblePath {
    param([string]$Path)

    return (
        [System.IO.Path]::GetExtension($Path) -ceq ".cs" -and
        -not $Path.StartsWith("samples/", [System.StringComparison]::Ordinal))
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

function Get-GitFileText {
    param(
        [string]$Repository,
        [string]$Revision,
        [string]$Path
    )

    return (Invoke-Git $Repository @("show", "--no-textconv", "${Revision}:$Path")) -join "`n"
}

function Test-GitDiff {
    param(
        [string]$BaseRevision,
        [string]$HeadRevision,
        [string]$Repository
    )

    if ([string]::IsNullOrEmpty($Repository)) {
        $repositoryOutput = @(Invoke-Git $PSScriptRoot @("rev-parse", "--show-toplevel"))
        $Repository = $repositoryOutput[0]
    }

    $changes = @(Invoke-Git $Repository @(
        "diff",
        "--name-status",
        "--no-renames",
        "--diff-filter=ACDMRTUXB",
        $BaseRevision,
        $HeadRevision,
        "--"
    ))

    if ($changes.Count -eq 0) {
        return $false
    }

    foreach ($change in $changes) {
        $status, $path = $change -split "`t", 2
        if ($status -ne "M" -or -not (Test-EligiblePath $path)) {
            return $false
        }

        $oldText = Get-GitFileText $Repository $BaseRevision $path
        $newText = Get-GitFileText $Repository $HeadRevision $path
        if (-not (Test-XmlDocOnlyTextChange $oldText $newText)) {
            return $false
        }
    }

    return $true
}

function Invoke-GitDiffSelfTest {
    $repository = Join-Path ([System.IO.Path]::GetTempPath()) "testfx-xml-doc-classifier-$([System.Guid]::NewGuid().ToString('N'))"
    [System.IO.Directory]::CreateDirectory($repository) | Out-Null

    try {
        $null = Invoke-Git $repository @("init", "--quiet")
        $null = Invoke-Git $repository @("config", "user.name", "XML documentation classifier")
        $null = Invoke-Git $repository @("config", "user.email", "classifier@example.invalid")

        $firstPath = Join-Path $repository "First.cs"
        $secondPath = Join-Path $repository "Second.cs"
        [System.IO.File]::WriteAllText($firstPath, "/// old first`nclass First { }`n")
        [System.IO.File]::WriteAllText($secondPath, "/// old second`nclass Second { int Value => 1; }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Base")
        $baseRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]

        [System.IO.File]::WriteAllText($firstPath, "/// new first`nclass First { }`n")
        [System.IO.File]::WriteAllText($secondPath, "/// new second`nclass Second { int Value => 1; }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Documentation")
        $documentationRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        if (-not (Test-GitDiff $baseRevision $documentationRevision $repository)) {
            throw "Git self-test expected a two-file documentation-only diff to classify as true."
        }

        [System.IO.File]::WriteAllText($firstPath, "/// newest first`nclass First { }`n")
        [System.IO.File]::WriteAllText($secondPath, "/// new second`nclass Second { int Value => 2; }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Mixed")
        $mixedRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        if (Test-GitDiff $documentationRevision $mixedRevision $repository) {
            throw "Git self-test expected a mixed documentation and code diff to classify as false."
        }

        $addedPath = Join-Path $repository "Added.cs"
        [System.IO.File]::WriteAllText($addedPath, "/// added`nclass Added { }`n")
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Added")
        $addedRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        if (Test-GitDiff $mixedRevision $addedRevision $repository) {
            throw "Git self-test expected an added file to classify as false."
        }

        [System.IO.File]::Delete($addedPath)
        $null = Invoke-Git $repository @("add", "--all")
        $null = Invoke-Git $repository @("commit", "--quiet", "-m", "Deleted")
        $deletedRevision = @(Invoke-Git $repository @("rev-parse", "HEAD"))[0]
        if (Test-GitDiff $addedRevision $deletedRevision $repository) {
            throw "Git self-test expected a deleted file to classify as false."
        }
    }
    finally {
        Remove-Item -LiteralPath $repository -Recurse -Force
    }
}

function Invoke-SelfTest {
    $cases = @(
        @{
            Name = "XML documentation content"
            Expected = $true
            Old = "class C {`n    /// old`n    void M() { }`n}"
            New = "class C {`n    /// new`n    void M() { }`n}"
        },
        @{
            Name = "Added XML documentation line"
            Expected = $true
            Old = "class C {`n    /// first`n    void M() { }`n}"
            New = "class C {`n    /// first`n    /// second`n    void M() { }`n}"
        },
        @{
            Name = "Removed XML documentation"
            Expected = $true
            Old = "class C {`n    /// removed`n    void M() { }`n}"
            New = "class C {`n    void M() { }`n}"
        },
        @{
            Name = "Multiline XML documentation"
            Expected = $true
            Old = "class C {`n    /** old */`n    void M() { }`n}"
            New = "class C {`n    /** new */`n    void M() { }`n}"
        },
        @{
            Name = "XML documentation include"
            Expected = $false
            Old = "class C {`n    /// <include file='docs.xml' path='old'/>`n    void M() { }`n}"
            New = "class C {`n    /// <include file='Docs.xml' path='new'/>`n    void M() { }`n}"
        },
        @{
            Name = "Ordinary comment"
            Expected = $false
            Old = "class C {`n    // old`n    void M() { }`n}"
            New = "class C {`n    // new`n    void M() { }`n}"
        },
        @{
            Name = "Executable code"
            Expected = $false
            Old = "class C { int P => 1; }"
            New = "class C { int P => 2; }"
        },
        @{
            Name = "XML-looking raw string content"
            Expected = $false
            Old = "class C { string P => """"""`n/// old`n""""""; }"
            New = "class C { string P => """"""`n/// new`n""""""; }"
        },
        @{
            Name = "Mixed XML and ordinary comments"
            Expected = $false
            Old = "class C {`n    /// old`n    // old`n    void M() { }`n}"
            New = "class C {`n    /// new`n    // new`n    void M() { }`n}"
        },
        @{
            Name = "Whitespace only"
            Expected = $false
            Old = "class C { void M() { } }"
            New = "class C {  void M() { } }"
        },
        @{
            Name = "Identical files"
            Expected = $false
            Old = "class C { }"
            New = "class C { }"
        },
        @{
            Name = "Mixed XML documentation and preprocessor directive"
            Expected = $false
            Old = "#if DEBUG`n/// old`nclass C { }`n#endif"
            New = "#if RELEASE`n/// new`nclass C { }`n#endif"
        },
        @{
            Name = "Relocated preprocessor directive"
            Expected = $false
            Old = "#nullable disable`n/// old`nclass C { }`nclass D { }"
            New = "/// new`nclass C { }`n#nullable disable`nclass D { }"
        }
    )

    foreach ($case in $cases) {
        $actual = Test-XmlDocOnlyTextChange $case.Old $case.New
        if ($actual -ne $case.Expected) {
            throw "Self-test '$($case.Name)' expected '$($case.Expected)' but got '$actual'."
        }
    }

    foreach ($pathCase in @(
        @{ Path = "src/Product.cs"; Expected = $true },
        @{ Path = "samples/public/Sample.cs"; Expected = $false },
        @{ Path = "samples/internal/Sample.cs"; Expected = $false },
        @{ Path = "src/Product.vb"; Expected = $false }
    )) {
        $actual = Test-EligiblePath $pathCase.Path
        if ($actual -ne $pathCase.Expected) {
            throw "Path self-test '$($pathCase.Path)' expected '$($pathCase.Expected)' but got '$actual'."
        }
    }

    Invoke-GitDiffSelfTest

    Write-Output "XML documentation change classifier self-tests passed."
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

try {
    Write-Output ((Test-GitDiff $Base $Head $Repository).ToString().ToLowerInvariant())
}
catch {
    [Console]::Error.WriteLine("##vso[task.logissue type=warning]XML documentation change classification failed; running full validation. $($_.Exception.Message)")
    Write-Output "false"
}
