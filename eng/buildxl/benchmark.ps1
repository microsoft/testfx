# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 7.2

[CmdletBinding()]
param(
    [ValidateRange(3, 10)]
    [int] $Samples = 3,
    [ValidateRange(1, 64)]
    [int] $Jobs = 8,
    [string] $ResultsDirectory,
    [switch] $NoJournal,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 2.0

function Get-PerfStatistics([double[]] $Values) {
    if ($Values.Count -lt 3 -or @($Values | Where-Object { -not [double]::IsFinite($_) -or $_ -le 0 }).Count) {
        throw 'Performance statistics require at least three finite, positive samples.'
    }

    $sorted = @($Values | Sort-Object)
    $middle = [int] [math]::Floor($sorted.Count / 2)
    $median = if ($sorted.Count % 2) { $sorted[$middle] } else { ($sorted[$middle - 1] + $sorted[$middle]) / 2 }
    return [pscustomobject]@{ Count = $sorted.Count; MedianSeconds = $median; MinSeconds = $sorted[0]; MaxSeconds = $sorted[-1] }
}

function Write-PerfReport($Report, [string] $Directory) {
    $Report.Summary = @()
    foreach ($scenario in @('Cold', 'Changed', 'NoOp')) {
        $groups = @{}
        foreach ($engine in @('MSBuild', 'BuildXL')) {
            $rows = @($Report.Measurements | Where-Object { $_.Engine -eq $engine -and $_.Scenario -eq $scenario -and $_.ExitCode -eq 0 })
            if ($rows.Count -eq $Report.Samples) {
                $groups[$engine] = Get-PerfStatistics ([double[]] $rows.Seconds)
            }
        }

        if ($groups.Count -eq 2) {
            $Report.Summary += [pscustomobject]@{
                Scenario = $scenario
                MSBuild = $groups.MSBuild
                BuildXL = $groups.BuildXL
                BuildXLSavingsPercent = 100 * ($groups.MSBuild.MedianSeconds - $groups.BuildXL.MedianSeconds) / $groups.MSBuild.MedianSeconds
            }
        }
    }

    $Report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $Directory 'results.json') -Encoding utf8
    $Report.Measurements | Select-Object Pair, Engine, Scenario, Seconds, ExitCode, TotalProcessPips, CacheHits, CacheMisses |
        Export-Csv -LiteralPath (Join-Path $Directory 'timings.csv') -NoTypeInformation
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# BuildXL versus MSBuild: TRX build performance')
    $lines.Add('')
    $lines.Add("Revision: $($Report.Source.Revision). Complete: $($Report.Complete). Samples per engine/scenario: $($Report.Samples).")
    $lines.Add('')
    $lines.Add('Elapsed seconds include fresh PowerShell/driver startup, restore, build/cache lookup, and logging. SDK/tool/package acquisition and scenario setup are outside the timer. Cold means fresh outputs and an empty action cache, not cold NuGet or OS caches.')
    $lines.Add('')
    $lines.Add('| Scenario | MSBuild median (range), s | BuildXL median (range), s | BuildXL savings |')
    $lines.Add('| --- | ---: | ---: | ---: |')
    foreach ($row in $Report.Summary) {
        $lines.Add(('| {0} | {1:F3} ({2:F3}-{3:F3}) | {4:F3} ({5:F3}-{6:F3}) | {7:F1}% |' -f
            $row.Scenario, $row.MSBuild.MedianSeconds, $row.MSBuild.MinSeconds, $row.MSBuild.MaxSeconds,
            $row.BuildXL.MedianSeconds, $row.BuildXL.MinSeconds, $row.BuildXL.MaxSeconds, $row.BuildXLSavingsPercent))
    }

    $lines.Add('')
    $lines.Add('Negative savings means BuildXL is slower. These are paired observations on one machine/workload, not a significance estimate or an end-to-end shipping/CI speedup. No timing threshold is enforced.')
    foreach ($problem in $Report.Problems) { $lines.Add('Problem: ' + $problem.Replace("`n", ' ')) }
    [IO.File]::WriteAllLines((Join-Path $Directory 'summary.md'), $lines)

    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $xml = [Xml.XmlWriter]::Create((Join-Path $Directory 'benchmark.junit.xml'), $settings)
    try {
        $xml.WriteStartDocument()
        $xml.WriteStartElement('testsuite')
        $xml.WriteAttributeString('name', $Report.TrendSeries)
        $xml.WriteAttributeString('tests', [string] ($Report.Measurements.Count + [int] (-not $Report.Complete)))
        $xml.WriteAttributeString('failures', [string] (@($Report.Measurements | Where-Object ExitCode -ne 0).Count + [int] (-not $Report.Complete)))
        foreach ($row in $Report.Measurements) {
            $xml.WriteStartElement('testcase')
            $xml.WriteAttributeString('classname', $Report.TrendSeries)
            $xml.WriteAttributeString('name', "$($row.Engine).$($row.Scenario).Sample$($row.Pair)")
            $xml.WriteAttributeString('time', $row.Seconds.ToString('F6', [Globalization.CultureInfo]::InvariantCulture))
            if ($row.ExitCode -ne 0) { $xml.WriteElementString('failure', "Build command failed with exit $($row.ExitCode).") }
            $xml.WriteEndElement()
        }

        if (-not $Report.Complete) {
            $xml.WriteStartElement('testcase')
            $xml.WriteAttributeString('classname', $Report.TrendSeries)
            $xml.WriteAttributeString('name', 'Benchmark.Completion')
            $xml.WriteElementString('failure', 'Incomplete or invalid performance comparison. ' + ($Report.Problems -join ' '))
            $xml.WriteEndElement()
        }

        $xml.WriteEndElement()
        $xml.WriteEndDocument()
    }
    finally { $xml.Dispose() }
}

function Assert-MatchingOutputs($Expected, $Actual) {
    if (@(Compare-Object @($Expected.Files) @($Actual.Files)).Count -ne 0 -or
        @(Compare-Object @($Expected.Assemblies) @($Actual.Assemblies)).Count -ne 0 -or
        $Expected.Marker -cne $Actual.Marker) {
        throw 'The engines produced different output inventories, assembly identities, or workload markers.'
    }
}

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ($SelfTest) {
    $scratch = Join-Path $repo "artifacts\buildxl-perf-selftest\$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $scratch | Out-Null
    try {
        $stats = Get-PerfStatistics @(30, 10, 20)
        if ($stats.MedianSeconds -ne 20 -or $stats.MinSeconds -ne 10 -or $stats.MaxSeconds -ne 30) { throw 'MedianAndObservedRange failed.' }
        if ((Get-PerfStatistics @(10, 40, 20, 30)).MedianSeconds -ne 25) { throw 'EvenSampleMedian failed.' }
        $rejected = $false
        try { Get-PerfStatistics @(0, 1, 2) | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'InvalidTimingRejected failed.' }
        $expected = [pscustomobject]@{ Files = @('a.dll', 'b.dll'); Assemblies = @('a, Version=1.0.0.0'); Marker = 'baseline' }
        Assert-MatchingOutputs $expected $expected
        $rejected = $false
        try { Assert-MatchingOutputs $expected ([pscustomobject]@{ Files = @('a.dll'); Assemblies = $expected.Assemblies; Marker = 'baseline' }) } catch { $rejected = $true }
        if (-not $rejected) { throw 'MissingOutputRejected failed.' }
        $measurements = @(
            foreach ($pair in 1..3) {
                foreach ($scenario in @('Cold', 'Changed', 'NoOp')) {
                    foreach ($engine in @('MSBuild', 'BuildXL')) {
                        [pscustomobject]@{ Pair = $pair; Engine = $engine; Scenario = $scenario; Seconds = if ($engine -eq 'MSBuild') { 10.0 } else { 20.0 }; ExitCode = 0; TotalProcessPips = $null; CacheHits = $null; CacheMisses = $null }
                    }
                }
            }
        )
        $report = [ordered]@{ Complete = $true; Samples = 3; Source = @{ Revision = 'test-revision' }; Measurements = $measurements; Summary = @(); Problems = @(); TrendSeries = 'TestFx.BuildPerformance.Trx.Debug.net10.Jobs8.FullHashing.v1' }
        Write-PerfReport $report $scratch
        [xml] $junit = Get-Content -LiteralPath (Join-Path $scratch 'benchmark.junit.xml') -Raw
        $saved = Get-Content -LiteralPath (Join-Path $scratch 'results.json') -Raw | ConvertFrom-Json
        if ($junit.testsuite.tests -ne 18 -or @($junit.testsuite.testcase).Count -ne 18 -or $saved.Summary.Count -ne 3 -or $saved.Summary[0].BuildXLSavingsPercent -ne -100) { throw 'CompleteBenchmarkExport failed.' }
        $report.Complete = $false
        $report.Problems = @('Missing sample <must fail>.')
        Write-PerfReport $report $scratch
        [xml] $partial = Get-Content -LiteralPath (Join-Path $scratch 'benchmark.junit.xml') -Raw
        if ($partial.testsuite.failures -ne 1 -or $partial.testsuite.testcase[-1].name -ne 'Benchmark.Completion' -or
            -not $partial.testsuite.testcase[-1].failure.Contains('Missing sample <must fail>.')) { throw 'IncompleteRunExportFails failed.' }
        Write-Host 'PASS MedianAndObservedRange, EvenSampleMedian, InvalidTimingRejected, MissingOutputRejected, CompleteBenchmarkExport, IncompleteRunExportFails'
    }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force }
    exit 0
}

if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {
    throw 'BuildXL performance comparisons require Windows x64.'
}

$runId = [Guid]::NewGuid().ToString('N')
$ResultsDirectory = if ($ResultsDirectory) { [IO.Path]::GetFullPath($ResultsDirectory) } else { Join-Path $repo "artifacts\buildxl-perf\$runId" }
if (Test-Path -LiteralPath $ResultsDirectory) { throw 'ResultsDirectory must be new so previous measurements cannot be mistaken for this run.' }
New-Item -ItemType Directory -Path $ResultsDirectory | Out-Null
$fixtures = Join-Path $repo "artifacts\buildxl-perf-fixtures\$runId"
New-Item -ItemType Directory -Path $fixtures | Out-Null
$marker = Join-Path $fixtures 'BuildXLPerfMarker.g.cs'
$targets = Join-Path $fixtures 'benchmark.targets'
$escapedMarker = [Security.SecurityElement]::Escape($marker)
@'
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' == 'Microsoft.Testing.Platform' and '$(TargetFramework)' != ''">
    <Compile Include="__MARKER__" />
  </ItemGroup>
</Project>
'@.Replace('__MARKER__', $escapedMarker) | Set-Content -LiteralPath $targets -Encoding utf8

function Set-WorkloadMarker([string] $Value) {
    [IO.File]::WriteAllText($marker, "// <auto-generated/>$([Environment]::NewLine)[assembly: System.Reflection.AssemblyMetadata(`"TestFx.BuildXL.Perf`", `"$Value`")]$([Environment]::NewLine)", [Text.UTF8Encoding]::new($true))
}

function Get-SourceIdentity {
    $revision = & git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the benchmark source revision.' }
    $diff = & git -C $repo diff --binary HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify tracked source changes.' }
    $untracked = @(& git -C $repo ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify untracked source changes.' }
    $hashes = @($untracked | ForEach-Object { "$_|$((Get-FileHash -LiteralPath (Join-Path $repo $_) -Algorithm SHA256).Hash)" })
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($diff -join "`n") + ($hashes -join "`n"))))
    return [pscustomobject]@{ Revision = $revision.Trim(); PatchSha256 = $hash }
}

$source = Get-SourceIdentity
$policy = if ($NoJournal) { 'FullHashing' } else { 'Journal' }
$report = [ordered]@{
    SchemaVersion = 1; Complete = $false; CreatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Source = $source; Samples = $Samples; Jobs = $Jobs; Configuration = 'Debug'; TargetFramework = 'net10.0'
    Project = 'src\Platform\Microsoft.Testing.Extensions.TrxReport\Microsoft.Testing.Extensions.TrxReport.csproj'
    TrendSeries = "TestFx.BuildPerformance.Trx.Debug.net10.Jobs$Jobs.$policy.v1"
    Host = [ordered]@{ OS = [Environment]::OSVersion.Version.ToString(); Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); Processors = [Environment]::ProcessorCount; PowerShell = $PSVersionTable.PSVersion.ToString(); Image = $env:ImageVersion }
    Build = [ordered]@{ Id = $env:BUILD_BUILDID; Branch = $env:BUILD_SOURCEBRANCH; Reason = $env:BUILD_REASON; JobAttempt = $env:SYSTEM_JOBATTEMPT }
    Toolchain = $null; Commands = @(); Measurements = @(); Summary = @(); Problems = @()
}
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$driver = Join-Path (Split-Path $PSScriptRoot -Parent) 'buildxl.ps1'

function Invoke-PerfBuild([string] $OutputId, [string] $Engine, [string] $Name, [switch] $Prepare) {
    $directory = Join-Path $ResultsDirectory $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    $arguments = @('-NoLogo', '-NoProfile', '-File', $driver, '-Project', $report.Project, '-TargetFramework', $report.TargetFramework,
        '-Engine', $Engine, '-OutputId', $OutputId, '-Jobs', [string] $Jobs, '-AdditionalTargets', $targets)
    if ($NoJournal) { $arguments += '-NoJournal' }
    if ($Prepare) { $arguments += '-PrepareOnly' }
    $started = [DateTime]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        & $pwsh @arguments *> (Join-Path $directory 'console.log')
        $exitCode = $LASTEXITCODE
    }
    finally { $timer.Stop() }
    $output = Join-Path $repo "artifacts\buildxl-runs\$OutputId"
    $report.Commands += [pscustomobject]@{ Name = $Name; Executable = $pwsh; Arguments = $arguments; ExitCode = $exitCode; Seconds = $timer.Elapsed.TotalSeconds; StartedUtc = $started.ToString('o'); OutputRoot = $output }
    if (Test-Path -LiteralPath (Join-Path $output 'logs')) { Copy-Item -LiteralPath (Join-Path $output 'logs') -Destination $directory -Recurse }
    if (Test-Path -LiteralPath (Join-Path $output 'Out\Logs\MSBuild')) { Copy-Item -LiteralPath (Join-Path $output 'Out\Logs\MSBuild') -Destination (Join-Path $directory 'project-logs') -Recurse }
    if ($exitCode -ne 0) { throw "$Engine $Name failed with exit $exitCode. See $directory\console.log." }
    return [pscustomobject]@{ Seconds = $timer.Elapsed.TotalSeconds; OutputRoot = $output; Started = $started; Evidence = $directory }
}

function Get-PerfOutputs([string] $OutputRoot, [string] $ExpectedMarker) {
    $bin = Join-Path $OutputRoot 'bin'
    $files = @(Get-ChildItem -LiteralPath $bin -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($bin, $_.FullName) } | Sort-Object)
    if ($files.Count -eq 0) { throw 'The build produced no output files.' }
    $assemblies = @()
    foreach ($name in @('Microsoft.Testing.Platform', 'Microsoft.Testing.Extensions.TrxReport.Abstractions', 'Microsoft.Testing.Extensions.TrxReport')) {
        $path = Join-Path $bin "$name\Debug\net10.0\$name.dll"
        $assemblies += [Reflection.AssemblyName]::GetAssemblyName($path).FullName
    }

    $stream = [IO.File]::OpenRead((Join-Path $bin 'Microsoft.Testing.Platform\Debug\net10.0\Microsoft.Testing.Platform.dll'))
    $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $values = @()
        foreach ($handle in $reader.GetAssemblyDefinition().GetCustomAttributes()) {
            $attribute = $reader.GetCustomAttribute($handle)
            if ($attribute.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
            $member = $reader.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]::op_Explicit($attribute.Constructor))
            if ($member.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
            $type = $reader.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]::op_Explicit($member.Parent))
            if ($reader.GetString($type.Name) -cne 'AssemblyMetadataAttribute' -or
                $reader.GetString($type.Namespace) -cne 'System.Reflection') { continue }
            $blob = $reader.GetBlobReader($attribute.Value)
            if ($blob.ReadUInt16() -ne 1) { throw 'Unexpected assembly-metadata encoding.' }
            if ($blob.ReadSerializedString() -ceq 'TestFx.BuildXL.Perf') { $values += $blob.ReadSerializedString() }
        }
        if ($values.Count -ne 1 -or $values[0] -cne $ExpectedMarker) {
            throw 'The built platform did not contain the expected changed-input marker.'
        }
    }
    finally { $pe.Dispose(); $stream.Dispose() }
    return [pscustomobject]@{ Files = $files; Assemblies = $assemblies; Marker = $ExpectedMarker }
}

function Measure-PerfBuild([int] $Pair, [string] $Engine, [string] $Scenario, [string] $OutputId, [string] $ExpectedMarker) {
    Write-Host "Measuring pair $Pair/$Samples $Engine $Scenario."
    $run = Invoke-PerfBuild $OutputId $Engine "$Pair-$Engine-$Scenario"
    $outputs = Get-PerfOutputs $run.OutputRoot $ExpectedMarker
    $row = [ordered]@{ Pair = $Pair; Engine = $Engine; Scenario = $Scenario; Seconds = $run.Seconds; ExitCode = 0; Outputs = $outputs; TotalProcessPips = $null; CacheHits = $null; CacheMisses = $null }
    if ($Engine -eq 'BuildXL') {
        $statsPath = Join-Path $run.Evidence 'logs\BuildXL.stats'
        if ((Get-Item -LiteralPath $statsPath).LastWriteTimeUtc -lt $run.Started) { throw 'BuildXL counters are stale.' }
        $stats = @{}
        foreach ($line in Get-Content -LiteralPath $statsPath) {
            if ($line -cmatch '^(TotalProcessPips|ProcessPipCacheHits|ProcessPipCacheMisses)=(\d+)$') { $stats[$Matches[1]] = [long] $Matches[2] }
        }
        if ($stats.Count -ne 3 -or $stats.TotalProcessPips -ne 6) { throw 'Expected six native MSBuild project instances.' }
        $row.TotalProcessPips = $stats.TotalProcessPips; $row.CacheHits = $stats.ProcessPipCacheHits; $row.CacheMisses = $stats.ProcessPipCacheMisses
        if ($Scenario -eq 'Cold' -and $row.CacheHits -ne 0) { throw 'An output-cold sample reused an action cache.' }
        if ($Scenario -eq 'NoOp' -and ($row.CacheHits -ne 6 -or $row.CacheMisses -ne 0)) { throw 'The no-op BuildXL sample did not reuse all six cached processes.' }
        if ($Scenario -eq 'Changed' -and $row.CacheMisses -lt 1) { throw 'The changed-input sample did not invalidate the action cache.' }
    }
    $report.Measurements += [pscustomobject] $row
}

try {
    Set-WorkloadMarker 'baseline'
    $preparation = Invoke-PerfBuild "perf-$runId-prepare" 'MSBuild' 'preparation' -Prepare
    $report.Toolchain = Get-Content -LiteralPath (Join-Path $preparation.OutputRoot 'toolchain.json') -Raw | ConvertFrom-Json
    for ($pair = 1; $pair -le $Samples; $pair++) {
        $engines = if ($pair % 2) { @('MSBuild', 'BuildXL') } else { @('BuildXL', 'MSBuild') }
        foreach ($engine in $engines) {
            $id = "perf-$runId-$pair-$($engine.ToLowerInvariant())"
            foreach ($path in @("artifacts\buildxl-runs\$id", "artifacts\obj\buildxl-runs\$id")) {
                if (Test-Path -LiteralPath (Join-Path $repo $path)) { throw 'Each output-cold arm must start in a fresh directory.' }
            }
            Set-WorkloadMarker 'baseline'
            Measure-PerfBuild $pair $engine 'Cold' $id 'baseline'
            $null = Invoke-PerfBuild $id $engine "$pair-$engine-prepare-change"
            Set-WorkloadMarker 'changed'
            Measure-PerfBuild $pair $engine 'Changed' $id 'changed'
            Set-WorkloadMarker 'baseline'
            $null = Invoke-PerfBuild $id $engine "$pair-$engine-prepare-noop"
            Measure-PerfBuild $pair $engine 'NoOp' $id 'baseline'
        }
        foreach ($scenario in @('Cold', 'Changed', 'NoOp')) {
            $rows = @($report.Measurements | Where-Object { $_.Pair -eq $pair -and $_.Scenario -eq $scenario })
            if ($rows.Count -ne 2) { throw 'A paired scenario is missing an engine.' }
            Assert-MatchingOutputs $rows[0].Outputs $rows[1].Outputs
        }
        Write-PerfReport $report $ResultsDirectory
    }
    if ($report.Measurements.Count -ne 6 * $Samples) { throw 'The measured sample matrix is incomplete.' }
    $after = Get-SourceIdentity
    if ($after.Revision -cne $source.Revision -or $after.PatchSha256 -cne $source.PatchSha256) { throw 'Repository sources changed during the comparison.' }
    $report.Complete = $true
}
catch {
    $report.Problems += $_.Exception.Message
    throw
}
finally {
    Set-WorkloadMarker 'baseline'
    Write-PerfReport $report $ResultsDirectory
    Write-Host "Build performance evidence: $ResultsDirectory"
}

Get-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.md') | Write-Host
exit 0
