# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [string] $EvidenceRoot,
    [string] $NativeEvidenceDirectory,
    [string] $BuildEvidenceDirectory,
    [string[]] $TestName,
    [string] $PairedEvidenceDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
Import-Module (Join-Path $PSScriptRoot 'dogfood-results.psm1') -Force
$base = if ($EvidenceRoot) { [IO.Path]::GetFullPath($EvidenceRoot) } else { Join-Path ([IO.Path]::GetTempPath()) 'testfx-bazel-dogfood-results' }
$directory = Join-Path $base ([Guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($directory)
$tests = [ordered]@{}

function Add-Test([string] $Name, [scriptblock] $Body) {
    if ($Name -cnotmatch '^[A-Za-z][A-Za-z0-9]+$' -or $tests.Contains($Name)) { throw "Invalid or duplicate test name: $Name" }
    $tests.Add($Name, $Body)
}

function Assert-Equal($Expected, $Actual, [string] $Because = 'value') {
    $equal = if ($null -eq $Expected) { $null -eq $Actual }
    elseif ($Expected -is [bool]) { $Actual -is [bool] -and $Expected -eq $Actual }
    elseif ($Expected -is [string]) { $Actual -is [string] -and $Expected -ceq $Actual }
    else { $null -ne $Actual -and $Expected -eq $Actual }
    if (-not $equal) { throw "Assertion failed ($Because): expected <$Expected>, actual <$Actual>." }
}

function Assert-Throws([scriptblock] $Body, [string] $MessagePart) {
    try { & $Body | Out-Null }
    catch {
        if (-not $_.Exception.Message.Contains($MessagePart)) {
            throw "Assertion failed: expected error containing '$MessagePart', actual '$($_.Exception.Message)'."
        }
        return
    }
    throw "Assertion failed: expected error containing '$MessagePart', but no error was raised."
}

function Write-Fixture([string] $Text, [string] $Extension = 'trx') {
    $path = Join-Path $directory "$([Guid]::NewGuid().ToString('N')).$Extension"
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
    return $path
}

$mixedTrx = @'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="test-a" executionId="exec-a" testName="Passing" outcome="Passed" />
    <UnitTestResult testId="test-b" executionId="exec-b" testName="Failing" outcome="Failed" />
    <UnitTestResult testId="test-c" executionId="exec-c" testName="Skipping" outcome="NotExecuted" />
  </Results>
  <TestDefinitions>
    <UnitTest id="test-a" name="untrusted display name" storage="C:\old\test.dll"><TestMethod className="Example.Tests, Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" name="Passing" codeBase="C:\old\test.dll" /></UnitTest>
    <UnitTest id="test-b"><TestMethod className="Example.Tests" name="Failing" /></UnitTest>
    <UnitTest id="test-c"><TestMethod className="Example.Tests" name="Skipping" /></UnitTest>
  </TestDefinitions>
  <ResultSummary outcome="Failed"><Counters total="3" executed="2" passed="1" failed="1" notExecuted="1" /></ResultSummary>
</TestRun>
'@

$singleTrx = @'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results><UnitTestResult testId="one" testName="One" outcome="Passed" /></Results>
  <TestDefinitions><UnitTest id="one"><TestMethod className="Example.Tests" name="One" /></UnitTest></TestDefinitions>
  <ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary>
</TestRun>
'@

Add-Test TrxParsesPassedFailedSkipped {
    $actual = Read-BazelDogfoodTrx (Write-Fixture $mixedTrx)
    Assert-Equal 3 $actual.Total
    Assert-Equal 2 $actual.Executed
    Assert-Equal 1 $actual.Passed
    Assert-Equal 1 $actual.Failed
    Assert-Equal 1 $actual.Skipped
    Assert-Equal 0 $actual.Other
    Assert-Equal 3 $actual.Cases.Count
    Assert-Equal '["Example.Tests","Passing","Passing"]' $actual.Cases[0].Key
    Assert-Equal 'Passed' $actual.Cases[0].Outcome
    Assert-Equal '["Example.Tests","Failing","Failing"]' $actual.Cases[1].Key
    Assert-Equal 'Failed' $actual.Cases[1].Outcome
    Assert-Equal 'NotExecuted' $actual.Cases[2].Outcome
    # Independent SHA-256 oracle from the literal sorted selection JSON,
    # calculated before adding this assertion, not by the module under test.
    Assert-Equal '890365a80a92b8a3a8ae7b3a58c13b27687e94bd3b3f74300d1dfe5fd9a72995' $actual.SelectionHash
}

Add-Test TrxAcceptsSingletonAndPrefixedNamespace {
    $actual = Read-BazelDogfoodTrx (Write-Fixture $singleTrx)
    Assert-Equal 1 $actual.Total
    Assert-Equal 1 $actual.Executed
    Assert-Equal 1 $actual.Passed
    Assert-Equal 0 $actual.Failed
    Assert-Equal 0 $actual.Skipped
    Assert-Equal 0 $actual.Other
    Assert-Equal 1 $actual.Cases.Count
    Assert-Equal '["Example.Tests","One","One"]' $actual.Cases[0].Key
    foreach ($year in '2010', '2006') {
        $text = $mixedTrx.Replace('TeamTest/2010', "TeamTest/$year").Replace('xmlns=', 'xmlns:t=')
        $text = $text -replace '<(/?)(TestRun|Results|UnitTestResult|TestDefinitions|UnitTest|TestMethod|ResultSummary|Counters)(?=[\s>/])', '<$1t:$2'
        $actual = Read-BazelDogfoodTrx (Write-Fixture $text)
        Assert-Equal 3 $actual.Total
        Assert-Equal '890365a80a92b8a3a8ae7b3a58c13b27687e94bd3b3f74300d1dfe5fd9a72995' $actual.SelectionHash
    }
}

Add-Test TrxPreservesEscapedDisplayNames {
    $text = $singleTrx.Replace('testName="One"', 'testName="Data(&quot;a|b&quot;,&#10;line)"')
    $actual = Read-BazelDogfoodTrx (Write-Fixture $text)
    Assert-Equal '["Example.Tests","One","Data(\"a|b\",\nline)"]' $actual.Cases[0].Key
    Assert-Equal 1 $actual.Passed
}

Add-Test TrxRejectsZeroTests {
    $text = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results/><TestDefinitions/><ResultSummary><Counters total="0" executed="0" passed="0" failed="0" notExecuted="0"/></ResultSummary></TestRun>'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $text) } 'zero tests'
}

Add-Test TrxRejectsMalformedXml {
    foreach ($text in @('', '<TestRun>', $mixedTrx.Replace('</Results>', '</Wrong>'))) {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $text) } 'Invalid TRX XML'
    }
}

Add-Test TrxRejectsExternalEntities {
    $text = '<!DOCTYPE TestRun [<!ENTITY external SYSTEM "file:///unread-secret">]>' + $mixedTrx
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $text) } 'Invalid TRX XML'
}

Add-Test TrxRequiresNamespaceAndCounters {
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('http://microsoft.com/schemas/VisualStudio/TeamTest/2010', 'urn:other'))) } 'namespace'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx -replace '<Counters [^>]*/>', '')) } 'exactly one Counters'
    $duplicate = $mixedTrx.Replace('<ResultSummary outcome="Failed">', '<ResultSummary outcome="Failed"><Counters/>')
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $duplicate) } 'exactly one Counters'
    foreach ($attribute in 'total="3"', 'executed="2"', 'passed="1"', 'failed="1"', 'notExecuted="1"') {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace($attribute, ''))) } 'Missing TRX counter'
    }
}

Add-Test TrxRejectsInvalidCounters {
    foreach ($value in '-1', '1.5', 'many', '9223372036854775808', ' 3') {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('total="3"', "total=`"$value`""))) } 'Invalid nonnegative integer'
    }
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('total="3"', 'total="3" mystery="1"'))) } 'Unexpected TRX counter'
}

Add-Test TrxRejectsInconsistentCounters {
    foreach ($text in @(
        $mixedTrx.Replace('total="3"', 'total="4"'),
        $mixedTrx.Replace('executed="2"', 'executed="3"'),
        $mixedTrx.Replace('executed="2"', 'executed="1"'),
        $mixedTrx.Replace('passed="1" failed="1"', 'passed="0" failed="2"'),
        ($mixedTrx -replace '<UnitTestResult testId="test-c"[^>]*/>', ''))) {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $text) } 'Inconsistent TRX'
    }
}

Add-Test TrxRequiresDefinitionsAndIdentity {
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('<TestDefinitions>', '<Other>').Replace('</TestDefinitions>', '</Other>'))) } 'exactly one TestDefinitions'
    foreach ($text in @(
        $mixedTrx.Replace('id="test-a"', 'id=""'),
        $mixedTrx.Replace('className="Example.Tests"', 'className=""'),
        $mixedTrx.Replace('name="Skipping"', 'name=""'),
        $mixedTrx.Replace('<TestMethod className="Example.Tests" name="Failing" />', ''))) {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $text) } 'Missing TRX test definition identity'
    }
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('testName="Passing"', 'testName=""'))) } 'Missing TRX result identity'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('testId="test-a"', 'testId=""'))) } 'Missing TRX result identity'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('testId="test-a"', 'testId="unknown"'))) } 'Unresolved TRX definition'
}

Add-Test TrxRejectsDuplicateDefinitionIds {
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('id="test-b"', 'id="test-a"'))) } 'Duplicate TRX definition id'
}

Add-Test TrxRejectsAmbiguousResultStructure {
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($singleTrx -replace '<Results>.*</Results>', '')) } 'exactly one Results'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('<Results>', '<Results/><Results>'))) } 'exactly one Results'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('<TestDefinitions>', '<TestDefinitions/><TestDefinitions>'))) } 'exactly one TestDefinitions'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('<UnitTest ', '<AggregateTest ').Replace('</UnitTest>', '</AggregateTest>'))) } 'Unexpected TRX test definition'
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('<UnitTestResult ', '<AggregateResult '))) } 'Unexpected or nested TRX result'
    $nested = $singleTrx.Replace('outcome="Passed" />', 'outcome="Passed"><InnerResults><UnitTestResult testId="one" testName="One" outcome="Passed"/></InnerResults></UnitTestResult>')
    Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture $nested) } 'Unexpected or nested TRX result'
}

Add-Test TrxPreservesDuplicateCanonicalIdentities {
    $text = $mixedTrx.Replace('Failing', 'Passing')
    $actual = Read-BazelDogfoodTrx (Write-Fixture $text)
    Assert-Equal 3 $actual.Cases.Count
    Assert-Equal '["Example.Tests","Passing","Passing"]' $actual.Cases[0].Key
    Assert-Equal '["Example.Tests","Passing","Passing"]' $actual.Cases[1].Key
    Assert-Equal 'Failed' $actual.Cases[1].Outcome
    $single = $text -replace '<UnitTestResult testId="test-b"[^>]*/>', ''
    $single = $single.Replace('total="3" executed="2" passed="1" failed="1"', 'total="2" executed="1" passed="1" failed="0"')
    $reduced = Read-BazelDogfoodTrx (Write-Fixture $single)
    Assert-Equal $false ($actual.SelectionHash -ceq $reduced.SelectionHash) 'multiset multiplicity'
}

Add-Test TrxHashIgnoresGuidsPathsOutcomesAndOrder {
    $original = Read-BazelDogfoodTrx (Write-Fixture $mixedTrx)
    $text = $mixedTrx.Replace('Version=1.0.0.0', 'Version=99.0.0.0').Replace('C:\old', 'D:\different')
    foreach ($id in 'test-a', 'test-b', 'test-c', 'exec-a', 'exec-b', 'exec-c') { $text = $text.Replace($id, [Guid]::NewGuid().ToString()) }
    $text = $text.Replace('testName="Passing" outcome="Passed"', 'testName="Passing" outcome="Failed"')
    $text = $text.Replace('testName="Failing" outcome="Failed"', 'testName="Failing" outcome="Passed"')
    $xml = [xml] $text
    [void] $xml.TestRun.Results.PrependChild($xml.TestRun.Results.LastChild)
    $changed = Read-BazelDogfoodTrx (Write-Fixture $xml.OuterXml)
    Assert-Equal $original.SelectionHash $changed.SelectionHash
    Assert-Equal 'NotExecuted' $changed.Cases[0].Outcome
    Assert-Equal 'Failed' $changed.Cases[1].Outcome
    Assert-Equal 'Passed' $changed.Cases[2].Outcome
}

Add-Test TrxHashDistinguishesSelectionAndTupleBoundaries {
    $original = Read-BazelDogfoodTrx (Write-Fixture $mixedTrx)
    foreach ($text in @(
        $mixedTrx.Replace('testName="Passing"', 'testName="Passing row two"'),
        $mixedTrx.Replace('name="Passing" codeBase', 'name="OtherMethod" codeBase'),
        $mixedTrx.Replace('className="Example.Tests,', 'className="Other.Tests,'))) {
        $changed = Read-BazelDogfoodTrx (Write-Fixture $text)
        Assert-Equal $false ($original.SelectionHash -ceq $changed.SelectionHash) 'selection changes'
    }
    $left = Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('className="Example.Tests"', 'className="Example|Tests"').Replace('name="One"', 'name="A|B"')))
    $right = Read-BazelDogfoodTrx (Write-Fixture ($singleTrx.Replace('className="Example.Tests"', 'className="Example|Tests|A"').Replace('name="One"', 'name="B"')))
    Assert-Equal '["Example|Tests","A|B","One"]' $left.Cases[0].Key
    Assert-Equal '["Example|Tests|A","B","One"]' $right.Cases[0].Key
    Assert-Equal $false ($left.SelectionHash -ceq $right.SelectionHash) 'tuple boundaries'
}

Add-Test TrxHashUsesOrdinalOrdering {
    $text = $mixedTrx.Replace('className="Example.Tests,', 'className="Z.Tests,').Replace('className="Example.Tests"', 'className="a.Tests"')
    $previous = [Globalization.CultureInfo]::CurrentCulture
    try {
        foreach ($culture in 'en-US', 'tr-TR') {
            [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo($culture)
            $actual = Read-BazelDogfoodTrx (Write-Fixture $text)
            # Independently hashed literal order: Z.Tests, a.Tests/Failing, a.Tests/Skipping.
            Assert-Equal 'f20e9ee198bf8981c06ac7b430479881b9af978e87cded67b3958a2d47fb83ea' $actual.SelectionHash
        }
    }
    finally { [Globalization.CultureInfo]::CurrentCulture = $previous }
}

Add-Test TrxClassifiesKnownOtherOutcomes {
    $outcomes = @{
        Error = 'error'; Timeout = 'timeout'; Aborted = 'aborted'; Inconclusive = 'inconclusive'
        PassedButRunAborted = 'passedButRunAborted'; NotRunnable = 'notRunnable'; Disconnected = 'disconnected'
        Warning = 'warning'; Completed = 'completed'; InProgress = 'inProgress'; Pending = 'pending'
    }
    foreach ($entry in $outcomes.GetEnumerator()) {
        $text = $mixedTrx.Replace('outcome="Failed" />', "outcome=`"$($entry.Key)`" />")
        $text = $text.Replace('failed="1"', "failed=`"0`" $($entry.Value)=`"1`"")
        $actual = Read-BazelDogfoodTrx (Write-Fixture $text)
        Assert-Equal 1 $actual.Other
        Assert-Equal 0 $actual.Failed
        Assert-Equal $entry.Key $actual.Cases[1].Outcome
    }
}

Add-Test TrxRejectsUnknownOutcome {
    foreach ($outcome in 'Skipped', 'Mystery', 'passed', '') {
        Assert-Throws { Read-BazelDogfoodTrx (Write-Fixture ($mixedTrx.Replace('outcome="NotExecuted"', "outcome=`"$outcome`""))) } 'Unclassified TRX outcome'
    }
}

function New-Bep([string] $Cache = 'Executed', [string[]] $Labels = @('//:one'), [string] $Command = 'test') {
    $events = [Collections.Generic.List[object]]::new()
    $events.Add(@{ id = @{ started = @{} }; started = @{ command = $Command; startTime = '2026-11-04T10:00:00Z' } })
    foreach ($label in $Labels) {
        $result = @{ status = 'PASSED'; testAttemptDuration = '1.250s'; testAttemptDurationMillis = '1250' }
        $summary = @{ overallStatus = 'PASSED'; totalRunCount = 1; runCount = 1; attemptCount = 1; totalRunDuration = '1.250s'; totalRunDurationMillis = '1250' }
        switch ($Cache) {
            'Local' { $result.cachedLocally = $true; $summary.totalNumCached = 1 }
            'Disk' { $result.executionInfo = @{ strategy = 'disk cache hit'; cachedRemotely = $true }; $summary.totalNumCached = 1 }
            'Remote' { $result.executionInfo = @{ strategy = 'remote'; cachedRemotely = $true }; $summary.totalNumCached = 1 }
            default { $result.executionInfo = @{ strategy = 'local' } }
        }
        $events.Add(@{
            id = @{ testResult = @{ label = $label; configuration = @{ id = 'configuration-a' }; run = 1; shard = 1; attempt = 1 } }
            testResult = $result
        })
        $events.Add(@{
            id = @{ testSummary = @{ label = $label; configuration = @{ id = 'configuration-a' } } }
            testSummary = $summary
        })
    }
    $events.Add(@{ id = @{ buildFinished = @{} }; finished = @{ exitCode = @{ name = 'SUCCESS' }; finishTime = '2026-11-04T10:00:03Z' } })
    $events.Add(@{
        id = @{ buildMetrics = @{} }
        lastMessage = $true
        buildMetrics = @{
            timingMetrics = @{ wallTimeInMs = '1500' }
            actionSummary = @{
                actionsCreated = '8'; actionsExecuted = '5'
                runnerCount = @(@{ name = 'total'; count = 5 }, @{ name = 'local'; count = 4 }, @{ name = 'internal'; count = 2 })
                actionCacheStatistics = @{ hits = 7; misses = 5 }
            }
        }
    })
    return $events.ToArray()
}

function Write-Bep($Events) {
    return Write-Fixture (($Events | ForEach-Object { ConvertTo-Json -InputObject $_ -Depth 30 -Compress }) -join "`n") 'jsonl'
}

Add-Test BepClassifiesExecutedTests {
    foreach ($strategy in 'local', 'remote') {
        $events = New-Bep
        $events[1].testResult.executionInfo.strategy = $strategy
        $events[1].testResult.statusDetails = 'Display text says cached, but is not cache evidence.'
        $actual = Read-BazelDogfoodEvents (Write-Bep $events) -ExpectedTargets '//:one'
        Assert-Equal $true $actual.Completed
        Assert-Equal $true $actual.Success
        Assert-Equal 0 $actual.ExitCode
        Assert-Equal 1 $actual.Targets.Count
        Assert-Equal '//:one' $actual.Targets[0].Label
        Assert-Equal 'PASSED' $actual.Targets[0].Status
        Assert-Equal $false $actual.Targets[0].Cached 'actual execution, including remote execution'
        Assert-Equal 1 $actual.Targets[0].RunCount
        Assert-Equal 1.25 $actual.Targets[0].DurationSeconds
        Assert-Equal 1.5 $actual.WallSeconds 'reported metrics, not three-second timestamp span'
    }
}

Add-Test BepClassifiesLocallyCachedTests {
    $actual = Read-BazelDogfoodEvents (Write-Bep (New-Bep -Cache Local)) -ExpectedTargets '//:one'
    Assert-Equal $true $actual.Success
    Assert-Equal $true $actual.Targets[0].Cached
    Assert-Equal 1.25 $actual.Targets[0].DurationSeconds 'historical cached attempt duration'
    Assert-Equal 1 $actual.Targets[0].RunCount
}

Add-Test BepClassifiesDiskAndRemoteCachedTests {
    foreach ($cache in 'Disk', 'Remote') {
        $actual = Read-BazelDogfoodEvents (Write-Bep (New-Bep -Cache $cache)) -ExpectedTargets '//:one'
        Assert-Equal $true $actual.Success
        Assert-Equal $true $actual.Targets[0].Cached $cache
        Assert-Equal 1.25 $actual.Targets[0].DurationSeconds
    }
}

Add-Test BepRejectsMissingCacheEvidence {
    $events = New-Bep
    $events[1].testResult.Remove('executionInfo')
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Missing BEP execution/cache evidence'
    $events[1].testResult.cachedLocally = $false
    $events[1].testResult.statusDetails = 'Test executed, no cache hit'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Missing BEP execution/cache evidence'
}

Add-Test BepRejectsContradictoryCacheEvidence {
    foreach ($cache in 'Local', 'Disk', 'Remote') {
        $events = New-Bep -Cache $cache
        $events[2].testSummary.Remove('totalNumCached')
        Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP cache evidence'
    }
    $events = New-Bep
    $events[2].testSummary.totalNumCached = 1
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP cache evidence'
    $events[1].testResult.cachedLocally = 'false'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid BEP boolean'
}

Add-Test BepRejectsMissingExpectedTarget {
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (New-Bep)) -ExpectedTargets '//:one', '//:missing' } 'Missing expected test target'
}

Add-Test BepRejectsExtraTarget {
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (New-Bep -Labels '//:one', '//:two')) -ExpectedTargets '//:one' } 'Unexpected test target'
}

Add-Test BepRejectsDuplicateResultOrSummary {
    foreach ($index in 1, 2) {
        $events = New-Bep
        $duplicate = @($events[0..3]) + @($events[$index], $events[4])
        Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $duplicate) } 'Duplicate BEP'
    }
}

Add-Test BepRejectsMultipleConfigurations {
    $events = New-Bep
    $events[2].id.testSummary.configuration.id = 'configuration-b'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Mismatched BEP configuration'
    $events = New-Bep
    $extra = (New-Bep)[1]
    $extra.id.testResult.configuration.id = 'configuration-b'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (@($events[0..3]) + @($extra, $events[4]))) } 'Duplicate BEP testResult'
}

Add-Test BepRejectsRetriesRunsAndShards {
    foreach ($name in 'run', 'shard', 'attempt') {
        foreach ($count in 0, 2) {
            $events = New-Bep
            $events[1].id.testResult[$name] = $count
            Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'requires one run, shard and attempt'
        }
    }
    foreach ($name in 'totalRunCount', 'runCount', 'attemptCount', 'shardCount') {
        $events = New-Bep
        $events[2].testSummary[$name] = 2
        $message = if ($name -eq 'shardCount') { 'requires unsharded tests' } else { 'requires one run, shard and attempt' }
        Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } $message
    }
}

Add-Test BepRejectsShardingEvenWithSingleShard {
    $events = New-Bep
    $events[2].testSummary.shardCount = 1
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'requires unsharded tests'
}

Add-Test BepRejectsUnpairedResultOrSummary {
    foreach ($index in 1, 2) {
        $events = New-Bep
        $partial = for ($i = 0; $i -lt $events.Count; $i++) { if ($i -ne $index) { $events[$i] } }
        Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $partial) } 'Unpaired BEP result/summary'
    }
}

Add-Test BepPreservesFailureExit {
    $events = New-Bep
    $events[1].testResult.status = 'FAILED'
    $events[2].testSummary.overallStatus = 'FAILED'
    $events[3].finished.exitCode = @{ name = 'TESTS_FAILED'; code = 3 }
    $actual = Read-BazelDogfoodEvents (Write-Bep $events) -ExpectedTargets '//:one'
    Assert-Equal $true $actual.Completed
    Assert-Equal $false $actual.Success
    Assert-Equal 3 $actual.ExitCode
    Assert-Equal 'FAILED' $actual.Targets[0].Status
    Assert-Equal $false $actual.Targets[0].Cached
    foreach ($status in 'FAILED', 'TIMEOUT', 'INCOMPLETE', 'REMOTE_FAILURE', 'FAILED_TO_BUILD', 'TOOL_HALTED_BEFORE_TESTING', 'FLAKY') {
        $events[1].testResult.status = $status
        $events[2].testSummary.overallStatus = $status
        $events[3].finished.exitCode = @{ name = 'SUCCESS' }
        $actual = Read-BazelDogfoodEvents (Write-Bep $events)
        Assert-Equal 0 $actual.ExitCode 'preserve the reported exit'
        Assert-Equal $false $actual.Success 'nonpassing tests cannot be green even with exit zero'
    }
}

Add-Test BepRejectsContradictoryStatus {
    $events = New-Bep
    $events[2].testSummary.overallStatus = 'FAILED'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP test status'
    $events = New-Bep
    $events[1].testResult.status = 'UNKNOWN'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'unclassified BEP test status'
    $events = New-Bep
    $events[3].finished.overallSuccess = $false
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP overall success'
    $events = New-Bep
    $events[3].finished.exitCode.code = 3
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP exit name/code'
}

Add-Test BepPartialStreamCannotPass {
    $events = New-Bep
    $unfinished = Read-BazelDogfoodEvents (Write-Bep $events[0..2]) -ExpectedTargets '//:one'
    Assert-Equal $false $unfinished.Completed
    Assert-Equal $false $unfinished.Success
    Assert-Equal $null $unfinished.ExitCode
    Assert-Equal $null $unfinished.WallSeconds
    $truncated = Read-BazelDogfoodEvents (Write-Bep $events[0..3]) -ExpectedTargets '//:one'
    Assert-Equal $false $truncated.Completed
    Assert-Equal $false $truncated.Success
    Assert-Equal 0 $truncated.ExitCode
    Assert-Equal 3 $truncated.WallSeconds
    $missingStart = Read-BazelDogfoodEvents (Write-Bep $events[1..4])
    Assert-Equal $false $missingStart.Completed
    Assert-Equal $false $missingStart.Success
}

Add-Test BepAllowsBuildOnly {
    $events = New-Bep -Labels @() -Command build
    $actual = Read-BazelDogfoodEvents (Write-Bep $events)
    Assert-Equal $true $actual.Completed
    Assert-Equal $true $actual.Success
    Assert-Equal 0 $actual.ExitCode
    Assert-Equal 0 $actual.Targets.Count
    Assert-Equal 1.5 $actual.WallSeconds
}

Add-Test BepRejectsZeroTests {
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (New-Bep -Labels @())) } 'zero tests'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (New-Bep)) -ExpectedTargets @() } 'must not be empty'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (New-Bep)) -ExpectedTargets '//:one', '//:one' } 'empty or duplicate label'
}

Add-Test BepRejectsMalformedInput {
    Assert-Throws { Read-BazelDogfoodEvents (Write-Fixture '' 'jsonl') } 'no events'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Fixture '{' 'jsonl') } 'Invalid BEP JSON'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Fixture '[]' 'jsonl') } 'Invalid BEP event object'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Fixture '{"id":{"testResult":{}}}' 'jsonl') } 'Invalid BEP event payload'
    $events = New-Bep
    $events[1].id.testResult.configuration.Remove('id')
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'label/configuration'
    $events = New-Bep
    $events[3].finished.Remove('exitCode')
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Missing BEP build exit code'
    $events = New-Bep -Cache Local
    $events[1].testResult.executionInfo = 'malformed metadata'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid BEP executionInfo'
}

Add-Test BepRejectsAmbiguousEventObjects {
    $events = New-Bep
    $events[0].id.other = @{}
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid BEP event object'
    $events = New-Bep
    $events[0].progress = @{ stdout = 'extra oneof payload' }
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid BEP event payload'
}

Add-Test BepRejectsInvalidMetrics {
    foreach ($duration in '-1s', 'NaN', '1,25s', '1.250') {
        $events = New-Bep
        $events[2].testSummary.totalRunDuration = $duration
        Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid BEP duration'
    }
    $events = New-Bep
    $events[2].testSummary.totalRunDurationMillis = '2000'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP durations'
    $events = New-Bep
    $events[4].buildMetrics.timingMetrics.wallTimeInMs = '-1'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Invalid nonnegative integer'
    $events = New-Bep
    $events[3].finished.finishTime = '2026-11-04T09:59:59Z'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Negative BEP build interval'
}

Add-Test BepPreservesRawActionStatistics {
    $actual = Read-BazelDogfoodEvents (Write-Bep (New-Bep))
    Assert-Equal '8' $actual.Actions.actionsCreated
    Assert-Equal '5' $actual.Actions.actionsExecuted
    Assert-Equal 3 $actual.Actions.runnerCount.Count
    Assert-Equal 5 $actual.Actions.runnerCount[0].count
    Assert-Equal 4 $actual.Actions.runnerCount[1].count
    Assert-Equal 2 $actual.Actions.runnerCount[2].count 'overlapping categories must not be subtracted'
    Assert-Equal 7 $actual.Actions.actionCacheStatistics.hits
    Assert-Equal 5 $actual.Actions.actionCacheStatistics.misses
}

Add-Test BepPreservesUnknownTiming {
    $events = New-Bep
    $events[0].started.Remove('startTime')
    $events[3].finished.Remove('finishTime')
    $events[4].buildMetrics.Remove('timingMetrics')
    $events[4].buildMetrics.Remove('actionSummary')
    foreach ($name in 'testAttemptDuration', 'testAttemptDurationMillis') { $events[1].testResult.Remove($name) }
    foreach ($name in 'totalRunDuration', 'totalRunDurationMillis') { $events[2].testSummary.Remove($name) }
    $actual = Read-BazelDogfoodEvents (Write-Bep $events)
    Assert-Equal $true $actual.Completed
    Assert-Equal $null $actual.WallSeconds
    Assert-Equal $null $actual.Actions
    Assert-Equal $null $actual.Targets[0].DurationSeconds
}

Add-Test BepReadsLegacyAndZeroTimings {
    $events = New-Bep
    $events[0].started.Remove('startTime')
    $events[0].started.startTimeMillis = '1000'
    $events[3].finished.Remove('finishTime')
    $events[3].finished.finishTimeMillis = '3500'
    $events[4].buildMetrics.Remove('timingMetrics')
    $events[1].testResult.Remove('testAttemptDuration')
    $events[2].testSummary.Remove('totalRunDuration')
    $events[1].testResult.cachedLocally = $false
    $events[1].testResult.executionInfo.cachedRemotely = $false
    $events[2].testSummary.totalNumCached = 0
    $events[2].testSummary.shardCount = 0
    $actual = Read-BazelDogfoodEvents (Write-Bep $events)
    Assert-Equal $true $actual.Success
    Assert-Equal $false $actual.Targets[0].Cached
    Assert-Equal 1.25 $actual.Targets[0].DurationSeconds
    Assert-Equal 2.5 $actual.WallSeconds
    $events = New-Bep
    $events[1].testResult.testAttemptDuration = '0s'
    $events[1].testResult.testAttemptDurationMillis = '0'
    $events[2].testSummary.totalRunDuration = '0s'
    $events[2].testSummary.totalRunDurationMillis = '0'
    $events[4].buildMetrics.timingMetrics.wallTimeInMs = '0'
    $actual = Read-BazelDogfoodEvents (Write-Bep $events)
    Assert-Equal 0 $actual.WallSeconds
    Assert-Equal 0 $actual.Targets[0].DurationSeconds
}

Add-Test BepChecksDurationBoundaries {
    $events = New-Bep
    $events[1].testResult.testAttemptDuration = '1.250999999s'
    $events[2].testSummary.totalRunDuration = '1.250999999s'
    $actual = Read-BazelDogfoodEvents (Write-Bep $events)
    Assert-Equal 1.250999999 $actual.Targets[0].DurationSeconds 'sub-millisecond truncation is compatible'
    $events[2].testSummary.totalRunDuration = '1.251s'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP durations'
    $events = New-Bep
    $events[2].testSummary.totalRunDuration = '1.249999999s'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep $events) } 'Contradictory BEP durations'
}

Add-Test BepRejectsDuplicateCompletion {
    $events = New-Bep
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (@($events[0..3]) + @($events[3], $events[4]))) } 'Duplicate BEP finished'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (@($events) + @($events[4]))) } 'after lastMessage'
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (@($events[0]) + @($events))) } 'Duplicate BEP started'
    $extra = (New-Bep)[4]
    $extra.Remove('lastMessage')
    Assert-Throws { Read-BazelDogfoodEvents (Write-Bep (@($events[0..3]) + @($extra, $events[4]))) } 'Duplicate BEP buildMetrics'
}

function New-Timeline {
    return @{ records = @(
        @{ id = 'job'; type = 'Job'; identifier = 'DogfoodJob'; name = 'Dogfood measured'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:00:00Z'; finishTime = '2026-11-04T10:02:00Z' },
        @{ id = 'phase'; type = 'Phase'; parentId = 'job'; name = 'Execution'; state = 'completed'; startTime = '2026-11-04T10:00:00Z'; finishTime = '2026-11-04T10:02:00Z' },
        @{ id = 'actions'; type = 'Task'; parentId = 'phase'; name = 'Cache Bazel actions'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:00:00Z'; finishTime = '2026-11-04T10:00:10Z' },
        @{ id = 'downloads'; type = 'Task'; parentId = 'job'; name = 'Cache Bazel downloads'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:00:10Z'; finishTime = '2026-11-04T10:00:15Z' },
        @{ id = 'packages'; type = 'Task'; parentId = 'job'; name = 'Cache MSBuild packages'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:00:15Z'; finishTime = '2026-11-04T10:00:18Z' },
        @{ id = 'save-actions'; type = 'Task'; parentId = 'phase'; name = 'Post-job: Cache Bazel actions'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:01:40Z'; finishTime = '2026-11-04T10:01:46Z' },
        @{ id = 'save-downloads'; type = 'Task'; parentId = 'job'; name = 'Post-job: Cache Bazel downloads'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:01:46Z'; finishTime = '2026-11-04T10:01:48Z' },
        @{ id = 'save-packages'; type = 'Task'; parentId = 'job'; name = 'Post-job: Cache MSBuild packages'; state = 'completed'; result = 'succeeded'; startTime = '2026-11-04T10:01:48Z'; finishTime = '2026-11-04T10:01:49Z' }
    ) }
}

Add-Test TimelineMeasuresRestoreAndPostJobSave {
    $parsed = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject (New-Timeline) -Depth 10)
    $actual = Get-BazelDogfoodTimeline $parsed -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 120 $actual.JobSeconds
    Assert-Equal 18 $actual.CacheRestoreSeconds
    Assert-Equal 9 $actual.CacheSaveSeconds
    Assert-Equal 6 $actual.CacheTasks.Count
    Assert-Equal 0 $actual.Missing.Count
    Assert-Equal 'Restore' $actual.CacheTasks[0].Kind
    Assert-Equal 'Cache Bazel actions' $actual.CacheTasks[0].Name
    Assert-Equal 'completed' $actual.CacheTasks[0].State
    Assert-Equal 10 $actual.CacheTasks[0].DurationSeconds
    Assert-Equal 'Save' $actual.CacheTasks[3].Kind
    Assert-Equal 'save-actions' $actual.CacheTasks[3].Id
    Assert-Equal 6 $actual.CacheTasks[3].DurationSeconds
    Assert-Equal 1 $actual.CacheTasks[5].DurationSeconds
}

Add-Test TimelineFollowsPhaseHierarchy {
    $timeline = New-Timeline
    $timeline.records += @{ id = 'nested-phase'; type = 'Phase'; parentId = 'phase'; state = 'completed'; startTime = '2026-11-04T10:00:00Z'; finishTime = '2026-11-04T10:02:00Z' }
    $timeline.records[2].parentId = 'nested-phase'
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 18 $actual.CacheRestoreSeconds 'phase spans are not counted'
    Assert-Equal 9 $actual.CacheSaveSeconds
    Assert-Equal 120 $actual.JobSeconds
}

Add-Test TimelineChecksBoundsThroughUntimedPhase {
    $timeline = New-Timeline
    $timeline.records[1].Remove('startTime')
    $timeline.records[1].Remove('finishTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 18 $actual.CacheRestoreSeconds
    $timeline.records[2].startTime = '2026-11-04T09:59:59Z'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'outside parent'
    $timeline.records[2].startTime = '2026-11-04T10:00:00Z'
    $timeline.records[5].finishTime = '2026-11-04T10:02:01Z'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'outside parent'
}

Add-Test TimelineIgnoresOtherJobsAndSpans {
    $timeline = New-Timeline
    $timeline.records[1].name = 'Cache Bazel actions'
    $timeline.records += @{ id = 'other-job'; type = 'Job'; identifier = 'OtherJob'; name = 'Other'; state = 'completed' }
    $timeline.records += @{ id = 'other-cache'; type = 'Task'; parentId = 'other-job'; name = 'Cache Bazel actions'; state = 'completed'; startTime = 'not a selected job timestamp' }
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 18 $actual.CacheRestoreSeconds
    Assert-Equal 6 $actual.CacheTasks.Count
    Assert-Equal 'actions' $actual.CacheTasks[0].Id
}

Add-Test TimelineRequiresExactJob {
    $timeline = New-Timeline
    $byName = Get-BazelDogfoodTimeline $timeline -JobName 'Dogfood measured'
    Assert-Equal 120 $byName.JobSeconds
    $missing = Get-BazelDogfoodTimeline $timeline -JobName Dogfood
    Assert-Equal $false $missing.Complete
    Assert-Equal $null $missing.JobSeconds
    Assert-Equal $null $missing.CacheRestoreSeconds
    Assert-Equal 7 $missing.Missing.Count
    Assert-Equal 'Job:Dogfood' $missing.Missing[0]
    $timeline.records += @{ id = 'display-collision'; type = 'Job'; name = 'DogfoodJob'; identifier = 'DifferentJob'; state = 'pending' }
    Assert-Equal 120 (Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob).JobSeconds 'identifier takes priority over display name'
    $empty = Get-BazelDogfoodTimeline @{ records = @() } -JobName DogfoodJob
    Assert-Equal $false $empty.Complete
    Assert-Equal 'Job:DogfoodJob' $empty.Missing[0]
    $wrongCase = Get-BazelDogfoodTimeline $timeline -JobName dogfoodjob
    Assert-Equal $false $wrongCase.Complete 'job identifiers are exact, not culture-sensitive'
}

Add-Test TimelineMatchesJobIdBeforeNames {
    $timeline = New-Timeline
    $jobId = 'c275e2ac-8845-4c27-a2f1-8c6b552333bb'
    $timeline.records[0].id = $jobId
    foreach ($record in $timeline.records) {
        if ($record.ContainsKey('parentId') -and $record.parentId -ceq 'job') { $record.parentId = $jobId }
    }
    $timeline.records += @{ id = 'other-job'; type = 'Job'; identifier = $jobId; name = $jobId; state = 'pending' }
    foreach ($lookup in $jobId, $jobId.ToUpperInvariant()) {
        $actual = Get-BazelDogfoodTimeline $timeline -JobName $lookup
        Assert-Equal $true $actual.Complete 'SYSTEM_JOBID must select the actual Job, not a colliding display identifier'
        Assert-Equal 120 $actual.JobSeconds
        Assert-Equal 18 $actual.CacheRestoreSeconds
        Assert-Equal 9 $actual.CacheSaveSeconds
        Assert-Equal 'actions' $actual.CacheTasks[0].Id
        Assert-Equal 0 $actual.Missing.Count
    }
}

Add-Test TimelineRejectsAmbiguousJobsAndIds {
    $timeline = New-Timeline
    $timeline.records += @{ id = 'second-job'; type = 'Job'; identifier = 'DogfoodJob'; state = 'completed' }
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Ambiguous timeline Job'
    $timeline = New-Timeline
    $timeline.records += $timeline.records[0]
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Duplicate timeline record id'
    Assert-Throws { Get-BazelDogfoodTimeline @{} -JobName DogfoodJob } 'records array'
    Assert-Throws { Get-BazelDogfoodTimeline @{ records = $null } -JobName DogfoodJob } 'records array'
}

Add-Test TimelinePreservesAbsentCacheTasks {
    $timeline = New-Timeline
    $timeline.records = $timeline.records[0..1]
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete 'cold job with no cache tasks'
    Assert-Equal 120 $actual.JobSeconds
    Assert-Equal $null $actual.CacheRestoreSeconds
    Assert-Equal $null $actual.CacheSaveSeconds
    Assert-Equal 6 $actual.Missing.Count
    foreach ($task in $actual.CacheTasks) {
        Assert-Equal 'missing' $task.State
        Assert-Equal $null $task.DurationSeconds
    }
    $timeline = New-Timeline
    $timeline.records = @($timeline.records[0], $timeline.records[1], $timeline.records[2], $timeline.records[5])
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 10 $actual.CacheRestoreSeconds
    Assert-Equal 6 $actual.CacheSaveSeconds
    Assert-Equal 4 $actual.Missing.Count 'other cache kinds are optional, not zero-duration tasks'
}

Add-Test TimelinePreservesIncompleteJobTimes {
    foreach ($names in @(@('startTime'), @('finishTime'), @('startTime', 'finishTime'))) {
        $timeline = New-Timeline
        foreach ($name in $names) { $timeline.records[0].Remove($name) }
        $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
        Assert-Equal $false $actual.Complete
        Assert-Equal $null $actual.JobSeconds
        Assert-Equal $names.Count $actual.Missing.Count
        foreach ($name in $names) { Assert-Equal $true ($actual.Missing -ccontains "Job:DogfoodJob:$name") }
    }
    $timeline = New-Timeline
    $timeline.records[0].state = 'inProgress'
    $timeline.records[0].Remove('finishTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $false $actual.Complete
    Assert-Equal $true ($actual.Missing -ccontains 'Job:DogfoodJob:completed')
}

Add-Test TimelinePreservesPendingAndFailedJobs {
    $timeline = New-Timeline
    $timeline.records = @($timeline.records[0])
    $timeline.records[0].state = 'pending'
    $timeline.records[0].Remove('startTime')
    $timeline.records[0].Remove('finishTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $false $actual.Complete
    Assert-Equal $null $actual.JobSeconds
    Assert-Equal $true ($actual.Missing -ccontains 'Job:DogfoodJob:completed')
    $timeline = New-Timeline
    $timeline.records[0].result = 'failed'
    $timeline.records[2].result = 'failed'
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete 'measurement completion is not success'
    Assert-Equal 120 $actual.JobSeconds
    Assert-Equal 'failed' $actual.CacheTasks[0].Result
    $timeline.records = @($timeline.records[0])
    $timeline.records[0].finishTime = '2026-11-04T10:00:00Z'
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal $true $actual.Complete
    Assert-Equal 0 $actual.JobSeconds
    Assert-Equal $null $actual.CacheRestoreSeconds
}

Add-Test TimelinePreservesIncompleteTaskTimes {
    foreach ($names in @(@('startTime'), @('finishTime'), @('startTime', 'finishTime'))) {
        $timeline = New-Timeline
        foreach ($name in $names) { $timeline.records[2].Remove($name) }
        $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
        Assert-Equal $false $actual.Complete
        Assert-Equal $null $actual.CacheTasks[0].DurationSeconds
        Assert-Equal $null $actual.CacheRestoreSeconds
        Assert-Equal 9 $actual.CacheSaveSeconds
        Assert-Equal $names.Count $actual.Missing.Count
    }
    $timeline = New-Timeline
    $timeline.records[2].state = 'inProgress'
    $timeline.records[2].Remove('finishTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal 'inProgress' $actual.CacheTasks[0].State
    Assert-Equal $true ($actual.Missing -ccontains 'Restore:Cache Bazel actions:completed')
    $timeline.records[2].Remove('state')
    $timeline.records[2].Remove('startTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal 'unknown' $actual.CacheTasks[0].State
    Assert-Equal $false $actual.Complete
}

Add-Test TimelinePreservesSkippedTasks {
    $timeline = New-Timeline
    $timeline.records[2].result = 'skipped'
    $timeline.records[2].Remove('startTime')
    $timeline.records[2].Remove('finishTime')
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal 'skipped' $actual.CacheTasks[0].State
    Assert-Equal 'skipped' $actual.CacheTasks[0].Result
    Assert-Equal $null $actual.CacheTasks[0].DurationSeconds
    Assert-Equal $null $actual.CacheRestoreSeconds
    Assert-Equal $false $actual.Complete 'unavailable timing is not zero'
    $timeline.records[2].startTime = '2026-11-04T10:00:00Z'
    $timeline.records[2].finishTime = '2026-11-04T10:00:00Z'
    $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
    Assert-Equal 0 $actual.CacheTasks[0].DurationSeconds 'explicit zero-length interval is valid'
    Assert-Equal 8 $actual.CacheRestoreSeconds
    Assert-Equal $true $actual.Complete
}

Add-Test TimelineRejectsNegativeAndContradictoryIntervals {
    foreach ($index in 0, 2) {
        $timeline = New-Timeline
        $timeline.records[$index].finishTime = '2026-11-04T09:59:59Z'
        Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Negative timeline interval'
    }
    $timeline = New-Timeline
    $timeline.records[2].startTime = '2026-11-04T09:59:59Z'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'outside parent'
    $timeline = New-Timeline
    $timeline.records[5].finishTime = '2026-11-04T10:02:01Z'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'outside parent'
    foreach ($state in 'pending', 'inProgress') {
        $timeline = New-Timeline
        $timeline.records[2].state = $state
        Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Contradictory timeline state/timestamps'
    }
}

Add-Test TimelineRejectsInvalidTimestamps {
    foreach ($value in '11/04/2026 10:00', '2026-11-04T10:00:00', 'invalid', '2026-13-04T10:00:00Z') {
        $timeline = New-Timeline
        $timeline.records[2].startTime = $value
        Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Invalid ISO timestamp'
    }
}

Add-Test TimelineRejectsCyclesAndMissingParents {
    $timeline = New-Timeline
    $timeline.records[1].parentId = 'phase'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Cycle in timeline'
    $timeline = New-Timeline
    $timeline.records[2].parentId = 'missing-parent'
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Unresolved timeline task parent'
    $timeline = New-Timeline
    $timeline.records[1].Remove('parentId')
    Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Unresolved timeline task Job ancestor'
}

Add-Test TimelineRejectsDuplicateCacheTasks {
    foreach ($index in 2, 5) {
        $timeline = New-Timeline
        $duplicate = $timeline.records[$index].Clone()
        $duplicate.id = 'duplicate-task'
        $timeline.records += $duplicate
        Assert-Throws { Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob } 'Duplicate timeline cache task'
    }
}

Add-Test TimelineUsesInvariantOffsets {
    $previous = [Globalization.CultureInfo]::CurrentCulture
    try {
        [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
        $timeline = New-Timeline
        $timeline.records[2].startTime = '2026-11-04T11:00:00.250+01:00'
        $timeline.records[2].finishTime = '2026-11-04T10:00:10.750Z'
        $actual = Get-BazelDogfoodTimeline $timeline -JobName DogfoodJob
        Assert-Equal 10.5 $actual.CacheTasks[0].DurationSeconds
        Assert-Equal 18.5 $actual.CacheRestoreSeconds
        Assert-Equal 120 $actual.JobSeconds
    }
    finally { [Globalization.CultureInfo]::CurrentCulture = $previous }
}

if ($NativeEvidenceDirectory) {
    Add-Test NativeTrxFrameworkCounters {
        $actual = Read-BazelDogfoodTrx (Join-Path $NativeEvidenceDirectory 'framework_tests.trx')
        Assert-Equal 1634 $actual.Total
        Assert-Equal 1634 $actual.Executed
        Assert-Equal 1634 $actual.Passed
        Assert-Equal 0 $actual.Failed
        Assert-Equal 0 $actual.Skipped
        Assert-Equal 0 $actual.Other
        Assert-Equal 1634 $actual.Cases.Count
        Assert-Equal '["UnitTestFramework.Tests.ArchitectureConditionAttributeTests","Constructor_SetsCorrectMode","Constructor_SetsCorrectMode"]' $actual.Cases[0].Key
    }
    Add-Test NativeTrxPlatformFailures {
        $actual = Read-BazelDogfoodTrx (Join-Path $NativeEvidenceDirectory 'platform_tests.trx')
        Assert-Equal 3380 $actual.Total
        Assert-Equal 3380 $actual.Executed
        Assert-Equal 3378 $actual.Passed
        Assert-Equal 2 $actual.Failed
        Assert-Equal 0 $actual.Skipped
        Assert-Equal 0 $actual.Other
        Assert-Equal 3380 $actual.Cases.Count
        $failed = @($actual.Cases | Where-Object Outcome -CEQ 'Failed')
        Assert-Equal 2 $failed.Count
        Assert-Equal '["Microsoft.Testing.Platform.UnitTests.NamedPipeServerSecurityTests","CreateServerStream_AnUnauthorizedIdentityCannotConnect","CreateServerStream_AnUnauthorizedIdentityCannotConnect"]' $failed[0].Key
        Assert-Equal '["Microsoft.Testing.Platform.UnitTests.NamedPipeServerSecurityTests","CreateServerStream_AnAppContainerConnectsOnlyWhenItsOwnPackageIsAuthorized","CreateServerStream_AnAppContainerConnectsOnlyWhenItsOwnPackageIsAuthorized"]' $failed[1].Key
    }
    Add-Test NativeTrxAnalyzerCounters {
        $actual = Read-BazelDogfoodTrx (Join-Path $NativeEvidenceDirectory 'analyzer_tests.trx')
        Assert-Equal 2102 $actual.Total
        Assert-Equal 2102 $actual.Executed
        Assert-Equal 2102 $actual.Passed
        Assert-Equal 0 $actual.Failed
        Assert-Equal 0 $actual.Skipped
        Assert-Equal 0 $actual.Other
        Assert-Equal 2102 $actual.Cases.Count
        Assert-Equal '["MSTest.Analyzers.UnitTests.AnalyzerReleaseTrackingTests","EveryDiagnosticAndSuppressionIdIsTracked","EveryDiagnosticAndSuppressionIdIsTracked"]' $actual.Cases[0].Key
    }
    Add-Test NativeTrxServerClientSkips {
        $actual = Read-BazelDogfoodTrx (Join-Path $NativeEvidenceDirectory 'server_client_tests.trx')
        Assert-Equal 313 $actual.Total
        Assert-Equal 305 $actual.Executed
        Assert-Equal 305 $actual.Passed
        Assert-Equal 0 $actual.Failed
        Assert-Equal 8 $actual.Skipped
        Assert-Equal 0 $actual.Other
        Assert-Equal 313 $actual.Cases.Count
        $skipped = @($actual.Cases | Where-Object Outcome -CEQ 'NotExecuted')
        Assert-Equal 8 $skipped.Count
        Assert-Equal $true $skipped[0].Key.Contains('"StartAsyncWhenStoppedProcessHasDescendantConnectAndKeepStandardErrorOpenReportsEarlyExit"')
    }
    Add-Test NativeBepPreservesFullSuiteFailure {
        $labels = @(
            '//:windows_targeting_test', '//:compatibility_reference_newer_package_test', '//:compatibility_reference_direct_tfm_test',
            '//:compatibility_reference_independent_test', '//:compatibility_reference_native_test', '//:windows_asset_selection_test',
            '//:reference_alias_tests', '//:framework_tests', '//:adapter_tests', '//:build_tools_tests', '//:vstest_bridge_tests',
            '//:platform_services_tests', '//:sdk_package_tests', '//:protocol_contract_tests', '//:platform_tests',
            '//:source_generation_tests', '//:msbuild_tests', '//:self_real_tests', '//:extension_tests',
            '//:terminal_contract_tests', '//:server_client_tests', '//:analyzer_tests')
        $actual = Read-BazelDogfoodEvents (Join-Path $NativeEvidenceDirectory 'events.json') -ExpectedTargets $labels
        Assert-Equal $true $actual.Completed
        Assert-Equal $false $actual.Success
        Assert-Equal 3 $actual.ExitCode
        Assert-Equal 22 $actual.Targets.Count
        Assert-Equal 465.935 $actual.WallSeconds
        Assert-Equal '419' $actual.Actions.actionsExecuted
        Assert-Equal 2240 $actual.Actions.actionCacheStatistics.hits
        Assert-Equal 'FAILED' ($actual.Targets | Where-Object Label -CEQ '//:platform_tests').Status
        Assert-Equal 30.469 ($actual.Targets | Where-Object Label -CEQ '//:server_client_tests').DurationSeconds
        foreach ($target in $actual.Targets) { Assert-Equal $false $target.Cached }
    }
}

if ($BuildEvidenceDirectory) {
    Add-Test NativeBepBuildOnlyWarmNoop {
        $actual = Read-BazelDogfoodEvents (Join-Path $BuildEvidenceDirectory 'net10-warm-noop-1.jsonl')
        Assert-Equal $true $actual.Completed
        Assert-Equal $true $actual.Success
        Assert-Equal 0 $actual.ExitCode
        Assert-Equal 0 $actual.Targets.Count
        Assert-Equal 5.574 $actual.WallSeconds
        Assert-Equal '1' $actual.Actions.actionsExecuted
        Assert-Equal 1 $actual.Actions.actionCacheStatistics.hits
    }
    Add-Test NativeBepBuildOnlyDiskRestore {
        $actual = Read-BazelDogfoodEvents (Join-Path $BuildEvidenceDirectory 'net10-disk-cache-restore-1.jsonl')
        Assert-Equal $true $actual.Completed
        Assert-Equal $true $actual.Success
        Assert-Equal 0 $actual.Targets.Count
        Assert-Equal 16.508 $actual.WallSeconds
        Assert-Equal '1517' $actual.Actions.actionsExecuted
        Assert-Equal 1136 ($actual.Actions.runnerCount | Where-Object name -CEQ 'disk cache hit').count
        Assert-Equal 1517 $actual.Actions.actionCacheStatistics.misses
    }
}

$dynamicTimingClass = 'Microsoft.Testing.Platform.UnitTests.DotnetTestDataConsumerTests'
$dynamicTimingMethod = 'GetTestNodeDetails_WithDuplicateSingleOrDefaultProperty_ThrowsInvalidOperationException'
$dynamicTimingClocks = @('11/04/2026 11:36:19 +00:00', '11/04/2026 11:36:20 +00:00', '11/04/2026 11:36:21 +00:00', '11/04/2026 11:36:22 +00:00')
# Independent expected identity: only the four clock spans are canonicalized.
$dynamicTimingCanonicalDisplay = 'GetTestNodeDetails_WithDuplicateSingleOrDefaultProperty_ThrowsInvalidOperationException (Microsoft.Testing.Platform.Extensions.Messages.TimingProperty,TimingProperty { GlobalTiming = TimingInfo { StartTime = 01/01/0001 00:00:00 +00:00, EndTime = 01/01/0001 00:00:00 +00:00, Duration = 00:00:01 }, StepTimings = [] },TimingProperty { GlobalTiming = TimingInfo { StartTime = 01/01/0001 00:00:00 +00:00, EndTime = 01/01/0001 00:00:00 +00:00, Duration = 00:00:01 }, StepTimings = [] })'

function New-DynamicTimingDisplay(
    [string[]] $Clocks = $dynamicTimingClocks,
    [string] $FirstDuration = '00:00:01',
    [string] $SecondDuration = '00:00:01',
    [string] $FirstSteps = '[]',
    [string] $SecondSteps = '[]'
) {
    if ($Clocks.Count -ne 4) { throw 'Fixture requires four clock values.' }
    return "$dynamicTimingMethod (Microsoft.Testing.Platform.Extensions.Messages.TimingProperty,TimingProperty { GlobalTiming = TimingInfo { StartTime = $($Clocks[0]), EndTime = $($Clocks[1]), Duration = $FirstDuration }, StepTimings = $FirstSteps },TimingProperty { GlobalTiming = TimingInfo { StartTime = $($Clocks[2]), EndTime = $($Clocks[3]), Duration = $SecondDuration }, StepTimings = $SecondSteps })"
}

function New-DynamicTimingTrx(
    [string[]] $Names,
    [string[]] $Outcomes,
    [string] $ClassName = $dynamicTimingClass,
    [string] $MethodName = $dynamicTimingMethod
) {
    if (-not $Outcomes) { $Outcomes = @('Passed') * $Names.Count }
    if ($Names.Count -ne $Outcomes.Count) { throw 'Fixture names and outcomes must have equal length.' }
    $results = [Text.StringBuilder]::new()
    $definitions = [Text.StringBuilder]::new()
    for ($i = 0; $i -lt $Names.Count; $i++) {
        [void] $results.Append("<UnitTestResult testId=`"row-$i`" testName=`"$([Security.SecurityElement]::Escape($Names[$i]))`" outcome=`"$($Outcomes[$i])`"/>")
        [void] $definitions.Append("<UnitTest id=`"row-$i`"><TestMethod className=`"$([Security.SecurityElement]::Escape($ClassName))`" name=`"$([Security.SecurityElement]::Escape($MethodName))`"/></UnitTest>")
    }
    $passed = @($Outcomes | Where-Object { $_ -ceq 'Passed' }).Count
    $failed = @($Outcomes | Where-Object { $_ -ceq 'Failed' }).Count
    $skipped = @($Outcomes | Where-Object { $_ -ceq 'NotExecuted' }).Count
    return "<TestRun xmlns=`"http://microsoft.com/schemas/VisualStudio/TeamTest/2010`"><Results>$results</Results><TestDefinitions>$definitions</TestDefinitions><ResultSummary><Counters total=`"$($Names.Count)`" executed=`"$($passed + $failed)`" passed=`"$passed`" failed=`"$failed`" notExecuted=`"$skipped`"/></ResultSummary></TestRun>"
}

Add-Test TrxTimingPolicyDefaultsToExact {
    $name = New-DynamicTimingDisplay
    $path = Write-Fixture (New-DynamicTimingTrx -Names @($name))
    $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $default = Read-BazelDogfoodTrx -Path $path
    $exact = Read-BazelDogfoodTrx -Path $path -IdentityPolicy Exact
    $normalized = Read-BazelDogfoodTrx -Path $path -IdentityPolicy TestFxDynamicTimingV1
    $parts = @(ConvertFrom-Json -InputObject $default.Cases[0].Key)
    Assert-Equal $name $parts[2]
    Assert-Equal $default.SelectionHash $exact.SelectionHash
    Assert-Equal $default.SelectionHash $default.RawSelectionHash
    Assert-Equal $default.SelectionHash $normalized.RawSelectionHash
    Assert-Equal $false ($default.SelectionHash -ceq $normalized.SelectionHash)
    Assert-Equal $dynamicTimingCanonicalDisplay (@(ConvertFrom-Json -InputObject $normalized.Cases[0].Key))[2]
    Assert-Equal $before (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash 'raw TRX remains unchanged'
}

Add-Test TrxTimingPolicyNormalizesOnlyFourClockFields {
    $baseline = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @((New-DynamicTimingDisplay)))) -IdentityPolicy TestFxDynamicTimingV1
    for ($i = 0; $i -lt 4; $i++) {
        $clocks = [string[]] $dynamicTimingClocks.Clone()
        $clocks[$i] = '12/31/2030 23:59:58 -05:00'
        $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @((New-DynamicTimingDisplay -Clocks $clocks)))) -IdentityPolicy TestFxDynamicTimingV1
        $parts = @(ConvertFrom-Json -InputObject $actual.Cases[0].Key)
        Assert-Equal 3 $parts.Count
        Assert-Equal $dynamicTimingClass $parts[0]
        Assert-Equal $dynamicTimingMethod $parts[1]
        Assert-Equal $dynamicTimingCanonicalDisplay $parts[2]
        Assert-Equal $baseline.SelectionHash $actual.SelectionHash "clock position $i"
        Assert-Equal $false ($baseline.RawSelectionHash -ceq $actual.RawSelectionHash) 'raw audit remains clock-sensitive'
        Assert-Equal 1 $actual.Total
        Assert-Equal 'Passed' $actual.Cases[0].Outcome
    }
}

Add-Test TrxTimingPolicyPreservesDurations {
    $baseline = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @((New-DynamicTimingDisplay)))) -IdentityPolicy TestFxDynamicTimingV1
    foreach ($value in '00:00:02.5000000', '1.02:03:04', '-00:00:01') {
        foreach ($position in 0, 1) {
            $name = if ($position -eq 0) { New-DynamicTimingDisplay -FirstDuration $value }
            else { New-DynamicTimingDisplay -SecondDuration $value }
            $expected = $dynamicTimingCanonicalDisplay
            $marker = 'Duration = 00:00:01'
            $index = if ($position -eq 0) { $expected.IndexOf($marker, [StringComparison]::Ordinal) }
            else { $expected.LastIndexOf($marker, [StringComparison]::Ordinal) }
            $expected = $expected.Remove($index, $marker.Length).Insert($index, "Duration = $value")
            $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($name))) -IdentityPolicy TestFxDynamicTimingV1
            Assert-Equal $expected (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2]
            Assert-Equal $false ($baseline.SelectionHash -ceq $actual.SelectionHash) 'each Duration remains selection-significant'
            Assert-Equal $false ($actual.SelectionHash -ceq $actual.RawSelectionHash) 'valid changed durations still normalize only the clocks'
        }
    }
}

Add-Test TrxTimingPolicyRetainsStepTimings {
    $steps = '[prepare: TimingInfo { StartTime = 12/24/2040 17:00:00 +00:00, EndTime = 12/24/2040 17:00:03 +00:00, Duration = 00:00:03 }]'
    foreach ($position in 0, 1) {
        $name = if ($position -eq 0) { New-DynamicTimingDisplay -FirstSteps $steps }
        else { New-DynamicTimingDisplay -SecondSteps $steps }
        $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($name))) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $name (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2] 'nonempty StepTimings stay entirely exact'
        Assert-Equal $actual.RawSelectionHash $actual.SelectionHash
        $changedName = $name.Replace('12/24/2040 17:00:00 +00:00', '12/24/2040 17:00:01 +00:00')
        $changed = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($changedName))) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $changedName (@(ConvertFrom-Json -InputObject $changed.Cases[0].Key))[2]
        Assert-Equal $false ($actual.SelectionHash -ceq $changed.SelectionHash) 'nested StartTime is not an allowed clock'
    }
}

Add-Test TrxTimingPolicyRequiresExactClassMethodAndRow {
    $name = New-DynamicTimingDisplay
    $samples = @(
        @{ Class = "$dynamicTimingClass.Other"; Method = $dynamicTimingMethod; Name = $name },
        @{ Class = $dynamicTimingClass.ToLowerInvariant(); Method = $dynamicTimingMethod; Name = $name },
        @{ Class = $dynamicTimingClass; Method = "$dynamicTimingMethod.Other"; Name = $name },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod.ToLowerInvariant(); Name = $name },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = $name.Replace('Microsoft.Testing.Platform.Extensions.Messages.TimingProperty', 'Other.Namespace.TimingProperty') },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = $name.Replace('Microsoft.Testing.Platform.Extensions.Messages.TimingProperty', 'Microsoft.Testing.Platform.Extensions.Messages.StandardOutputProperty') },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = $name.Replace('TimingProperty {', 'OtherTimingProperty {') },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = $name.Replace('TimingInfo {', 'OtherTimingInfo {') },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = $name.Replace($dynamicTimingMethod, "$dynamicTimingMethod.Other") },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = "prefix $name" },
        @{ Class = $dynamicTimingClass; Method = $dynamicTimingMethod; Name = "$name suffix" }
    )
    foreach ($sample in $samples) {
        $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($sample.Name) -ClassName $sample.Class -MethodName $sample.Method)) -IdentityPolicy TestFxDynamicTimingV1
        $parts = @(ConvertFrom-Json -InputObject $actual.Cases[0].Key)
        Assert-Equal $sample.Class $parts[0]
        Assert-Equal $sample.Method $parts[1]
        Assert-Equal $sample.Name $parts[2]
        Assert-Equal $actual.RawSelectionHash $actual.SelectionHash
        $changedName = $sample.Name.Replace($dynamicTimingClocks[0], '12/31/2030 23:59:58 -05:00')
        $changed = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($changedName) -ClassName $sample.Class -MethodName $sample.Method)) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $false ($actual.SelectionHash -ceq $changed.SelectionHash) 'nearby class, method, or row identities must remain date-sensitive'
    }
}

Add-Test TrxTimingPolicyRetainsUnrecognizedForms {
    $name = New-DynamicTimingDisplay
    $samples = @(
        $name.Replace($dynamicTimingClocks[0], '13/04/2026 11:36:19 +00:00'),
        $name.Replace($dynamicTimingClocks[1], '11/31/2026 11:36:20 +00:00'),
        $name.Replace($dynamicTimingClocks[2], '11/04/2026 25:36:21 +00:00'),
        $name.Replace($dynamicTimingClocks[3], '11/04/2026 11:36:22 +15:00'),
        $name.Replace($dynamicTimingClocks[0], '2026-11-04T11:36:19+00:00'),
        $name.Replace($dynamicTimingClocks[0], '11/04/2026 11:36:19.123 +00:00'),
        $name.Replace($dynamicTimingClocks[0], ''),
        $name.Replace('StartTime =', 'StartTime  ='),
        $name.Replace('Duration = 00:00:01', 'Duration = many'),
        $name.Replace('Duration = 00:00:01', 'Duration = 00:99:01'),
        $name.Replace('TimingInfo { StartTime', 'TimingInfo { Extra = keep-me, StartTime')
    )
    foreach ($sample in $samples) {
        $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($sample))) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $sample (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2]
        Assert-Equal $actual.RawSelectionHash $actual.SelectionHash 'unexpected forms must not be partially normalized'
    }
    $previous = [Globalization.CultureInfo]::CurrentCulture
    try {
        [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
        $clocks = [string[]] $dynamicTimingClocks.Clone()
        $clocks[0] = '12/31/2030 23:59:58 -05:00'
        $valid = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @((New-DynamicTimingDisplay -Clocks $clocks)))) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $dynamicTimingCanonicalDisplay (@(ConvertFrom-Json -InputObject $valid.Cases[0].Key))[2] 'V1 has an invariant format'
        $clocks[0] = '31/12/2030 23:59:58 -05:00'
        $unexpected = New-DynamicTimingDisplay -Clocks $clocks
        $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($unexpected))) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $unexpected (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2] 'do not broaden V1 using current culture'
    }
    finally { [Globalization.CultureInfo]::CurrentCulture = $previous }
}

Add-Test TrxTimingPolicyPreservesOtherDatesAndIdentities {
    $otherDate = 'DateRoundTrip (2026-11-04T11:36:19+00:00, StartTime = 11/04/2026 11:36:19 +00:00, Duration = 00:00:01)'
    $otherRow = "$dynamicTimingMethod (Microsoft.Testing.Platform.Extensions.Messages.StandardOutputProperty,11/04/2026 11:36:19 +00:00,EndTime = 11/04/2026 11:36:20 +00:00)"
    $names = @((New-DynamicTimingDisplay), $otherDate, $otherRow)
    $actual = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names $names)) -IdentityPolicy TestFxDynamicTimingV1
    Assert-Equal 3 $actual.Cases.Count
    Assert-Equal $dynamicTimingCanonicalDisplay (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2]
    Assert-Equal $otherDate (@(ConvertFrom-Json -InputObject $actual.Cases[1].Key))[2]
    Assert-Equal $otherRow (@(ConvertFrom-Json -InputObject $actual.Cases[2].Key))[2]
    foreach ($index in 1, 2) {
        $changedNames = [string[]] $names.Clone()
        $changedNames[$index] = $changedNames[$index].Replace('11:36:19', '11:47:06')
        $changed = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names $changedNames)) -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $false ($actual.SelectionHash -ceq $changed.SelectionHash) 'dates in other rows remain significant'
        Assert-Equal $changedNames[$index] (@(ConvertFrom-Json -InputObject $changed.Cases[$index].Key))[2]
    }
}

Add-Test TrxTimingPolicyPreservesCountersOutcomesAndMultiplicity {
    $clocks = [string[]] $dynamicTimingClocks.Clone()
    $clocks[3] = '12/31/2030 23:59:58 -05:00'
    $names = @((New-DynamicTimingDisplay), (New-DynamicTimingDisplay -Clocks $clocks), 'Unrelated (2026-11-04)')
    $path = Write-Fixture (New-DynamicTimingTrx -Names $names -Outcomes Passed, Failed, NotExecuted)
    $exact = Read-BazelDogfoodTrx -Path $path
    $actual = Read-BazelDogfoodTrx -Path $path -IdentityPolicy TestFxDynamicTimingV1
    Assert-Equal 3 $actual.Total
    Assert-Equal 2 $actual.Executed
    Assert-Equal 1 $actual.Passed
    Assert-Equal 1 $actual.Failed
    Assert-Equal 1 $actual.Skipped
    Assert-Equal 0 $actual.Other
    Assert-Equal 3 $actual.Cases.Count
    Assert-Equal 'Passed' $actual.Cases[0].Outcome
    Assert-Equal 'Failed' $actual.Cases[1].Outcome
    Assert-Equal 'NotExecuted' $actual.Cases[2].Outcome
    Assert-Equal $dynamicTimingCanonicalDisplay (@(ConvertFrom-Json -InputObject $actual.Cases[0].Key))[2]
    Assert-Equal $actual.Cases[0].Key $actual.Cases[1].Key 'clock-only variants form a multiset, not a deduplicated set'
    Assert-Equal $exact.SelectionHash $actual.RawSelectionHash
    $outcomeChange = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names $names -Outcomes Failed, Passed, NotExecuted)) -IdentityPolicy TestFxDynamicTimingV1
    Assert-Equal $actual.SelectionHash $outcomeChange.SelectionHash
    Assert-Equal $actual.RawSelectionHash $outcomeChange.RawSelectionHash
    $reduced = Read-BazelDogfoodTrx -Path (Write-Fixture (New-DynamicTimingTrx -Names @($names[0], $names[2]) -Outcomes Passed, NotExecuted)) -IdentityPolicy TestFxDynamicTimingV1
    Assert-Equal 2 $reduced.Total
    Assert-Equal 2 $reduced.Cases.Count
    Assert-Equal $actual.Cases[0].Key $reduced.Cases[0].Key
    Assert-Equal $false ($actual.SelectionHash -ceq $reduced.SelectionHash) 'duplicate multiplicity remains in the hash'
}

Add-Test TrxTimingPolicyRejectsUnknownPolicy {
    $path = Write-Fixture (New-DynamicTimingTrx -Names @((New-DynamicTimingDisplay)))
    foreach ($policy in 'UnknownPolicy', 'testfxdynamictimingv1', 'Exact ', '') {
        Assert-Throws { Read-BazelDogfoodTrx -Path $path -IdentityPolicy $policy } 'IdentityPolicy'
    }
}

if ($PairedEvidenceDirectory) {
    Add-Test NativeTrxTimingPolicyMatchesPairedSelections {
        $nativePath = Join-Path $PairedEvidenceDirectory 'native-full2/reports/executed/platform_tests.trx'
        $msbuildPath = Join-Path $PairedEvidenceDirectory 'msbuild-full/reports/executed/platform_tests.trx'
        $nativeExact = Read-BazelDogfoodTrx -Path $nativePath
        $msbuildExact = Read-BazelDogfoodTrx -Path $msbuildPath
        Assert-Equal $false ($nativeExact.SelectionHash -ceq $msbuildExact.SelectionHash) 'the real exact-selection mismatch stays visible'
        $native = Read-BazelDogfoodTrx -Path $nativePath -IdentityPolicy TestFxDynamicTimingV1
        $msbuild = Read-BazelDogfoodTrx -Path $msbuildPath -IdentityPolicy TestFxDynamicTimingV1
        Assert-Equal $native.SelectionHash $msbuild.SelectionHash 'only the approved row clocks are ignored'
        foreach ($pair in @(@{ Exact = $nativeExact; Normalized = $native }, @{ Exact = $msbuildExact; Normalized = $msbuild })) {
            $actual = $pair.Normalized
            Assert-Equal 3380 $actual.Total
            Assert-Equal 3380 $actual.Executed
            Assert-Equal 3378 $actual.Passed
            Assert-Equal 2 $actual.Failed
            Assert-Equal 0 $actual.Skipped
            Assert-Equal 0 $actual.Other
            Assert-Equal 3380 $actual.Cases.Count
            Assert-Equal $pair.Exact.SelectionHash $actual.RawSelectionHash
            $changed = @(for ($i = 0; $i -lt 3380; $i++) {
                Assert-Equal $pair.Exact.Cases[$i].Outcome $actual.Cases[$i].Outcome
                if ($pair.Exact.Cases[$i].Key -cne $actual.Cases[$i].Key) { $i }
            })
            Assert-Equal 1 $changed.Count
            $parts = @(ConvertFrom-Json -InputObject $actual.Cases[$changed[0]].Key)
            Assert-Equal $dynamicTimingClass $parts[0]
            Assert-Equal $dynamicTimingMethod $parts[1]
            Assert-Equal $dynamicTimingCanonicalDisplay $parts[2]
            Assert-Equal 'Passed' $actual.Cases[$changed[0]].Outcome
        }
    }
}

$selected = @(if ($TestName) { $TestName } else { $tests.Keys })
if ($selected.Count -eq 0) { throw 'No tests selected.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in $selected) {
    if (-not $tests.Contains($name) -or -not $seen.Add($name)) { throw "Unknown or duplicate selected test: $name" }
}
$results = [Collections.Generic.List[object]]::new()
foreach ($name in $selected) {
    $result = 'Passed'
    $message = ''
    try { & $tests[$name] }
    catch { $result = 'Failed'; $message = $_.Exception.Message }
    $results.Add([pscustomobject]@{ Name = $name; Result = $result; Message = $message })
    Write-Host "${name}: $result $message"
}
$reportPath = Join-Path $directory 'fixture-results.json'
[IO.File]::WriteAllText($reportPath, (ConvertTo-Json -InputObject $results.ToArray() -Depth 8), [Text.UTF8Encoding]::new($false))
# Validate the actual exported artifact, not just the runner's in-memory status.
$report = @(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($reportPath)))
Assert-Equal $selected.Count $report.Count 'exported result count'
$failed = 0
for ($i = 0; $i -lt $report.Count; $i++) {
    Assert-Equal $selected[$i] $report[$i].Name 'exported test identity'
    Assert-Equal $results[$i].Result $report[$i].Result 'exported outcome'
    if ($report[$i].Result -eq 'Failed') { $failed++ }
}
Write-Host "Results: total=$($report.Count), passed=$($report.Count - $failed), failed=$failed, skipped=0. Evidence: $reportPath"
if ($failed -ne 0) { exit 1 }
exit 0
