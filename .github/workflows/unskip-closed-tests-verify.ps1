param(
    [Parameter(Position = 0, Mandatory)]
    [string] $RequestPath,

    [string] $RepositoryRoot = (Get-Location).Path,

    [ValidateRange(1, 86400)]
    [int] $TimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:MaxRequestBytes = 4 * 1024 * 1024
$script:FqnPattern = '^(?:@?[A-Za-z_][A-Za-z0-9_]*\.)+@?[A-Za-z_][A-Za-z0-9_]*$'
$script:TfmPattern = '^net(?<version>[0-9]+)(?:\.[0-9]+)?(?:-[A-Za-z0-9.-]+)?$'

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Context
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Context.$Name is required."
    }

    return $property.Value
}

function Get-RequiredString {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Context
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Context $Context
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw "$Context.$Name must be a non-empty string."
    }

    return $value
}

function Get-RequiredArray {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Context
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Context $Context
    if ($value -is [string]) {
        throw "$Context.$Name must be an array."
    }

    return @($value)
}

function ConvertTo-RepositoryRelativePath {
    param(
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [string] $Context
    )

    if (
        [System.IO.Path]::IsPathRooted($Value) -or
        $Value.Contains('\') -or
        $Value.StartsWith('./', [StringComparison]::Ordinal) -or
        ($Value -split '/') -contains '..'
    ) {
        throw "$Context must be a normalized repository-relative path using '/' separators."
    }

    return $Value
}

function Read-VerificationRequest {
    param([Parameter(Mandatory)] [string] $Path)

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.Length -gt $script:MaxRequestBytes) {
        throw "Verification request exceeds the $($script:MaxRequestBytes)-byte limit."
    }

    try {
        $request = Get-Content -LiteralPath $item.FullName -Raw -Encoding utf8 |
            ConvertFrom-Json -Depth 64
    }
    catch {
        throw "Cannot read verification request: $($_.Exception.Message)"
    }

    if ((Get-RequiredString -InputObject $request -Name 'schema_version' -Context 'verification request') -ne '1') {
        throw "verification request.schema_version must be '1'."
    }

    $candidate = Get-RequiredProperty -InputObject $request -Name 'candidate' -Context 'verification request'
    $candidateId = Get-RequiredString -InputObject $candidate -Name 'candidate_id' -Context 'verification request.candidate'
    $repository = Get-RequiredString -InputObject $request -Name 'repository' -Context 'verification request'
    if ($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        throw 'verification request.repository must use owner/repository form.'
    }

    $sourceCommit = (
        Get-RequiredString -InputObject $request -Name 'source_commit' -Context 'verification request'
    ).ToLowerInvariant()
    if ($sourceCommit -notmatch '^[0-9a-f]{40,64}$') {
        throw 'verification request.source_commit must be a full hexadecimal object id.'
    }

    $tests = @()
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $index = 0
    foreach ($test in Get-RequiredArray -InputObject $request -Name 'tests' -Context 'verification request') {
        $context = "verification request.tests[$index]"
        $fqn = Get-RequiredString -InputObject $test -Name 'fqn' -Context $context
        if ($fqn -notmatch $script:FqnPattern) {
            throw "$context.fqn is not a supported test identity."
        }
        if (-not $seen.Add($fqn)) {
            throw "verification request contains duplicate test identity '$fqn'."
        }

        $sourcePath = ConvertTo-RepositoryRelativePath -Value (
            Get-RequiredString -InputObject $test -Name 'source_path' -Context $context
        ) -Context "$context.source_path"
        if (-not $sourcePath.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) {
            throw "$context.source_path must identify C# source."
        }

        $resultFile = Get-RequiredString -InputObject $test -Name 'result_file' -Context $context
        $tests += [pscustomobject]@{
            Fqn = $fqn
            SourcePath = $sourcePath
            ResultFile = $resultFile
        }
        $index++
    }

    if ($tests.Count -eq 0) {
        throw 'verification request.tests must not be empty.'
    }

    return [pscustomobject]@{
        CandidateId = $candidateId
        Repository = $repository
        SourceCommit = $sourceCommit
        Tests = $tests
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    $output = & git -C $Root @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Assert-Revision {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $ExpectedCommit
    )

    $head = (Invoke-Git -Root $Root -Arguments @('rev-parse', 'HEAD')).ToLowerInvariant()
    if ($head -ne $ExpectedCommit) {
        throw "Repository revision changed from $ExpectedCommit to $head."
    }
}

function Get-TestProject {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $SourcePath
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $relative = $SourcePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $source = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $relative))
    $prefix = $resolvedRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    ) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $source.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Source file escapes the repository: $SourcePath"
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Source file does not exist: $SourcePath"
    }

    $directory = Split-Path -Parent $source
    while ($true) {
        $projects = @(Get-ChildItem -LiteralPath $directory -Filter '*.csproj' -File)
        if ($projects.Count -eq 1) {
            return $projects[0].FullName
        }
        if ($projects.Count -gt 1) {
            throw "Source project is ambiguous for ${SourcePath}: $($projects.Name -join ', ')"
        }
        if ([string]::Equals($directory, $resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parent = Split-Path -Parent $directory
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $directory) {
            break
        }
        $directory = $parent
    }

    throw "No owning test project found for $SourcePath."
}

function Get-DotNetPath {
    param([Parameter(Mandatory)] [string] $Root)

    $executable = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $local = Join-Path (Join-Path $Root '.dotnet') $executable
    if (Test-Path -LiteralPath $local -PathType Leaf) {
        return $local
    }

    return 'dotnet'
}

function New-ProcessStartInfo {
    param(
        [Parameter(Mandatory)] [string] $FileName,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [switch] $CaptureOutput
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $CaptureOutput
    $startInfo.RedirectStandardError = $CaptureOutput
    $startInfo.Environment['DOTNET_ROLL_FORWARD'] = if ($env:DOTNET_ROLL_FORWARD) {
        $env:DOTNET_ROLL_FORWARD
    } else {
        'Major'
    }
    $startInfo.Environment['DOTNET_ROLL_FORWARD_TO_PRERELEASE'] = if ($env:DOTNET_ROLL_FORWARD_TO_PRERELEASE) {
        $env:DOTNET_ROLL_FORWARD_TO_PRERELEASE
    } else {
        '1'
    }
    foreach ($argument in $Arguments) {
        [void] $startInfo.ArgumentList.Add($argument)
    }

    return $startInfo
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory)] [string] $FileName,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [Parameter(Mandatory)] [int] $Timeout
    )

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = New-ProcessStartInfo -FileName $FileName -Arguments $Arguments -WorkingDirectory $WorkingDirectory
    try {
        if (-not $process.Start()) {
            throw "Could not start command '$FileName'."
        }
        if (-not $process.WaitForExit($Timeout * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Command timed out after $Timeout seconds: $FileName $($Arguments -join ' ')"
        }
        if ($process.ExitCode -ne 0) {
            throw "Command exited with $($process.ExitCode): $FileName $($Arguments -join ' ')"
        }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-CapturedProcess {
    param(
        [Parameter(Mandatory)] [string] $FileName,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [Parameter(Mandatory)] [int] $Timeout
    )

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = New-ProcessStartInfo -FileName $FileName -Arguments $Arguments -WorkingDirectory $WorkingDirectory -CaptureOutput
    try {
        if (-not $process.Start()) {
            throw "Could not start command '$FileName'."
        }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($Timeout * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Command timed out after $Timeout seconds: $FileName $($Arguments -join ' ')"
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "Command exited with $($process.ExitCode): $FileName $($Arguments -join ' '): $errorOutput"
        }

        return $output
    }
    finally {
        $process.Dispose()
    }
}

function Select-TargetFramework {
    param([Parameter(Mandatory)] [string[]] $Frameworks)

    if ($Frameworks -contains 'net8.0') {
        return 'net8.0'
    }

    $candidates = foreach ($framework in $Frameworks) {
        $match = [regex]::Match($framework, $script:TfmPattern)
        if ($match.Success -and $framework.Contains('.') -and -not $framework.Contains('-')) {
            [pscustomobject]@{
                Version = [int] $match.Groups['version'].Value
                Framework = $framework
            }
        }
    }
    $selected = $candidates | Sort-Object Version, Framework | Select-Object -First 1
    if ($null -eq $selected) {
        throw 'No portable .NET target framework is available for verification.'
    }

    return $selected.Framework
}

function Get-ProjectTargetFramework {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [int] $Timeout
    )

    $dotnet = Get-DotNetPath -Root $Root
    $output = Invoke-CapturedProcess -FileName $dotnet -Arguments @(
        'msbuild',
        $Project,
        '-nologo',
        '-getProperty:TargetFrameworks',
        '-getProperty:TargetFramework',
        '-getProperty:OutputType'
    ) -WorkingDirectory $Root -Timeout $Timeout

    $jsonStart = $output.IndexOf('{')
    if ($jsonStart -lt 0) {
        throw 'Cannot parse evaluated test project properties.'
    }
    try {
        $properties = ($output.Substring($jsonStart) | ConvertFrom-Json -Depth 16).Properties
    }
    catch {
        throw "Cannot parse evaluated test project properties: $($_.Exception.Message)"
    }
    if (-not [string]::Equals([string] $properties.OutputType, 'Exe', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Project is not an executable test project."
    }

    $frameworkText = if ([string]::IsNullOrWhiteSpace([string] $properties.TargetFrameworks)) {
        [string] $properties.TargetFramework
    } else {
        [string] $properties.TargetFrameworks
    }
    return Select-TargetFramework -Frameworks @($frameworkText -split ';' | Where-Object { $_ })
}

function Test-RequiresPackedPackages {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Project
    )

    $relative = [System.IO.Path]::GetRelativePath($Root, $Project)
    return @($relative -split '[\\/]' | Where-Object {
        $_.EndsWith('.Acceptance.IntegrationTests', [StringComparison]::Ordinal)
    }).Count -gt 0
}

function Initialize-RepositoryBuild {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $SourceCommit,
        [Parameter(Mandatory)] [bool] $RequiresPack,
        [Parameter(Mandatory)] [int] $Timeout
    )

    $temporaryRoot = if ($env:RUNNER_TEMP) {
        $env:RUNNER_TEMP
    } else {
        Join-Path $Root 'artifacts/tmp'
    }
    $markerDirectory = Join-Path $temporaryRoot 'testfx-unskip'
    $kind = if ($RequiresPack) { 'packed' } else { 'built' }
    $marker = Join-Path $markerDirectory "$kind-$SourceCommit"
    if (Test-Path -LiteralPath $marker -PathType Leaf) {
        return
    }

    if ($IsWindows) {
        $buildScript = Join-Path $Root 'build.cmd'
        $fileName = if ($env:COMSPEC) { $env:COMSPEC } else { 'cmd.exe' }
        $arguments = @('/d', '/c', $buildScript)
    } else {
        $buildScript = Join-Path $Root 'build.sh'
        $fileName = $buildScript
        $arguments = @()
    }
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw "Repository build script is missing: $buildScript"
    }
    if ($RequiresPack) {
        $arguments += '-pack'
    }

    Invoke-CheckedProcess -FileName $fileName -Arguments $arguments -WorkingDirectory $Root -Timeout $Timeout
    Assert-Revision -Root $Root -ExpectedCommit $SourceCommit
    [void] (New-Item -ItemType Directory -Path $markerDirectory -Force)
    Set-Content -LiteralPath $marker -Value $SourceCommit -Encoding utf8NoBOM
    if ($RequiresPack) {
        Set-Content -LiteralPath (Join-Path $markerDirectory "built-$SourceCommit") -Value $SourceCommit -Encoding utf8NoBOM
    }
}

function Resolve-ResultPath {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Value
    )

    $path = if ([System.IO.Path]::IsPathRooted($Value)) {
        [System.IO.Path]::GetFullPath($Value)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $Root $Value))
    }
    $trustedRoot = [System.IO.Path]::GetFullPath(
        $(if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $Root })
    )
    $prefix = $trustedRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    ) + [System.IO.Path]::DirectorySeparatorChar
    if (
        -not [string]::Equals($path, $trustedRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    ) {
        throw "Requested result file is outside the trusted output root: $Value"
    }
    if (-not $path.EndsWith('.trx', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Requested result file must use .trx: $Value"
    }

    return $path
}

function Get-BuildArguments {
    param(
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [string] $TargetFramework
    )

    return @(
        'build',
        $Project,
        '-c',
        'Debug',
        '-f',
        $TargetFramework,
        '--no-restore',
        '-p:EnableCodeCoverage=False',
        '-bl:{}'
    )
}

function Get-TestArguments {
    param(
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [string] $TargetFramework,
        [Parameter(Mandatory)] [string] $Fqn,
        [Parameter(Mandatory)] [string] $ResultFile
    )

    return @(
        'run',
        '--project',
        $Project,
        '-c',
        'Debug',
        '-f',
        $TargetFramework,
        '--no-build',
        '--no-restore',
        '-p:EnableCodeCoverage=False',
        '-bl:{}',
        '--',
        '--filter-uid',
        $Fqn,
        '--report-trx',
        '--report-trx-filename',
        [System.IO.Path]::GetFileName($ResultFile),
        '--results-directory',
        [System.IO.Path]::GetDirectoryName($ResultFile)
    )
}

function Invoke-UnskipVerification {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [int] $Timeout
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $request = Read-VerificationRequest -Path $Path
    Assert-Revision -Root $resolvedRoot -ExpectedCommit $request.SourceCommit

    $projects = @($request.Tests | ForEach-Object {
        Get-TestProject -Root $resolvedRoot -SourcePath $_.SourcePath
    } | Sort-Object -Unique)
    if ($projects.Count -ne 1) {
        throw 'A single verification request must map to exactly one test project.'
    }
    $project = $projects[0]
    $requiresPack = Test-RequiresPackedPackages -Root $resolvedRoot -Project $project
    Initialize-RepositoryBuild -Root $resolvedRoot -SourceCommit $request.SourceCommit -RequiresPack $requiresPack -Timeout $Timeout
    $targetFramework = Get-ProjectTargetFramework -Root $resolvedRoot -Project $project -Timeout $Timeout

    $dotnet = Get-DotNetPath -Root $resolvedRoot
    Invoke-CheckedProcess -FileName $dotnet -Arguments (
        Get-BuildArguments -Project $project -TargetFramework $targetFramework
    ) -WorkingDirectory $resolvedRoot -Timeout $Timeout
    Assert-Revision -Root $resolvedRoot -ExpectedCommit $request.SourceCommit

    foreach ($test in $request.Tests) {
        $resultFile = Resolve-ResultPath -Root $resolvedRoot -Value $test.ResultFile
        [void] (New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($resultFile)) -Force)
        Remove-Item -LiteralPath $resultFile -Force -ErrorAction SilentlyContinue
        Invoke-CheckedProcess -FileName $dotnet -Arguments (
            Get-TestArguments -Project $project -TargetFramework $targetFramework -Fqn $test.Fqn -ResultFile $resultFile
        ) -WorkingDirectory $resolvedRoot -Timeout $Timeout
        if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
            throw "Test runner did not create requested TRX: $resultFile"
        }
        Assert-Revision -Root $resolvedRoot -ExpectedCommit $request.SourceCommit
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        Invoke-UnskipVerification -Path $RequestPath -Root $RepositoryRoot -Timeout $TimeoutSeconds
        exit 0
    }
    catch {
        [Console]::Error.WriteLine("error: $($_.Exception.Message)")
        exit 1
    }
}
