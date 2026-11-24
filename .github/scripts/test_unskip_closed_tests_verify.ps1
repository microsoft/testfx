Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$hookPath = Join-Path $PSScriptRoot '../workflows/unskip-closed-tests-verify.ps1'
. $hookPath -RequestPath 'unused'

$script:Passed = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory)] $Expected,
        [Parameter(Mandatory)] $Actual,
        [Parameter(Mandatory)] [string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', actual '$Actual'."
    }
}

function Assert-Contains {
    param(
        [Parameter(Mandatory)] [object[]] $Values,
        [Parameter(Mandatory)] [object] $Expected,
        [Parameter(Mandatory)] [string] $Message
    )

    if ($Values -notcontains $Expected) {
        throw "$Message Missing '$Expected'."
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)] [scriptblock] $Action,
        [Parameter(Mandatory)] [string] $Pattern
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notmatch $Pattern) {
            throw "Exception '$($_.Exception.Message)' did not match '$Pattern'."
        }
        return
    }

    throw "Expected exception matching '$Pattern'."
}

function Invoke-TestCase {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [scriptblock] $Test
    )

    & $Test
    $script:Passed++
    Write-Host "PASS: $Name"
}

function Write-Request {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $Tests,
        [string] $SourceCommit = ('a' * 40)
    )

    @{
        schema_version = '1'
        repository = 'microsoft/testfx'
        source_commit = $SourceCommit
        candidate = @{ candidate_id = 'candidate-1' }
        tests = $Tests
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Save-Functions {
    param([Parameter(Mandatory)] [string[]] $Names)

    $saved = @{}
    foreach ($name in $Names) {
        $saved[$name] = (Get-Item "Function:$name").ScriptBlock
    }
    return $saved
}

function Restore-Functions {
    param([Parameter(Mandatory)] [hashtable] $Saved)

    foreach ($entry in $Saved.GetEnumerator()) {
        Set-Item "Function:$($entry.Key)" -Value $entry.Value
    }
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "testfx-unskip-hook-$PID"
Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
[void] (New-Item -ItemType Directory -Path $temporaryRoot)

try {
    Invoke-TestCase 'parses exact source and test identities' {
        $requestPath = Join-Path $temporaryRoot 'valid-request.json'
        Write-Request -Path $requestPath -Tests @(
            @{
                fqn = 'Example.Tests.TestOne'
                source_path = 'test/UnitTests/Example/Tests.cs'
                result_file = (Join-Path $temporaryRoot 'TestOne.trx')
            }
        )
        $request = Read-VerificationRequest -Path $requestPath
        Assert-Equal -Expected 'candidate-1' -Actual $request.CandidateId -Message 'Candidate parsing failed.'
        Assert-Equal -Expected ('a' * 40) -Actual $request.SourceCommit -Message 'Commit parsing failed.'
        Assert-Equal -Expected 'Example.Tests.TestOne' -Actual $request.Tests[0].Fqn -Message 'FQN parsing failed.'
    }

    Invoke-TestCase 'rejects malformed and duplicate test identities' {
        $emptyPath = Join-Path $temporaryRoot 'empty-request.json'
        Write-Request -Path $emptyPath -Tests @()
        Assert-Throws -Action { Read-VerificationRequest -Path $emptyPath } -Pattern 'must not be empty'

        $duplicatePath = Join-Path $temporaryRoot 'duplicate-request.json'
        $test = @{
            fqn = 'Example.Tests.TestOne'
            source_path = 'test/UnitTests/Example/Tests.cs'
            result_file = (Join-Path $temporaryRoot 'TestOne.trx')
        }
        Write-Request -Path $duplicatePath -Tests @($test, $test)
        Assert-Throws -Action { Read-VerificationRequest -Path $duplicatePath } -Pattern 'duplicate test identity'

        $fabricatedPath = Join-Path $temporaryRoot 'fabricated-request.json'
        Write-Request -Path $fabricatedPath -Tests @(
            @{
                fqn = 'Invented'
                source_path = 'test/UnitTests/Example/Tests.cs'
                result_file = (Join-Path $temporaryRoot 'Invented.trx')
            }
        )
        Assert-Throws -Action { Read-VerificationRequest -Path $fabricatedPath } -Pattern 'supported test identity'
    }

    Invoke-TestCase 'maps source to the nearest unambiguous project' {
        $source = Join-Path $temporaryRoot 'project-map/test/UnitTests/Example/Tests.cs'
        [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $source) -Force)
        Set-Content -LiteralPath $source -Value 'class Tests {}' -Encoding utf8NoBOM
        $project = Join-Path (Split-Path -Parent $source) 'Example.csproj'
        Set-Content -LiteralPath $project -Value '<Project />' -Encoding utf8NoBOM
        Assert-Equal -Expected $project -Actual (
            Get-TestProject -Root (Join-Path $temporaryRoot 'project-map') -SourcePath 'test/UnitTests/Example/Tests.cs'
        ) -Message 'Nearest project mapping failed.'
    }

    Invoke-TestCase 'rejects ambiguous and unmapped source projects' {
        $ambiguousRoot = Join-Path $temporaryRoot 'ambiguous'
        $source = Join-Path $ambiguousRoot 'test/Example/Tests.cs'
        [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $source) -Force)
        Set-Content -LiteralPath $source -Value 'class Tests {}' -Encoding utf8NoBOM
        Set-Content -LiteralPath (Join-Path (Split-Path -Parent $source) 'One.csproj') -Value '<Project />' -Encoding utf8NoBOM
        Set-Content -LiteralPath (Join-Path (Split-Path -Parent $source) 'Two.csproj') -Value '<Project />' -Encoding utf8NoBOM
        Assert-Throws -Action {
            Get-TestProject -Root $ambiguousRoot -SourcePath 'test/Example/Tests.cs'
        } -Pattern 'ambiguous'

        $unmappedRoot = Join-Path $temporaryRoot 'unmapped'
        $unmappedSource = Join-Path $unmappedRoot 'test/Example/Tests.cs'
        [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $unmappedSource) -Force)
        Set-Content -LiteralPath $unmappedSource -Value 'class Tests {}' -Encoding utf8NoBOM
        Assert-Throws -Action {
            Get-TestProject -Root $unmappedRoot -SourcePath 'test/Example/Tests.cs'
        } -Pattern 'No owning'
    }

    Invoke-TestCase 'selects every portable target framework and rejects mixed unsupported targets' {
        Assert-Equal -Expected 'net8.0,net10.0' -Actual (
            (Select-TargetFrameworks -Frameworks @('net10.0', 'net8.0', 'net8.0')) -join ','
        ) -Message 'Portable framework ordering failed.'
        Assert-Throws -Action {
            Select-TargetFrameworks -Frameworks @('net462', 'net8.0')
        } -Pattern 'Cannot verify every target framework'
        Assert-Throws -Action {
            Select-TargetFrameworks -Frameworks @('net8.0-windows')
        } -Pattern 'Cannot verify every target framework'
    }

    Invoke-TestCase 'uses distinct TRX files for multi-target verification' {
        $requested = Join-Path $temporaryRoot 'results/TestOne.trx'
        Assert-Equal -Expected $requested -Actual (
            Get-TargetResultFile -RequestedResultFile $requested -TargetFramework 'net8.0' -TargetFrameworkCount 1
        ) -Message 'Single-target result path changed.'
        Assert-Equal -Expected (Join-Path $temporaryRoot 'results/TestOne--net9.0.trx') -Actual (
            Get-TargetResultFile -RequestedResultFile $requested -TargetFramework 'net9.0' -TargetFrameworkCount 2
        ) -Message 'Multi-target result path was not framework-specific.'
    }

    Invoke-TestCase 'builds exact-FQN and requested-TRX commands' {
        $project = Join-Path $temporaryRoot 'Example.csproj'
        $result = Join-Path $temporaryRoot 'results/TestOne.trx'
        $buildArguments = Get-BuildArguments -Project $project -TargetFramework 'net8.0'
        Assert-Contains -Values $buildArguments -Expected '-p:EnableCodeCoverage=False' -Message 'Build coverage opt-out failed.'
        Assert-Contains -Values $buildArguments -Expected '-bl:{}' -Message 'Build binlog argument missing.'

        $testArguments = Get-TestArguments -Project $project -TargetFramework 'net8.0' -Fqn 'Example.Tests.TestOne' -ResultFile $result
        Assert-Contains -Values $testArguments -Expected '--filter-uid' -Message 'Test UID filter missing.'
        Assert-Contains -Values $testArguments -Expected 'Example.Tests.TestOne' -Message 'Exact FQN missing.'
        Assert-Contains -Values $testArguments -Expected ([System.IO.Path]::GetFileName($result)) -Message 'Requested TRX filename missing.'
        Assert-Contains -Values $testArguments -Expected ([System.IO.Path]::GetDirectoryName($result)) -Message 'Requested TRX directory missing.'
        Assert-Contains -Values $testArguments -Expected '-bl:{}' -Message 'Test binlog argument missing.'
    }

    Invoke-TestCase 'rejects stale source revisions before execution' {
        $repository = Join-Path $temporaryRoot 'stale-repository'
        [void] (New-Item -ItemType Directory -Path $repository)
        & git -C $repository init --quiet
        & git -C $repository config user.email tests@example.invalid
        & git -C $repository config user.name Tests
        Set-Content -LiteralPath (Join-Path $repository 'README.md') -Value 'fixture' -Encoding utf8NoBOM
        & git -C $repository add .
        & git -C $repository commit --quiet -m fixture
        Assert-Throws -Action {
            Assert-Revision -Root $repository -ExpectedCommit ('0' * 40)
        } -Pattern 'Repository revision changed'
    }

    Invoke-TestCase 'allows only runner temp and dedicated Git metadata results' {
        $repository = Join-Path $temporaryRoot 'result-roots'
        $runnerTemp = Join-Path $temporaryRoot 'runner-temp'
        [void] (New-Item -ItemType Directory -Path $repository)
        [void] (New-Item -ItemType Directory -Path $runnerTemp)
        & git -C $repository init --quiet

        $previousRunnerTemp = $env:RUNNER_TEMP
        $env:RUNNER_TEMP = $runnerTemp
        try {
            $runnerResult = Join-Path $runnerTemp 'runner.trx'
            Assert-Equal -Expected ([System.IO.Path]::GetFullPath($runnerResult)) -Actual (
                Resolve-ResultPath -Root $repository -Value $runnerResult
            ) -Message 'Runner temp result path was rejected.'

            $gitDirectory = & git -C $repository rev-parse --git-dir
            $metadataResult = Join-Path $repository "$gitDirectory/unskip-closed-tests/digest/candidate/result.trx"
            Assert-Equal -Expected ([System.IO.Path]::GetFullPath($metadataResult)) -Actual (
                Resolve-ResultPath -Root $repository -Value $metadataResult
            ) -Message 'Dedicated Git metadata result path was rejected.'

            $outsideResult = Join-Path $temporaryRoot 'outside/result.trx'
            Assert-Throws -Action {
                Resolve-ResultPath -Root $repository -Value $outsideResult
            } -Pattern 'outside the trusted output roots'
        }
        finally {
            $env:RUNNER_TEMP = $previousRunnerTemp
        }
    }

    Invoke-TestCase 'fails closed when the requested TRX is missing' {
        $root = Join-Path $temporaryRoot 'missing-trx'
        $source = Join-Path $root 'test/Example/Tests.cs'
        [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $source) -Force)
        Set-Content -LiteralPath $source -Value 'class Tests {}' -Encoding utf8NoBOM
        $project = Join-Path (Split-Path -Parent $source) 'Example.csproj'
        Set-Content -LiteralPath $project -Value '<Project />' -Encoding utf8NoBOM
        $requestPath = Join-Path $root 'request.json'
        $resultPath = Join-Path $root 'results/missing.trx'
        Write-Request -Path $requestPath -Tests @(
            @{
                fqn = 'Example.Tests.TestOne'
                source_path = 'test/Example/Tests.cs'
                result_file = $resultPath
            }
        )

        $saved = Save-Functions -Names @(
            'Assert-Revision',
            'Initialize-RepositoryBuild',
            'Get-ProjectTargetFrameworks',
            'Get-DotNetPath',
            'Invoke-CheckedProcess'
        )
        try {
            Set-Item Function:Assert-Revision -Value { param($Root, $ExpectedCommit) }
            Set-Item Function:Initialize-RepositoryBuild -Value { param($Root, $SourceCommit, $RequiresPack, $Timeout) }
            Set-Item Function:Get-ProjectTargetFrameworks -Value { param($Root, $Project, $Timeout) @('net8.0') }
            Set-Item Function:Get-DotNetPath -Value { param($Root) 'dotnet' }
            Set-Item Function:Invoke-CheckedProcess -Value { param($FileName, $Arguments, $WorkingDirectory, $Timeout) }
            $previousRunnerTemp = $env:RUNNER_TEMP
            $env:RUNNER_TEMP = $root
            try {
                Assert-Throws -Action {
                    Invoke-UnskipVerification -Path $requestPath -Root $root -Timeout 30
                } -Pattern 'did not create requested TRX'
            }
            finally {
                $env:RUNNER_TEMP = $previousRunnerTemp
            }
        }
        finally {
            Restore-Functions -Saved $saved
        }
    }

    Invoke-TestCase 'runs repository pack only once for acceptance projects' {
        $root = Join-Path $temporaryRoot 'acceptance'
        [void] (New-Item -ItemType Directory -Path $root)
        $buildScript = Join-Path $root $(if ($IsWindows) { 'build.cmd' } else { 'build.sh' })
        Set-Content -LiteralPath $buildScript -Value '' -Encoding utf8NoBOM
        $runnerTemp = Join-Path $root 'runner-temp'
        $calls = [System.Collections.Generic.List[object]]::new()
        $saved = Save-Functions -Names @('Invoke-CheckedProcess', 'Assert-Revision')
        try {
            Set-Item Function:Invoke-CheckedProcess -Value {
                param($FileName, $Arguments, $WorkingDirectory, $Timeout)
                $calls.Add([pscustomobject]@{ FileName = $FileName; Arguments = @($Arguments) })
            }
            Set-Item Function:Assert-Revision -Value { param($Root, $ExpectedCommit) }
            $previousRunnerTemp = $env:RUNNER_TEMP
            $env:RUNNER_TEMP = $runnerTemp
            try {
                Initialize-RepositoryBuild -Root $root -SourceCommit ('a' * 40) -RequiresPack $true -Timeout 30
                Initialize-RepositoryBuild -Root $root -SourceCommit ('a' * 40) -RequiresPack $true -Timeout 30
            }
            finally {
                $env:RUNNER_TEMP = $previousRunnerTemp
            }
        }
        finally {
            Restore-Functions -Saved $saved
        }
        Assert-Equal -Expected 1 -Actual $calls.Count -Message 'Repository pack should run once.'
        Assert-Contains -Values $calls[0].Arguments -Expected '-pack' -Message 'Acceptance pack argument missing.'
    }

    Invoke-TestCase 'replaces the packaged fail-closed placeholder' {
        $hookText = Get-Content -LiteralPath $hookPath -Raw
        if ($hookText.Contains('The repository must replace verification.command')) {
            throw 'The TestFX hook still contains the package placeholder.'
        }
        $config = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../workflows/unskip-closed-tests.config.json') -Raw |
            ConvertFrom-Json -Depth 16
        Assert-Equal -Expected 'pwsh' -Actual $config.verification.command[0] -Message 'PowerShell command is not configured.'
        Assert-Contains -Values @($config.verification.command) -Expected '.github/workflows/unskip-closed-tests-verify.ps1' -Message 'PowerShell hook path is not configured.'
    }

    Assert-Equal -Expected 12 -Actual $script:Passed -Message 'Unexpected hook test count.'
    Write-Host "All $script:Passed unskip closed tests PowerShell hook tests passed."
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
