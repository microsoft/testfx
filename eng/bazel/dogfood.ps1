# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Cold', 'BuildCache', 'Cached', 'MSBuild')]
    [string] $Mode,

    [Parameter(Mandatory)]
    [ValidateScript({ [IO.Path]::IsPathFullyQualified($_) })]
    [string] $RunRoot,

    [Parameter(Mandatory)]
    [ValidateScript({ [IO.Path]::IsPathFullyQualified($_) })]
    [string] $CacheRoot,

    [ValidateRange(1, 64)]
    [int] $Jobs = 8,

    [ValidateRange(1, 16)]
    [int] $TestJobs = 2,

    [string[]] $Targets = @(),

    [switch] $Prepare
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {
    throw 'The initial dogfood contract requires Windows x64.'
}
if ($TestJobs -gt $Jobs) { throw 'TestJobs cannot exceed Jobs.' }

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
if ($RunRoot.TrimEnd('\') -eq [IO.Path]::GetPathRoot($RunRoot).TrimEnd('\') -or
    $CacheRoot.TrimEnd('\') -eq [IO.Path]::GetPathRoot($CacheRoot).TrimEnd('\') -or
    ($RunRoot.TrimEnd('\') + '\').StartsWith($CacheRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
    ($CacheRoot.TrimEnd('\') + '\').StartsWith($RunRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunRoot and CacheRoot must be separate, dedicated directories, not drive roots.'
}
$workloadPath = Join-Path $PSScriptRoot 'dogfood-workload.json'
$workload = Get-Content -LiteralPath $workloadPath -Raw | ConvertFrom-Json
$globalJson = Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json
$engineering = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'engineering-sdk.bzl') -Raw
$sdkVersion = [regex]::Match($engineering, '(?m)^_SDK_VERSION = "([^"]+)"\r?$').Groups[1].Value
$sdkRuntime = [regex]::Match($engineering, '(?m)^_RUNTIME_VERSION = "([^"]+)"\r?$').Groups[1].Value
if (-not $sdkRuntime -or $sdkVersion -ne $globalJson.sdk.version -or $sdkVersion -ne $globalJson.tools.dotnet) {
    throw 'The native engineering toolchain and both repository SDK pins must agree.'
}
if ($workload.schemaVersion -ne 1 -or $workload.targetFramework -ne 'net10.0' -or $workload.configuration -ne 'Release') {
    throw 'Unsupported dogfood workload contract.'
}
if (Test-Path -LiteralPath (Join-Path $repo '.bazelrc.local')) {
    throw 'The dogfood requires no .bazelrc.local override. Preserve that file and use an isolated checkout.'
}

$selection = @($workload.tests)
if ($Targets.Count) {
    $unknown = @($Targets | Where-Object { $_ -notin $workload.tests.label })
    if ($unknown.Count -or @($Targets | Select-Object -Unique).Count -ne $Targets.Count) {
        throw 'Targets must be distinct labels from dogfood-workload.json.'
    }
    $selection = @($selection | Where-Object label -in $Targets)
}
foreach ($test in $selection) {
    if ($test.label -notmatch '^//:[a-z][a-z0-9_]*$' -or $test.project -notmatch '^[A-Za-z0-9.]+$' -or
        $test.assembly -notmatch '^[A-Za-z0-9.]+$' -or $test.runtime -notin @('sdk', 'selfContained')) {
        throw 'Invalid unit-workload entry.'
    }
    $project = Join-Path $repo "test\UnitTests\$($test.project)\$($test.project).csproj"
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Missing unit project: $project" }
}

function Get-TextHash([string] $Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text)))
}

function Get-SourceState {
    $head = & git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source revision.' }
    $status = @(& git -C $repo status --porcelain=v1)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify working-tree changes.' }
    $diff = & git -C $repo diff --binary HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fingerprint tracked source changes.' }
    $untracked = @(& git -C $repo ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fingerprint untracked source changes.' }
    $hashes = foreach ($path in $untracked) {
        "$path|$((Get-FileHash -LiteralPath (Join-Path $repo $path) -Algorithm SHA256).Hash)"
    }
    [ordered]@{ Revision = $head.Trim(); WorkingTree = $status; PatchHash = Get-TextHash (($diff -join "`n") + ($hashes -join "`n")) }
}

$source = Get-SourceState
$scope = [ordered]@{
    Schema = 1
    WorkloadHash = (Get-FileHash -LiteralPath $workloadPath -Algorithm SHA256).Hash
    Targets = @($selection.label)
    TargetFramework = $workload.targetFramework
    Configuration = $workload.configuration
    Sdk = $sdkVersion
    SdkRuntime = $sdkRuntime
    SelfContainedRuntime = $workload.selfContainedRuntime
    IdentityPolicy = $workload.identityPolicy
    Jobs = $Jobs
    TestJobs = $TestJobs
    TestTimeoutSeconds = 1800
    Coverage = $false
    AffectedTests = $false
}
$scopeHash = Get-TextHash ($scope | ConvertTo-Json -Depth 6 -Compress)
$programFilesSdks = foreach ($folder in @("$env:ProgramFiles\dotnet\sdk", "${env:ProgramFiles(x86)}\dotnet\sdk")) {
    if (Test-Path -LiteralPath $folder) {
        Get-ChildItem -LiteralPath $folder -Directory | Select-Object -ExpandProperty Name
    }
}
$identity = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
$hostContract = [ordered]@{
    Os = [Environment]::OSVersion.Version.ToString()
    Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    Processors = [Environment]::ProcessorCount
    Culture = [Globalization.CultureInfo]::CurrentCulture.Name
    Image = $env:ImageVersion
    Administrator = $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    InstalledSdks = @($programFilesSdks | Sort-Object -Unique)
}
$keyFiles = @('.bazelversion', '.bazelrc', 'MODULE.bazel', 'MODULE.bazel.lock', 'global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')
$keyHashes = foreach ($path in $keyFiles) {
    "$path|$((Get-FileHash -LiteralPath (Join-Path $repo $path) -Algorithm SHA256).Hash)"
}
$compatibilityKey = Get-TextHash ($scopeHash + ($hostContract | ConvertTo-Json -Compress) + ($keyHashes -join "`n"))
$runId = (Get-TextHash $RunRoot).Substring(0, 12).ToLowerInvariant()
$shortRoot = Join-Path ([IO.Path]::GetPathRoot($RunRoot)) "tfbd\$runId"
$contextPath = Join-Path $RunRoot 'context.json'
$context = [ordered]@{
    Schema = 1; Mode = $Mode; RunRoot = $RunRoot; CacheRoot = $CacheRoot
    BazelRoot = $shortRoot; Scope = $scope; ScopeHash = $scopeHash
    CacheKey = $compatibilityKey; Host = $hostContract; Source = $source
    PreparedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    JobName = $env:SYSTEM_JOBNAME; JobId = $env:SYSTEM_JOBID
}
if (Test-Path -LiteralPath $contextPath) {
    $previous = Get-Content -LiteralPath $contextPath -Raw | ConvertFrom-Json
    if ($Prepare -or $previous.Mode -ne $Mode -or $previous.ScopeHash -ne $scopeHash -or
        $previous.CacheKey -ne $compatibilityKey -or $previous.Source.Revision -ne $source.Revision -or
        $previous.Source.PatchHash -ne $source.PatchHash -or $previous.CacheRoot -ne $CacheRoot) {
        throw 'Prepared invocation does not match the requested source, host or workload.'
    }
    $context.PreparedUtc = $previous.PreparedUtc
}
else {
    if ((Test-Path -LiteralPath $RunRoot) -and @(Get-ChildItem -LiteralPath $RunRoot -Force).Count) {
        throw 'RunRoot must be new or an empty task-owned directory.'
    }
    if (Test-Path -LiteralPath $shortRoot) { throw "Bazel output root already exists: $shortRoot" }
    New-Item -ItemType Directory -Path $RunRoot -Force | Out-Null
    $context | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $contextPath -Encoding utf8
    $probe = Join-Path $RunRoot 'symlink-probe'
    try {
        $null = [IO.File]::CreateSymbolicLink($probe, $contextPath)
        if (-not (Test-Path -LiteralPath $probe)) { throw 'The Windows agent cannot follow native build symlinks.' }
    }
    finally {
        if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe }
    }
}
foreach ($directory in @('actions', 'downloads', 'msbuild')) {
    New-Item -ItemType Directory -Path (Join-Path $CacheRoot $directory) -Force | Out-Null
}
if ($Prepare) {
    Write-Host "##vso[task.setvariable variable=DogfoodCacheKey]$compatibilityKey"
    Write-Host "Prepared $Mode, $($selection.Count) unit targets, scope $scopeHash."
    return
}
if (Test-Path -LiteralPath $shortRoot) { throw 'The measured Bazel output root must be fresh, even after preparation.' }

$lock = [IO.File]::Open((Join-Path $RunRoot 'invocation.lock'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$commands = [Collections.Generic.List[object]]::new()
$children = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    Schema = 1; Mode = $Mode; Source = $source; Scope = $scope; ScopeHash = $scopeHash
    Host = $hostContract; CacheKey = $compatibilityKey
    JobName = $env:SYSTEM_JOBNAME; JobId = $env:SYSTEM_JOBID
    StartedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Completed = $false; Success = $false; Commands = $commands; Tests = @()
    BuildSeconds = $null; RestoreSeconds = $null; CompilationSeconds = $null
    TestSeconds = $null; HarnessSeconds = $null
    BuildEvents = $null; TestEvents = $null
    Error = $null
}
$watch = [Diagnostics.Stopwatch]::StartNew()
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$pathEntries = @(
    (Split-Path $windowsPowerShell -Parent), "$env:SystemRoot\System32", $env:SystemRoot,
    (Split-Path $pwsh -Parent)
)
foreach ($tool in @('git', 'node', 'python')) {
    $resolved = Get-Command $tool -CommandType Application -ErrorAction SilentlyContinue
    if ($resolved) { $pathEntries += Split-Path $resolved.Source -Parent }
}
$pathEntries += [Environment]::GetEnvironmentVariable('PATH', 'Machine') -split ';'
$environment = @{
    PATH = (@($pathEntries | Where-Object { $_ } | Select-Object -Unique) -join ';')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_MULTILEVEL_LOOKUP = '0'
    TESTFX_BAZEL_OUTPUT_ROOT = $shortRoot
}

function Start-RecordedProcess([string] $Name, [string] $Executable, [string[]] $Arguments, [hashtable] $ExtraEnvironment = @{}) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.WorkingDirectory = $repo
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    foreach ($key in $environment.Keys) { $info.Environment[$key] = $environment[$key] }
    foreach ($key in $ExtraEnvironment.Keys) { $info.Environment[$key] = $ExtraEnvironment[$key] }
    $stdoutPath = Join-Path $RunRoot "$Name.stdout.log"
    $stderrPath = Join-Path $RunRoot "$Name.stderr.log"
    $stdout = [IO.FileStream]::new($stdoutPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, [IO.FileOptions]::Asynchronous)
    $stderr = [IO.FileStream]::new($stderrPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, [IO.FileOptions]::Asynchronous)
    try { $process = [Diagnostics.Process]::Start($info) }
    catch {
        $stdout.Dispose()
        $stderr.Dispose()
        throw
    }
    $child = [pscustomobject]@{
        Name = $Name; Process = $process; Watch = [Diagnostics.Stopwatch]::StartNew()
        Output = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        ErrorOutput = $process.StandardError.BaseStream.CopyToAsync($stderr)
        OutputStream = $stdout; ErrorStream = $stderr; Stdout = $stdoutPath; Stderr = $stderrPath
        Executable = $Executable; Arguments = $Arguments
    }
    $children.Add($child)
    return $child
}

function Complete-RecordedProcess($Child) {
    $Child.Process.WaitForExit()
    $Child.Watch.Stop()
    try {
        $null = $Child.Output.GetAwaiter().GetResult()
        $null = $Child.ErrorOutput.GetAwaiter().GetResult()
    }
    finally {
        $Child.OutputStream.Dispose()
        $Child.ErrorStream.Dispose()
    }
    $result = [pscustomobject]@{
        Name = $Child.Name; Executable = $Child.Executable; Arguments = $Child.Arguments
        ExitCode = $Child.Process.ExitCode; Seconds = [math]::Round($Child.Watch.Elapsed.TotalSeconds, 3)
        Stdout = $Child.Stdout; Stderr = $Child.Stderr
    }
    $commands.Add($result)
    $children.Remove($Child) | Out-Null
    $Child.Process.Dispose()
    Write-Host "$($result.Name): exit $($result.ExitCode), $($result.Seconds)s"
    if ($result.ExitCode -ne 0) {
        Get-Content -LiteralPath $Child.Stderr -Tail 20 | Write-Host
        Get-Content -LiteralPath $Child.Stdout -Tail 20 | Write-Host
    }
    return $result
}

function Invoke-RecordedProcess([string] $Name, [string] $Executable, [string[]] $Arguments, [hashtable] $ExtraEnvironment = @{}) {
    Complete-RecordedProcess (Start-RecordedProcess $Name $Executable $Arguments $ExtraEnvironment)
}

function Invoke-RecordedScript(
    [string] $Name,
    [string] $PowerShell,
    [string] $Script,
    [string[]] $Arguments = @(),
    [hashtable] $Parameters = @{},
    [hashtable] $ExtraEnvironment = @{}) {
    # -File reparses colon-bearing unknown options, including Bazel's labels and
    # --output_base=Q:\path. Keep those values as data all the way to the script.
    $invocation = [ordered]@{ Script = $Script; Arguments = $Arguments; Parameters = $Parameters }
    $path = Join-Path $RunRoot "$Name.invocation.json"
    $invocation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
    $entry = @'
$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0
$call = Get-Content -LiteralPath $env:TESTFX_DOGFOOD_INVOCATION -Raw | ConvertFrom-Json
if (@($call.Parameters.PSObject.Properties).Count) {
    $parameters = @{}
    foreach ($property in $call.Parameters.PSObject.Properties) { $parameters[$property.Name] = $property.Value }
    & $call.Script @parameters
}
else {
    $arguments = [string[]]@($call.Arguments)
    & $call.Script @arguments
}
exit $LASTEXITCODE
'@
    $envForScript = $ExtraEnvironment.Clone()
    $envForScript.TESTFX_DOGFOOD_INVOCATION = $path
    $result = Invoke-RecordedProcess $Name $PowerShell @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-OutputFormat', 'Text',
        '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($entry))
    ) $envForScript
    $result | Add-Member -NotePropertyName Invocation -NotePropertyValue $invocation
    return $result
}

$bazelStartup = @('--nohome_rc', '--nosystem_rc', '--host_jvm_args=-Xmx4g', "--output_base=$shortRoot\o")
$bazelFlags = @(
    "--compilation_mode=$($workload.bazelConfiguration)", "--jobs=$Jobs",
    "--@rules_dotnet//dotnet:target_framework=$($workload.targetFramework)",
    '--extra_toolchains=@dotnet_engineering_toolchains//:all',
    "--repository_cache=$CacheRoot\downloads",
    "--repo_contents_cache=$shortRoot\repositories",
    '--remote_cache=', '--remote_executor=', '--bes_backend=',
    '--lockfile_mode=error', '--guard_against_concurrent_changes',
    '--experimental_convenience_symlinks=ignore',
    '--noshow_progress', '--show_result=0', '--color=no', '--curses=no'
)
function Invoke-Bazel([string] $Name, [string] $Command, [string[]] $Arguments = @()) {
    Invoke-RecordedScript -Name $Name -PowerShell $pwsh -Script (Join-Path $repo 'eng\bazel.ps1') `
        -Arguments ($bazelStartup + $Command + $Arguments)
}

function Add-TestEvidence($Test, [string] $Trx, [bool] $Cached, [int] $ExitCode) {
    $parsed = Read-BazelDogfoodTrx -Path $Trx -IdentityPolicy $workload.identityPolicy
    $outcomes = [string[]]@($parsed.Cases | ForEach-Object { "$($_.Key)|$($_.Outcome)" })
    [Array]::Sort($outcomes, [StringComparer]::Ordinal)
    $report.Tests += [pscustomobject]@{
        Label = $Test.label; Project = $Test.project; TargetFramework = $workload.targetFramework
        Runtime = if ($Test.runtime -eq 'sdk') { $sdkRuntime } else { $workload.selfContainedRuntime }
        Cached = $Cached; ExitCode = $ExitCode
        Total = $parsed.Total; Executed = $parsed.Executed; Passed = $parsed.Passed
        Failed = $parsed.Failed; Skipped = $parsed.Skipped; Other = $parsed.Other
        SelectionHash = $parsed.SelectionHash; OutcomeHash = Get-TextHash ($outcomes -join "`n")
        Trx = $Trx; TrxSha256 = (Get-FileHash -LiteralPath $Trx -Algorithm SHA256).Hash
    }
}

$bazelStarted = $false
try {
    Import-Module (Join-Path $PSScriptRoot 'dogfood-results.psm1') -Force
    $reports = Join-Path $RunRoot 'reports'
    foreach ($kind in @('executed', 'cached')) {
        New-Item -ItemType Directory -Path (Join-Path $reports $kind) -Force | Out-Null
    }
    if ($Mode -ne 'MSBuild') {
        $bazelStarted = $true
        $diskCache = if ($Mode -eq 'Cold') { Join-Path $RunRoot 'cold-actions' } else { Join-Path $CacheRoot 'actions' }
        if ($Mode -eq 'Cold' -and (Test-Path -LiteralPath $diskCache)) { throw 'Cold mode requires a new compiled-output/test cache.' }
        New-Item -ItemType Directory -Path $diskCache -Force | Out-Null
        $inventory = Invoke-Bazel 'selection' 'query' @(
            'tests(//:all_unit_tests)', '--output=label', '--noshow_progress', '--lockfile_mode=error',
            "--repository_cache=$CacheRoot\downloads", "--repo_contents_cache=$shortRoot\repositories"
        )
        if ($inventory.ExitCode -ne 0) { throw 'Could not reconcile the native unit-suite inventory.' }
        $declared = [string[]]@(Get-Content -LiteralPath $inventory.Stdout | Where-Object { $_ -match '^//:' })
        $expected = [string[]]@($workload.tests.label)
        [Array]::Sort($declared, [StringComparer]::Ordinal)
        [Array]::Sort($expected, [StringComparer]::Ordinal)
        if (($declared -join "`n") -cne ($expected -join "`n")) { throw 'The unit-workload manifest does not match //:all_unit_tests.' }
        $flags = $bazelFlags + @(
            "--disk_cache=$diskCache", "--local_test_jobs=$TestJobs",
            '--runs_per_test=1', '--flaky_test_attempts=1', '--test_sharding_strategy=disabled',
            "--test_timeout=$($scope.TestTimeoutSeconds)",
            "--test_env=TESTFX_DOGFOOD_HOST=$compatibilityKey",
            $(if ($Mode -eq 'Cached') { '--cache_test_results=auto' } else { '--cache_test_results=no' })
        )
        $build = Invoke-Bazel 'build' 'build' (@($selection.label) + $flags + "--build_event_json_file=$RunRoot\build.bep.jsonl")
        $report.BuildSeconds = $build.Seconds
        $report.BuildEvents = Read-BazelDogfoodEvents -Path (Join-Path $RunRoot 'build.bep.jsonl')
        if ($build.ExitCode -ne 0 -or -not $report.BuildEvents.Success) { throw 'Native build failed; no successful timing sample.' }
        $toolchain = Invoke-Bazel 'toolchain' 'cquery' (@(
            '@rules_dotnet//dotnet:resolved_toolchain', '--output=starlark',
            '--starlark:expr="DOGFOOD_TOOLCHAIN|" + providers(target)["ToolchainInfo"].dotnetinfo.sdk_version + "|" + providers(target)["ToolchainInfo"].dotnetinfo.runtime_version'
        ) + $flags)
        $toolchainText = Get-Content -LiteralPath $toolchain.Stdout -Raw
        if ($toolchain.ExitCode -ne 0 -or $toolchainText -notmatch [regex]::Escape("DOGFOOD_TOOLCHAIN|$sdkVersion|$sdkRuntime")) {
            throw 'The resolved native SDK/runtime differs from the comparison contract.'
        }
        $published = @($selection | Where-Object runtime -eq 'selfContained')
        if ($published.Count) {
            $execution = Invoke-Bazel 'execution-root' 'info' (@('execution_root') + $flags)
            if ($execution.ExitCode -ne 0) { throw 'Cannot locate the native runtime layouts.' }
            $executionRoot = (Get-Content -LiteralPath $execution.Stdout -Raw).Trim()
            $expression = 'set(' + (($published | ForEach-Object { $_.label + '_app' }) -join ' ') + ')'
            $files = Invoke-Bazel 'published-runtimes' 'cquery' (@($expression, '--output=files') + $flags)
            if ($files.ExitCode -ne 0) { throw 'Cannot inspect native self-contained runtime contracts.' }
            $paths = @(Get-Content -LiteralPath $files.Stdout)
            foreach ($test in $published) {
                $config = @($paths | Where-Object { [IO.Path]::GetFileName($_) -eq "$($test.assembly).runtimeconfig.json" })
                if ($config.Count -ne 1) { throw "Missing or ambiguous runtime configuration: $($test.label)" }
                $configPath = Join-Path $executionRoot $config[0].Replace('/', '\')
                $options = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).runtimeOptions
                $framework = @($options.includedFrameworks | Where-Object name -in @('Microsoft.NETCore.App', 'Microsoft.NETCore.App.Runtime.win-x64'))
                if ($framework.Count -ne 1 -or $framework[0].version -ne $workload.selfContainedRuntime) {
                    throw "Native self-contained runtime drifted from the comparison contract: $($test.label)"
                }
                $corelib = Get-Item -LiteralPath (Join-Path (Split-Path $configPath -Parent) 'System.Private.CoreLib.dll')
                if ($corelib.VersionInfo.ProductVersion -notmatch ('^' + [regex]::Escape($workload.selfContainedRuntime) + '(-|\+|$)')) {
                    throw "Native CoreLib differs from its declared runtime: $($test.label)"
                }
            }
        }
        $testArguments = @($selection.label) + $flags + @(
            '--keep_going', '--test_output=errors', "--build_event_json_file=$RunRoot\test.bep.jsonl"
        )
        $tested = Invoke-Bazel 'tests' 'test' $testArguments
        $report.TestSeconds = $tested.Seconds
        $report.TestEvents = Read-BazelDogfoodEvents -Path (Join-Path $RunRoot 'test.bep.jsonl') -ExpectedTargets @($selection.label)
        $location = Invoke-Bazel 'testlogs' 'info' (@('bazel-testlogs') + $flags)
        if ($location.ExitCode -ne 0) { throw 'Cannot locate declared native test outputs.' }
        $testLogs = (Get-Content -LiteralPath $location.Stdout -Raw).Trim()
        foreach ($test in $selection) {
            $name = $test.label.Substring(3)
            $event = @($report.TestEvents.Targets | Where-Object Label -eq $test.label)
            if ($event.Count -ne 1) { throw "Missing or ambiguous native test result: $($test.label)" }
            if ($Mode -ne 'Cached' -and $event[0].Cached) { throw 'A forced-execution control reused a test result.' }
            $kind = if ($event[0].Cached) { 'cached' } else { 'executed' }
            $trx = Join-Path $reports "$kind\$name.trx"
            Copy-Item -LiteralPath (Join-Path $testLogs "$name\test.outputs\tests.trx") -Destination $trx
            Add-TestEvidence $test $trx $event[0].Cached $(if ($event[0].Status -eq 'PASSED') { 0 } else { $tested.ExitCode })
        }
        $report.Success = $tested.ExitCode -eq 0 -and $report.TestEvents.Success
    }
    else {
        $filter = Join-Path $RunRoot 'unit-tests.slnf'
        [ordered]@{ solution = [ordered]@{
            path = Join-Path $repo 'TestFx.slnx'
            projects = @($selection | ForEach-Object { "test\UnitTests\$($_.project)\$($_.project).csproj" })
        } } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $filter -Encoding utf8
        $bin = Join-Path $repo "artifacts\bin\dogfood-$runId\"
        $obj = Join-Path $repo "artifacts\obj\dogfood-$runId\"
        if ((Test-Path -LiteralPath $bin) -or (Test-Path -LiteralPath $obj)) { throw 'MSBuild outputs must be fresh.' }
        $sdkRoot = Join-Path $CacheRoot 'msbuild\dotnet'
        $buildEnvironment = @{
            NUGET_PACKAGES = "$CacheRoot\msbuild\packages"
            DOTNET_CLI_HOME = "$RunRoot\dotnet-home"
            DOTNET_GLOBAL_INSTALL_DIR = $sdkRoot
            DOTNET_INSTALL_DIR = $sdkRoot
            DOTNET_ROOT = $sdkRoot
            DOTNET_ROOT_X64 = $sdkRoot
            DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $sdkRoot
        }
        # A previously restored Arcade toolset can bypass SDK initialization.
        # Bootstrap through its supported entry point before selecting workloads.
        $bootstrap = Invoke-RecordedScript -Name 'sdk-bootstrap' -PowerShell $pwsh `
            -Script (Join-Path $repo 'eng\common\dotnet.ps1') -Arguments @('--version') -ExtraEnvironment $buildEnvironment
        if ($bootstrap.ExitCode -ne 0) { throw 'The repository SDK bootstrap failed.' }
        $commonProperties = @(
            "/m:$Jobs", '/p:EnableCodeCoverage=false',
            "/p:ArtifactsBinDir=$bin", "/p:ArtifactsObjDir=$obj", "/p:ArtifactsLogDir=$RunRoot\msbuild-logs\"
        )
        # Restore the declared multi-targeted graph before selecting net10.0.
        # A global TargetFramework during restore changes package-pruning policy
        # for dependencies that also need those packages on older frameworks.
        $restore = Invoke-RecordedScript -Name 'restore' -PowerShell $pwsh -Script (Join-Path $repo 'eng\build.ps1') `
            -Parameters @{
                restore = $true; configuration = $workload.configuration; projects = $filter
                properties = $commonProperties + "/bl:$RunRoot\restore-$([Guid]::NewGuid().ToString('N')).binlog"
            } -ExtraEnvironment $buildEnvironment
        $report.RestoreSeconds = $restore.Seconds
        if ($restore.ExitCode -ne 0) { throw 'MSBuild restore failed; no build or test may use its partial assets.' }
        foreach ($test in $selection) {
            $assetsPath = Join-Path $obj "$($test.project)\project.assets.json"
            $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
            if ($workload.targetFramework -notin $assets.project.frameworks.PSObject.Properties.Name) {
                throw "Restore did not cover the selected framework: $($test.project)"
            }
        }
        $buildArguments = $commonProperties + @(
            "/p:TargetFramework=$($workload.targetFramework)", '/restore:false',
            "/bl:$RunRoot\build-$([Guid]::NewGuid().ToString('N')).binlog"
        )
        $build = Invoke-RecordedScript -Name 'build' -PowerShell $pwsh -Script (Join-Path $repo 'eng\build.ps1') `
            -Parameters @{ restore = $false; build = $true; configuration = $workload.configuration; projects = $filter; properties = $buildArguments } `
            -ExtraEnvironment $buildEnvironment
        $report.CompilationSeconds = $build.Seconds
        $report.BuildSeconds = [math]::Round($restore.Seconds + $build.Seconds, 3)
        if ($build.ExitCode -ne 0) { throw 'MSBuild failed; no successful timing sample.' }
        if ('//:msbuild_tests' -in $selection.label) {
            $legacyProject = Join-Path $repo 'src\Platform\Microsoft.Testing.Platform.MSBuild\Microsoft.Testing.Platform.MSBuild.csproj'
            $legacy = Invoke-RecordedScript -Name 'legacy-fixture' -PowerShell $pwsh -Script (Join-Path $repo 'eng\build.ps1') `
                -Parameters @{
                    restore = $false; build = $true; configuration = $workload.configuration; projects = $legacyProject
                    properties = $commonProperties + @(
                        '/p:TargetFramework=netstandard2.0', '/restore:false',
                        "/bl:$RunRoot\legacy-fixture-$([Guid]::NewGuid().ToString('N')).binlog"
                    )
                } -ExtraEnvironment $buildEnvironment
            $report.BuildSeconds = [math]::Round($report.BuildSeconds + $legacy.Seconds, 3)
            if ($legacy.ExitCode -ne 0) { throw 'The original legacy-task fixture could not be built.' }
            $legacyAssembly = Join-Path $bin "Microsoft.Testing.Platform.MSBuild\$($workload.configuration)\netstandard2.0\Microsoft.Testing.Platform.MSBuild.dll"
            $legacyDirectory = Join-Path $bin "Microsoft.Testing.Platform.MSBuild.UnitTests\$($workload.configuration)\$($workload.targetFramework)\LegacyMSBuild"
            New-Item -ItemType Directory -Path $legacyDirectory -Force | Out-Null
            Copy-Item -LiteralPath $legacyAssembly -Destination $legacyDirectory
            $report.LegacyFixture = [ordered]@{
                Path = Join-Path $legacyDirectory 'Microsoft.Testing.Platform.MSBuild.dll'
                Sha256 = (Get-FileHash -LiteralPath $legacyAssembly -Algorithm SHA256).Hash
            }
        }
        $dotnet = Join-Path $sdkRoot 'dotnet.exe'
        $sdk = Invoke-RecordedProcess 'sdk' $dotnet @('--version')
        if ($sdk.ExitCode -ne 0 -or (Get-Content -LiteralPath $sdk.Stdout -Raw).Trim() -ne $sdkVersion) {
            throw 'MSBuild did not use the exact pinned SDK.'
        }
        $runtimeRoot = Join-Path $CacheRoot "msbuild\runtimes\$($workload.selfContainedRuntime)"
        if (@($selection | Where-Object runtime -eq 'selfContained').Count -and
            -not (Test-Path -LiteralPath (Join-Path $runtimeRoot "shared\Microsoft.NETCore.App\$($workload.selfContainedRuntime)\System.Private.CoreLib.dll"))) {
            $install = Invoke-RecordedScript -Name 'runtime-bootstrap' -PowerShell $pwsh `
                -Script (Join-Path $repo 'eng\common\dotnet-install.ps1') `
                -Parameters @{ runtime = 'dotnet'; version = $workload.selfContainedRuntime; dotnetPath = $runtimeRoot; architecture = 'x64' }
            if ($install.ExitCode -ne 0) { throw 'The matching native self-contained runtime could not be installed privately.' }
        }
        $pending = [Collections.Generic.Queue[object]]::new()
        foreach ($test in $selection) { $pending.Enqueue($test) }
        $running = [Collections.Generic.List[object]]::new()
        $testWatch = [Diagnostics.Stopwatch]::StartNew()
        while ($pending.Count -or $running.Count) {
            while ($pending.Count -and $running.Count -lt $TestJobs) {
                $test = $pending.Dequeue()
                $name = $test.label.Substring(3)
                $dll = Join-Path $bin "$($test.project)\$($workload.configuration)\$($workload.targetFramework)\$($test.assembly).dll"
                if (-not (Test-Path -LiteralPath $dll)) { throw "Missing selected MSBuild assembly: $dll" }
                $hostRoot = if ($test.runtime -eq 'sdk') { Split-Path $dotnet -Parent } else { $runtimeRoot }
                $runtime = if ($test.runtime -eq 'sdk') { $sdkRuntime } else { $workload.selfContainedRuntime }
                $testEnvironment = @{
                    DOTNET_ROOT = $hostRoot; DOTNET_ROOT_X64 = $hostRoot; DOTNET_ROLL_FORWARD = 'LatestPatch'
                    PATH = "$hostRoot;$($environment.PATH)"; NUGET_PACKAGES = "$CacheRoot\msbuild\packages"
                }
                $child = Start-RecordedProcess "test-$name" (Join-Path $hostRoot 'dotnet.exe') @(
                    'exec', '--fx-version', $runtime, $dll, '--minimum-expected-tests', '1',
                    '--results-directory', (Join-Path $reports 'executed'), '--report-trx', '--report-trx-filename', "$name.trx"
                ) $testEnvironment
                $running.Add([pscustomobject]@{ Test = $test; Child = $child })
            }
            foreach ($item in @($running.ToArray())) {
                if (-not $item.Child.Process.HasExited) {
                    if ($item.Child.Watch.Elapsed.TotalSeconds -gt $scope.TestTimeoutSeconds) {
                        throw "Unit-test process exceeded the shared $($scope.TestTimeoutSeconds)-second limit: $($item.Test.label)"
                    }
                    continue
                }
                $result = Complete-RecordedProcess $item.Child
                $trx = Join-Path $reports "executed\$($item.Test.label.Substring(3)).trx"
                Add-TestEvidence $item.Test $trx $false $result.ExitCode
                $running.Remove($item) | Out-Null
            }
            if ($running.Count) { Start-Sleep -Milliseconds 100 }
        }
        $testWatch.Stop()
        $report.TestSeconds = [math]::Round($testWatch.Elapsed.TotalSeconds, 3)
        $report.Success = @($report.Tests | Where-Object { $_.ExitCode -ne 0 }).Count -eq 0
    }
    if ($report.Tests.Count -ne $selection.Count) { throw 'Incomplete unit-workload evidence.' }
    if (@($report.Tests | Where-Object { $_.Failed -gt 0 -or $_.Other -gt 0 }).Count) { $report.Success = $false }
    $after = Get-SourceState
    if ($after.Revision -ne $source.Revision -or $after.PatchHash -ne $source.PatchHash) {
        throw 'Source changed during the measured invocation.'
    }
    $report.Completed = $true
}
catch {
    $report.Success = $false
    $report.Error = $_.Exception.Message
    Write-Host "Dogfood failed: $($report.Error)"
}
finally {
    try {
        foreach ($child in $children.ToArray()) {
            if (-not $child.Process.HasExited) { $child.Process.Kill($true) }
            $null = Complete-RecordedProcess $child
        }
        if ($bazelStarted) {
            $shutdown = Invoke-Bazel 'shutdown' 'shutdown'
            if ($shutdown.ExitCode -ne 0) { throw 'Owned Bazel server shutdown failed.' }
        }
    }
    catch {
        $report.Success = $false
        $report.Error = "$($report.Error) Cleanup failed: $($_.Exception.Message)"
        Write-Host $report.Error
    }
    finally {
        $watch.Stop()
        $report.HarnessSeconds = [math]::Round($watch.Elapsed.TotalSeconds, 3)
        $report.FinishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $RunRoot 'result.json') -Encoding utf8
        $lock.Dispose()
    }
}
Write-Host "Evidence: $RunRoot"
if (-not $report.Success) { exit 1 }
exit 0
