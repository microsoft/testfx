#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateRange(1, 256)]
    [int]$Workers = 8,
    [ValidateRange(1, 30)]
    [int]$Repetitions = 3,
    [ValidateRange(1, 10)]
    [int]$Blocks = 2,
    [ValidateSet('serial', 'processes-no-reuse', 'threads-no-reuse', 'processes-reuse', 'threads-reuse')]
    [string[]]$Cases = @('serial', 'processes-no-reuse', 'threads-no-reuse', 'processes-reuse', 'threads-reuse'),
    [string]$OutputDirectory,
    [switch]$SkipBinaryLogs,
    [Parameter(DontShow)]
    [switch]$SingleBuild,
    [Parameter(DontShow)]
    [string]$CaseName,
    [Parameter(DontShow)]
    [string]$BinaryLog
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) {
    throw 'This reproduction measures the Windows solution. Run it with PowerShell 7 on Windows.'
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$dotnetRoot = Join-Path $repoRoot '.dotnet'
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$sdkVersion = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
if (!(Test-Path -LiteralPath (Join-Path $dotnetRoot "sdk\$sdkVersion\MSBuild.dll"))) {
    throw 'The repository-local pinned SDK is missing. Follow docs\investigations\msbuild-incremental-performance.md to prepare it.'
}

$casesByName = [ordered]@{
    'serial' = [pscustomobject]@{ Name = 'serial'; Nodes = 1; Reuse = $false; Threads = $false }
    'processes-no-reuse' = [pscustomobject]@{ Name = 'processes-no-reuse'; Nodes = $Workers; Reuse = $false; Threads = $false }
    'threads-no-reuse' = [pscustomobject]@{ Name = 'threads-no-reuse'; Nodes = $Workers; Reuse = $false; Threads = $true }
    'processes-reuse' = [pscustomobject]@{ Name = 'processes-reuse'; Nodes = $Workers; Reuse = $true; Threads = $false }
    'threads-reuse' = [pscustomobject]@{ Name = 'threads-reuse'; Nodes = $Workers; Reuse = $true; Threads = $true }
}
if ($SingleBuild) {
    if (!$casesByName.Contains($CaseName)) {
        throw "Unknown build case '$CaseName'."
    }
    Set-Location -LiteralPath $repoRoot
    $case = $casesByName[$CaseName]
    $buildParameters = @{
        build = $true
        msbuildEngine = 'dotnet'
        projects = (Join-Path $repoRoot 'TestFx.slnx')
        nodeReuse = $case.Reuse
        verbosity = 'quiet'
    }
    $buildArguments = @(
        "/m:$($case.Nodes)"
        "/mt:$($case.Threads.ToString().ToLowerInvariant())"
        '/p:SkipUapTargetFramework=true'
        '/p:SkipRuntimesInstall=true'
        '/tl:false'
    )
    if ($BinaryLog) {
        $buildArguments += "/bl:$BinaryLog;ProjectImports=None"
    }
    & (Join-Path $PSScriptRoot 'build.ps1') @buildParameters @buildArguments
    exit $LASTEXITCODE
}

$Cases = @($Cases | ForEach-Object { $_.ToLowerInvariant() })
if (!$Cases.Count -or @($Cases | Select-Object -Unique).Count -ne $Cases.Count) {
    throw 'Select at least one case, without duplicates.'
}
$selectedCases = @($Cases | ForEach-Object { $casesByName[$_] })
$git = (Get-Command git.exe -ErrorAction Stop).Source
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$source = Join-Path $repoRoot 'src\Platform\Microsoft.Testing.Extensions.Configuration\MicrosoftExtensionsConfigurationSnapshotSource.cs'
$sourceTimestamp = [IO.File]::GetLastWriteTimeUtc($source)
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\msbuild-incremental'
}
$runId = [guid]::NewGuid().ToString('N')
$runRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $runId.Substring(0, 8))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$rows = [Collections.Generic.List[object]]::new()
$processHistory = [Collections.Generic.List[object]]::new()
$ownedProcesses = @{}
$sequence = 0

function Get-ProcessSnapshot {
    @(Get-CimInstance Win32_Process -Filter "Name = 'pwsh.exe' OR Name = 'powershell.exe' OR Name = 'dotnet.exe' OR Name = 'MSBuild.exe' OR Name = 'VBCSCompiler.exe'" |
        Select-Object Name, ProcessId, ParentProcessId, CreationDate, ExecutablePath, CommandLine, WorkingSetSize)
}

function Update-OwnedProcesses {
    $snapshot = @(Get-ProcessSnapshot)
    $liveById = @{}
    foreach ($entry in $snapshot) {
        $liveById[[int]$entry.ProcessId] = $entry
    }
    do {
        $added = $false
        foreach ($entry in $snapshot) {
            $parentId = [int]$entry.ParentProcessId
            $id = [int]$entry.ProcessId
            if ($ownedProcesses.ContainsKey($id) -and $ownedProcesses[$id].CreationDate -eq $entry.CreationDate) {
                continue
            }
            # A matching PID alone is not ownership: the live parent must be the process we launched.
            if ($ownedProcesses.ContainsKey($parentId) -and $liveById.ContainsKey($parentId) -and
                $ownedProcesses[$parentId].CreationDate -eq $liveById[$parentId].CreationDate -and
                $entry.CreationDate -ge $ownedProcesses[$parentId].CreationDate) {
                $ownedProcesses[$id] = $entry
                $added = $true
            }
        }
    } while ($added)

    @($snapshot | Where-Object {
        $ownedProcesses.ContainsKey([int]$_.ProcessId) -and
        $ownedProcesses[[int]$_.ProcessId].CreationDate -eq $_.CreationDate -and
        $_.ExecutablePath -and $_.ExecutablePath.StartsWith($dotnetRoot + '\', [StringComparison]::OrdinalIgnoreCase)
    })
}

function Stop-OwnedProcesses {
    param([switch]$IncludeCompiler)

    foreach ($entry in @(Update-OwnedProcesses)) {
        if ($entry.Name -eq 'VBCSCompiler.exe' -and !$IncludeCompiler) {
            continue
        }
        $live = Get-CimInstance Win32_Process -Filter "ProcessId = $($entry.ProcessId)"
        if ($live -and $live.CreationDate -eq $entry.CreationDate -and $live.ExecutablePath -eq $entry.ExecutablePath) {
            Stop-Process -Id $entry.ProcessId -Force -ErrorAction Stop
        }
    }
}

function Invoke-Measurement {
    param([object]$Case, [int]$Block, [int]$Iteration, [string]$Scenario, [bool]$Measured, [string]$LogPath)

    $script:sequence++
    $id = '{0:D3}-{1}-b{2}-{3}-{4}' -f $script:sequence, $Case.Name, $Block, $Scenario, $Iteration
    $before = @(Update-OwnedProcesses | Where-Object Name -NE 'VBCSCompiler.exe')
    if ($Scenario -eq 'source-touch') {
        [IO.File]::SetLastWriteTimeUtc($source, [DateTime]::UtcNow)
    }
    $startInfo = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['PATH'] = @(
        $dotnetRoot, $PSHOME, "$env:SystemRoot\System32\WindowsPowerShell\v1.0",
        "$env:SystemRoot\System32", $env:SystemRoot, (Split-Path $git -Parent)
    ) -join ';'
    $startInfo.Environment.Remove('DOTNET_GLOBAL_INSTALL_DIR') | Out-Null
    $startInfo.Environment.Remove('DOTNET_INSTALL_DIR') | Out-Null
    $startInfo.Environment['DOTNET_ROOT'] = $dotnetRoot
    $startInfo.Environment['MSBUILDUSESERVER'] = '1'
    $startInfo.Environment['MSBUILDNODEHANDSHAKESALT'] = "testfx-incremental-$runId-$($Case.Name)-b$Block"
    $startInfo.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $startInfo.Environment['DOTNET_NOLOGO'] = '1'
    $startInfo.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = '1'
    foreach ($argument in @('-NoProfile', '-File', $PSCommandPath, '-SingleBuild', '-CaseName', $Case.Name, '-Workers', $Workers.ToString())) {
        $startInfo.ArgumentList.Add($argument)
    }
    if ($LogPath) {
        $startInfo.ArgumentList.Add('-BinaryLog')
        $startInfo.ArgumentList.Add($LogPath)
    }

    $started = [DateTime]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($startInfo)
    try {
        $rootProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)"
        if (!$rootProcess) {
            throw 'Cannot establish ownership of the build wrapper process.'
        }
        $ownedProcesses[$process.Id] = $rootProcess
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        while (!$process.WaitForExit(1000)) {
            Update-OwnedProcesses | Out-Null
        }
        $watch.Stop()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally {
        if (!$process.HasExited) {
            Update-OwnedProcesses | Out-Null
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
        $process.Dispose()
    }
    $stdoutPath = Join-Path $runRoot ($id + '.log')
    [IO.File]::WriteAllText($stdoutPath, $stdout + $stderr)
    $after = @(Update-OwnedProcesses | Where-Object Name -NE 'VBCSCompiler.exe')
    $beforePids = @($before | ForEach-Object ProcessId)
    $beforeById = @{}
    foreach ($entry in $before) {
        $beforeById[[int]$entry.ProcessId] = $entry.CreationDate
    }
    $retained = @($after | Where-Object {
        $beforeById.ContainsKey([int]$_.ProcessId) -and $beforeById[[int]$_.ProcessId] -eq $_.CreationDate
    })
    $processHistory.Add([pscustomobject]@{ Id = $id; Before = $before; After = $after })
    $reuseArgument = if ($Case.Reuse) { '$true' } else { '$false' }
    $row = [pscustomobject]@{
        Id = $id
        Case = $Case.Name
        Block = $Block
        Iteration = $Iteration
        Scenario = $Scenario
        Measured = $Measured
        Nodes = $Case.Nodes
        Reuse = $Case.Reuse
        MultiThreaded = $Case.Threads
        StartedUtc = $started.ToString('O')
        WallSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 6)
        ExitCode = $exitCode
        BeforePids = ($beforePids -join ',')
        AfterPids = (@($after | ForEach-Object ProcessId) -join ',')
        RetainedPids = (@($retained | ForEach-Object ProcessId) -join ',')
        SourceTimestampUtc = [IO.File]::GetLastWriteTimeUtc($source).ToString('O')
        Command = ".\eng\build.ps1 -build -msbuildEngine dotnet -projects .\TestFx.slnx -nodeReuse:$reuseArgument -verbosity quiet /m:$($Case.Nodes) /mt:$($Case.Threads) /p:SkipUapTargetFramework=true /p:SkipRuntimesInstall=true /tl:false"
        Stdout = $stdoutPath
        Binlog = $LogPath
    }
    $rows.Add($row)
    $row | Export-Csv -LiteralPath (Join-Path $runRoot 'runs.csv') -Append -NoTypeInformation
    $processHistory | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $runRoot 'processes.json') -Encoding utf8
    Write-Host ("{0}: exit {1}, {2:N3}s, reused PIDs [{3}]" -f $id, $exitCode, $watch.Elapsed.TotalSeconds, $row.RetainedPids)
    if ($exitCode -ne 0) {
        throw "Build $id failed. See '$stdoutPath'; failed builds are not performance samples."
    }
    if ($LogPath -and (!(Test-Path -LiteralPath $LogPath) -or (Get-Item -LiteralPath $LogPath).Length -eq 0)) {
        throw "Build $id did not produce its requested binlog."
    }
}

Push-Location -LiteralPath $repoRoot
try {
    $revision = & $git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source revision.' }
    $sdk = & $dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Cannot query the pinned SDK.' }
    $msbuild = & $dotnet msbuild -version -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Cannot query MSBuild.' }
    $metadata = [ordered]@{
        Revision = $revision
        InitialGitStatus = @(& $git status --porcelain)
        Sdk = $sdk
        MSBuild = $msbuild
        LogicalProcessors = [Environment]::ProcessorCount
        MemoryGiB = [Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
        Cases = $selectedCases
        Blocks = $Blocks
        RepetitionsPerBlock = $Repetitions
        Source = $source
        SourceSha256 = $sourceHash
        OriginalSourceTimestampUtc = $sourceTimestamp.ToString('O')
        MSBUILDUSESERVER = '1'
        Notes = 'Warm Build only; restore excluded. Fixed source bytes, timestamp-only invalidation. Timed runs have no binlog. Opposite case order in alternate blocks, fresh owned node pools with unmeasured warmup. One-second descendant sampling verifies PID ownership; pre-existing processes are never stopped.'
    }
    $metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'metadata.json') -Encoding utf8
    Write-Host "Evidence: $runRoot"
    try {
        for ($block = 1; $block -le $Blocks; $block++) {
            $order = @($selectedCases)
            if ($block % 2 -eq 0) {
                [Array]::Reverse($order)
            }
            foreach ($case in $order) {
                Invoke-Measurement -Case $case -Block $block -Iteration 0 -Scenario 'no-change' -Measured $false
                for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
                    Invoke-Measurement -Case $case -Block $block -Iteration $iteration -Scenario 'no-change' -Measured $true
                    Invoke-Measurement -Case $case -Block $block -Iteration $iteration -Scenario 'source-touch' -Measured $true
                }
                if ($block -eq $Blocks -and !$SkipBinaryLogs) {
                    foreach ($scenario in @('no-change', 'source-touch')) {
                        Invoke-Measurement -Case $case -Block $block -Iteration 0 -Scenario $scenario -Measured $false -LogPath (Join-Path $runRoot ("diagnostic-$($case.Name)-$scenario.binlog"))
                    }
                }
                Stop-OwnedProcesses
            }
        }
    }
    finally {
        $metadata['FinalSourceSha256'] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ($metadata.FinalSourceSha256 -eq $sourceHash) {
            [IO.File]::SetLastWriteTimeUtc($source, $sourceTimestamp)
        }
        Stop-OwnedProcesses -IncludeCompiler
        $metadata['FinalSourceTimestampUtc'] = [IO.File]::GetLastWriteTimeUtc($source).ToString('O')
        $metadata['FinalGitStatus'] = @(& $git status --porcelain)
        $metadata['RemainingOwnedProcesses'] = @(Update-OwnedProcesses)
        $metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'metadata.json') -Encoding utf8
        if ($metadata.FinalSourceSha256 -ne $sourceHash) {
            throw 'The source file changed during the experiment. Its contents were not overwritten; discard this comparison.'
        }
        if ($metadata.RemainingOwnedProcesses.Count) {
            throw 'Owned build processes remain after cleanup. Inspect metadata.json.'
        }
    }

    $summary = foreach ($group in ($rows | Where-Object Measured | Group-Object Case, Scenario)) {
        $times = @($group.Group.WallSeconds | Sort-Object)
        $middle = [int][Math]::Floor($times.Count / 2)
        $median = if ($times.Count % 2) { $times[$middle] } else { ($times[$middle - 1] + $times[$middle]) / 2 }
        [pscustomobject]@{
            Case = $group.Group[0].Case
            Scenario = $group.Group[0].Scenario
            Count = $times.Count
            MedianSeconds = [Math]::Round($median, 3)
            MinSeconds = [Math]::Round($times[0], 3)
            MaxSeconds = [Math]::Round($times[-1], 3)
        }
    }
    $summary | Export-Csv -LiteralPath (Join-Path $runRoot 'summary.csv') -NoTypeInformation
    $summary | Format-Table -AutoSize
}
finally {
    Pop-Location
}
