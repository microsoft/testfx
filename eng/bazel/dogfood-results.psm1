# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Get-DogfoodField($Value, [string] $Name) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [Collections.IDictionary]) { return $Value[$Name] }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $null
}

function ConvertTo-DogfoodCount($Value, [string] $Context) {
    $number = 0L
    if ($null -eq $Value -or [string] $Value -cnotmatch '^(0|[1-9][0-9]*)$' -or
        -not [long]::TryParse([string] $Value, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref] $number)) {
        throw "Invalid nonnegative integer for $Context."
    }
    return $number
}

function Get-DogfoodTypeIdentity([string] $ClassName) {
    # TRX may carry an assembly-qualified class name. Do not put version,
    # culture, key or processor metadata (including generic arguments') in a key.
    $name = [regex]::Replace($ClassName, ',\s*(Version|Culture|PublicKeyToken|ProcessorArchitecture)\s*=[^,\[\]]+', '', 'IgnoreCase')
    $depth = 0
    for ($i = 0; $i -lt $name.Length; $i++) {
        if ($name[$i] -eq '[') { $depth++ }
        elseif ($name[$i] -eq ']') { $depth-- }
        elseif ($name[$i] -eq ',' -and $depth -eq 0) { $name = $name.Substring(0, $i); break }
    }
    $name = $name.Trim()
    if ([string]::IsNullOrWhiteSpace($name)) { throw 'Missing TRX class identity.' }
    return $name
}

function Get-DogfoodSelectionHash([string[]] $Keys) {
    [Array]::Sort($Keys, [StringComparer]::Ordinal)
    $selection = ConvertTo-Json -InputObject $Keys -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($selection))).ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function ConvertTo-DogfoodTrxDisplayName([string] $ClassName, [string] $MethodName, [string] $DisplayName, [string] $IdentityPolicy) {
    $knownClass = 'Microsoft.Testing.Platform.UnitTests.DotnetTestDataConsumerTests'
    $knownMethod = 'GetTestNodeDetails_WithDuplicateSingleOrDefaultProperty_ThrowsInvalidOperationException'
    if ($IdentityPolicy -cne 'TestFxDynamicTimingV1' -or
        -not [StringComparer]::Ordinal.Equals($ClassName, $knownClass) -or
        -not [StringComparer]::Ordinal.Equals($MethodName, $knownMethod)) {
        return $DisplayName
    }

    # This source-bound row checks duplicate property types, not its four UtcNow
    # values. V1 deliberately recognizes only the observed complete shape.
    # In particular, never normalize dates inside nonempty StepTimings or a
    # changed/expanded record representation; those identities stay exact.
    $clock = '[0-9]{2}/[0-9]{2}/[0-9]{4} [0-9]{2}:[0-9]{2}:[0-9]{2} [+-][0-9]{2}:[0-9]{2}'
    $property = 'TimingProperty \{ GlobalTiming = TimingInfo \{ StartTime = (?<clock>' + $clock +
        '), EndTime = (?<clock>' + $clock + '), Duration = (?<duration>-?[0-9:.]+) \}, StepTimings = \[\] \}'
    $prefix = "$knownMethod (Microsoft.Testing.Platform.Extensions.Messages.TimingProperty,"
    $pattern = '\A' + [regex]::Escape($prefix) + $property + ',' + $property + '\)\z'
    $match = [regex]::Match($DisplayName, $pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) { return $DisplayName }
    foreach ($capture in $match.Groups['clock'].Captures) {
        $timestamp = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParseExact($capture.Value, 'MM/dd/yyyy HH:mm:ss zzz',
                [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $timestamp)) {
            return $DisplayName
        }
    }
    foreach ($capture in $match.Groups['duration'].Captures) {
        $duration = [TimeSpan]::Zero
        if (-not [TimeSpan]::TryParseExact($capture.Value, 'c', [Globalization.CultureInfo]::InvariantCulture, [ref] $duration)) {
            return $DisplayName
        }
    }
    # Replace captures from right to left, preserving every other character.
    # A valid clock sentinel cannot collide with an unrecognized exact fallback.
    $normalized = $DisplayName
    $clocks = $match.Groups['clock'].Captures
    for ($i = $clocks.Count - 1; $i -ge 0; $i--) {
        $normalized = $normalized.Remove($clocks[$i].Index, $clocks[$i].Length).Insert($clocks[$i].Index, '01/01/0001 00:00:00 +00:00')
    }
    return $normalized
}

function Read-BazelDogfoodTrx {
    <#
    .SYNOPSIS
    Reads validated TRX counters and a stable multiset of selected test cases.
    .NOTES
    Key is a compact JSON triple [className, methodName, result testName].
    SelectionHash is lowercase SHA-256 of UTF-8 compact JSON of ordinally sorted
    keys, including duplicates. Outcomes, GUIDs and storage paths are not hashed.
    Other counts recognized TRX outcomes other than Passed, Failed, NotExecuted;
    a caller must not interpret Other, Failed or Skipped as passed executions.
    IdentityPolicy defaults to Exact. TestFxDynamicTimingV1 normalizes only the
    four StartTime/EndTime clocks in the known duplicate-TimingProperty row of
    DotnetTestDataConsumerTests to 01/01/0001 00:00:00 +00:00. All other fields
    remain intact; unknown shapes stay exact. RawSelectionHash always audits
    the original Exact identities. Input TRXs are never rewritten. Callers must
    include the identity policy in their workload/comparison scope signature.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [ValidateSet('Exact', 'TestFxDynamicTimingV1', IgnoreCase = $false)]
        [string] $IdentityPolicy = 'Exact'
    )

    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = $null
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try {
        $reader = [Xml.XmlReader]::Create([IO.Path]::GetFullPath($Path), $settings)
        $document.Load($reader)
    }
    catch { throw "Invalid TRX XML '$Path': $($_.Exception.Message)" }
    finally { if ($null -ne $reader) { $reader.Dispose() } }

    $root = $document.DocumentElement
    if ($root.LocalName -cne 'TestRun' -or $root.NamespaceURI -cnotin @(
        'http://microsoft.com/schemas/VisualStudio/TeamTest/2010',
        'http://microsoft.com/schemas/VisualStudio/TeamTest/2006')) {
        throw 'TRX requires a supported TestRun XML namespace.'
    }
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('t', $root.NamespaceURI)
    $counterNodes = $document.SelectNodes('/t:TestRun/t:ResultSummary/t:Counters', $ns)
    if ($counterNodes.Count -ne 1) { throw 'TRX requires exactly one Counters element.' }
    $counterNode = $counterNodes[0]
    $outcomes = [ordered]@{
        Passed = 'passed'; Failed = 'failed'; NotExecuted = 'notExecuted'
        Error = 'error'; Timeout = 'timeout'; Aborted = 'aborted'
        Inconclusive = 'inconclusive'; PassedButRunAborted = 'passedButRunAborted'
        NotRunnable = 'notRunnable'; Disconnected = 'disconnected'; Warning = 'warning'
        Completed = 'completed'; InProgress = 'inProgress'; Pending = 'pending'
    }
    $counters = @{}
    foreach ($name in @('total', 'executed') + @($outcomes.Values)) {
        if (-not $counterNode.HasAttribute($name)) {
            if ($name -cin @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
                throw "Missing TRX counter '$name'."
            }
            $counters[$name] = 0L
        }
        else { $counters[$name] = ConvertTo-DogfoodCount $counterNode.GetAttribute($name) "TRX counter '$name'" }
    }
    foreach ($attribute in $counterNode.Attributes) {
        if ($attribute.NamespaceURI -eq '' -and -not $counters.ContainsKey($attribute.LocalName)) {
            throw "Unexpected TRX counter '$($attribute.LocalName)'."
        }
    }
    if ($counters.total -eq 0) { throw 'TRX contains zero tests.' }
    $sum = [decimal] 0
    foreach ($name in $outcomes.Values) { $sum += $counters[$name] }
    if ($sum -ne $counters.total -or $counters.executed -lt ($counters.passed + $counters.failed) -or
        $counters.executed -gt ($counters.total - $counters.notExecuted)) {
        throw 'Inconsistent TRX counters.'
    }

    if ($document.SelectNodes('/t:TestRun/t:TestDefinitions', $ns).Count -ne 1) {
        throw 'TRX requires exactly one TestDefinitions element.'
    }
    $definitions = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($definition in $document.SelectNodes('/t:TestRun/t:TestDefinitions/*', $ns)) {
        if ($definition.LocalName -cne 'UnitTest' -or $definition.NamespaceURI -cne $root.NamespaceURI) {
            throw 'Unexpected TRX test definition.'
        }
        $id = $definition.GetAttribute('id')
        $methods = $definition.SelectNodes('t:TestMethod', $ns)
        if ([string]::IsNullOrWhiteSpace($id) -or $methods.Count -ne 1 -or
            [string]::IsNullOrWhiteSpace($methods[0].GetAttribute('className')) -or
            [string]::IsNullOrWhiteSpace($methods[0].GetAttribute('name'))) {
            throw 'Missing TRX test definition identity.'
        }
        if ($definitions.ContainsKey($id)) { throw "Duplicate TRX definition id '$id'." }
        $definitions.Add($id, @(
            (Get-DogfoodTypeIdentity $methods[0].GetAttribute('className')),
            $methods[0].GetAttribute('name')))
    }
    if ($document.SelectNodes('/t:TestRun/t:Results', $ns).Count -ne 1) {
        throw 'TRX requires exactly one Results element.'
    }
    $resultNodes = $document.SelectNodes('/t:TestRun/t:Results/*', $ns)
    if ($resultNodes.Count -ne $counters.total) { throw 'Inconsistent TRX result count and total counter.' }
    $observed = @{}
    foreach ($outcome in $outcomes.Keys) { $observed[$outcome] = 0L }
    $cases = [Collections.Generic.List[object]]::new()
    $rawKeys = [Collections.Generic.List[string]]::new()
    foreach ($result in $resultNodes) {
        if ($result.LocalName -cne 'UnitTestResult' -or $result.NamespaceURI -cne $root.NamespaceURI -or
            $result.SelectNodes('.//t:UnitTestResult', $ns).Count -ne 0) {
            throw 'Unexpected or nested TRX result.'
        }
        $testId = $result.GetAttribute('testId')
        $testName = $result.GetAttribute('testName')
        if ([string]::IsNullOrWhiteSpace($testId) -or [string]::IsNullOrWhiteSpace($testName)) {
            throw 'Missing TRX result identity.'
        }
        if (-not $definitions.ContainsKey($testId)) { throw "Unresolved TRX definition '$testId'." }
        $outcome = $result.GetAttribute('outcome')
        if ($outcome -cnotin @($outcomes.Keys)) { throw "Unclassified TRX outcome '$outcome'." }
        $observed[$outcome]++
        $identity = [string[]] @($definitions[$testId][0], $definitions[$testId][1], $testName)
        $rawKey = ConvertTo-Json -InputObject $identity -Compress
        $rawKeys.Add($rawKey)
        $normalizedName = ConvertTo-DogfoodTrxDisplayName $identity[0] $identity[1] $testName $IdentityPolicy
        $key = $rawKey
        if (-not [StringComparer]::Ordinal.Equals($testName, $normalizedName)) {
            $identity[2] = $normalizedName
            $key = ConvertTo-Json -InputObject $identity -Compress
        }
        $cases.Add([pscustomobject]@{
            Key = $key
            Outcome = $outcome
        })
    }
    foreach ($outcome in $outcomes.Keys) {
        if ($observed[$outcome] -ne $counters[$outcomes[$outcome]]) {
            throw "Inconsistent TRX outcome counter '$($outcomes[$outcome])'."
        }
    }
    $hash = Get-DogfoodSelectionHash ([string[]] @($cases | ForEach-Object Key))
    $rawHash = if ($IdentityPolicy -ceq 'Exact') { $hash } else { Get-DogfoodSelectionHash $rawKeys.ToArray() }
    return [pscustomobject]@{
        Total = $counters.total
        Executed = $counters.executed
        Passed = $counters.passed
        Failed = $counters.failed
        Skipped = $counters.notExecuted
        Other = $counters.total - $counters.passed - $counters.failed - $counters.notExecuted
        Cases = $cases.ToArray()
        SelectionHash = $hash
        RawSelectionHash = $rawHash
    }
}

function Read-DogfoodFlag($Value, [string] $Name) {
    $flag = Get-DogfoodField $Value $Name
    if ($null -eq $flag) { return $false } # Protobuf JSON omits false scalars.
    if ($flag -isnot [bool]) { throw "Invalid BEP boolean '$Name'." }
    return $flag
}

function ConvertTo-DogfoodTimestamp($Value, [string] $Context) {
    if ($Value -is [DateTimeOffset]) { return $Value }
    # Newer ConvertFrom-Json versions materialize ISO strings as DateTime.
    if ($Value -is [DateTime]) {
        if ($Value.Kind -eq [DateTimeKind]::Unspecified) { throw "Timestamp requires a zone for $Context." }
        return [DateTimeOffset]::new($Value)
    }
    $timestamp = [DateTimeOffset]::MinValue
    if ($Value -isnot [string] -or $Value -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,9})?(Z|[+-]\d{2}:\d{2})$' -or
        -not [DateTimeOffset]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $timestamp)) {
        throw "Invalid ISO timestamp for $Context."
    }
    return $timestamp
}

function Get-DogfoodBepTimestamp($Value, [string] $Name, [string] $LegacyName) {
    $modern = Get-DogfoodField $Value $Name
    $legacy = Get-DogfoodField $Value $LegacyName
    $timestamp = if ($null -ne $modern) { ConvertTo-DogfoodTimestamp $modern "BEP $Name" } else { $null }
    $fallback = if ($null -ne $legacy) { [DateTimeOffset]::FromUnixTimeMilliseconds((ConvertTo-DogfoodCount $legacy "BEP $LegacyName")) } else { $null }
    if ($null -ne $timestamp -and $null -ne $fallback -and [Math]::Abs(($timestamp - $fallback).TotalMilliseconds) -ge 1) {
        throw "Contradictory BEP timestamps '$Name' and '$LegacyName'."
    }
    if ($null -ne $timestamp) { return $timestamp }
    return $fallback
}

function Get-DogfoodBepDuration($Value, [string] $Name, [string] $LegacyName) {
    $modern = Get-DogfoodField $Value $Name
    $legacy = Get-DogfoodField $Value $LegacyName
    $seconds = $null
    if ($null -ne $modern) {
        $parsed = [decimal] 0
        if ($modern -isnot [string] -or $modern -cnotmatch '^[0-9]+(\.[0-9]{1,9})?s$' -or
            -not [decimal]::TryParse($modern.Substring(0, $modern.Length - 1), [Globalization.NumberStyles]::AllowDecimalPoint,
                [Globalization.CultureInfo]::InvariantCulture, [ref] $parsed) -or $parsed -gt 315576000000) {
            throw "Invalid BEP duration '$Name'."
        }
        $seconds = $parsed
    }
    $milliseconds = if ($null -ne $legacy) { ConvertTo-DogfoodCount $legacy "BEP $LegacyName" } else { $null }
    # Legacy milliseconds truncate the protobuf duration. Decimal arithmetic
    # avoids admitting a contradictory value at the exact millisecond boundary.
    if ($null -ne $seconds -and $null -ne $milliseconds -and [decimal]::Truncate($seconds * 1000) -ne $milliseconds) {
        throw "Contradictory BEP durations '$Name' and '$LegacyName'."
    }
    if ($null -ne $seconds) { return [double] $seconds }
    if ($null -ne $milliseconds) { return $milliseconds / 1000.0 }
    return $null
}

function Read-BazelDogfoodEvents {
    <#
    .SYNOPSIS
    Reads Bazel 9.1.1 newline-delimited build events for the single-run pilot.
    .NOTES
    Cached is true for cachedLocally OR executionInfo.cachedRemotely (including
    disk cache reuse). Omitted protobuf false/zero scalars are accepted only in
    structurally complete result/summary evidence; absent execution/cache
    metadata is not evidence of execution. Log text is never consulted.
    DurationSeconds can be historical for cached tests. Actions preserves raw
    actionSummary statistics: actionsExecuted includes remote cache hits, and
    runnerCount entries are not guaranteed to be mutually exclusive.
    Completed requires started, finished and lastMessage. Success also requires
    exit code zero and PASSED test statuses. Invalid/ambiguous data throws;
    otherwise partial data stays incomplete, and unknown timings remain null.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [AllowEmptyCollection()][string[]] $ExpectedTargets
    )

    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $checkSelection = $PSBoundParameters.ContainsKey('ExpectedTargets')
    if ($checkSelection) {
        if ($null -eq $ExpectedTargets -or $ExpectedTargets.Count -eq 0) { throw 'ExpectedTargets must not be empty.' }
        foreach ($label in $ExpectedTargets) {
            if ([string]::IsNullOrWhiteSpace($label) -or -not $expected.Add($label)) {
                throw 'ExpectedTargets contains an empty or duplicate label.'
            }
        }
    }
    $results = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $summaries = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $started = $null
    $finished = $null
    $metrics = $null
    $lastMessage = $false
    $lineNumber = 0
    $eventCount = 0
    foreach ($line in [IO.File]::ReadLines([IO.Path]::GetFullPath($Path))) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($lastMessage) { throw 'BEP contains events after lastMessage.' }
        try { $buildEvent = ConvertFrom-Json -InputObject $line -AsHashtable -Depth 100 }
        catch { throw "Invalid BEP JSON at line ${lineNumber}: $($_.Exception.Message)" }
        $id = Get-DogfoodField $buildEvent 'id'
        if ($buildEvent -isnot [Collections.IDictionary] -or $id -isnot [Collections.IDictionary] -or $id.Count -ne 1) {
            throw "Invalid BEP event object at line $lineNumber."
        }
        $payloadNames = @($buildEvent.Keys | Where-Object { $_ -cnotin @('id', 'children', 'lastMessage') })
        if ($payloadNames.Count -ne 1) { throw "Invalid BEP event payload at line $lineNumber." }
        $eventCount++
        foreach ($kind in 'started', 'finished', 'buildMetrics', 'testResult', 'testSummary') {
            $idKind = if ($kind -eq 'finished') { 'buildFinished' } else { $kind }
            $identity = Get-DogfoodField $id $idKind
            $data = Get-DogfoodField $buildEvent $kind
            if ($null -eq $identity -and $null -eq $data) { continue }
            if ($identity -isnot [Collections.IDictionary] -or $data -isnot [Collections.IDictionary]) {
                throw "Missing or invalid BEP $kind identity/payload."
            }
            switch ($kind) {
                'started' {
                    if ($null -ne $started) { throw 'Duplicate BEP started event.' }
                    $started = $data
                }
                'finished' {
                    if ($null -ne $finished) { throw 'Duplicate BEP finished event.' }
                    $finished = $data
                }
                'buildMetrics' {
                    if ($null -ne $metrics) { throw 'Duplicate BEP buildMetrics event.' }
                    $metrics = $data
                }
                default {
                    $label = Get-DogfoodField $identity 'label'
                    $configuration = Get-DogfoodField (Get-DogfoodField $identity 'configuration') 'id'
                    if ($label -isnot [string] -or [string]::IsNullOrWhiteSpace($label) -or
                        $configuration -isnot [string] -or [string]::IsNullOrWhiteSpace($configuration)) {
                        throw "Missing BEP $kind label/configuration."
                    }
                    $collection = if ($kind -eq 'testResult') { $results } else { $summaries }
                    if ($collection.ContainsKey($label)) { throw "Duplicate BEP $kind label/configuration '$label'." }
                    $collection.Add($label, @{ Identity = $identity; Data = $data; Configuration = $configuration })
                }
            }
        }
        $lastMessage = Read-DogfoodFlag $buildEvent 'lastMessage'
    }
    if ($eventCount -eq 0) { throw 'BEP contains no events.' }
    if ($checkSelection) {
        $missingLabels = @($expected | Where-Object { -not $results.ContainsKey($_) -or -not $summaries.ContainsKey($_) })
        if ($missingLabels.Count -gt 0) { throw "Missing expected test target: $($missingLabels -join ', ')." }
        foreach ($label in @($results.Keys) + @($summaries.Keys)) {
            if (-not $expected.Contains($label)) { throw "Unexpected test target '$label'." }
        }
    }
    foreach ($label in @($results.Keys) + @($summaries.Keys)) {
        if (-not $results.ContainsKey($label) -or -not $summaries.ContainsKey($label)) {
            throw "Unpaired BEP result/summary for '$label'."
        }
    }
    $command = Get-DogfoodField $started 'command'
    if ($null -ne $started -and $command -cnotin @('build', 'test')) { throw 'Missing or unsupported BEP command.' }
    if ($command -ceq 'test' -and $results.Count -eq 0) { throw 'BEP test invocation contains zero tests.' }
    $targets = [Collections.Generic.List[object]]::new()
    $labels = [string[]] @($results.Keys)
    [Array]::Sort($labels, [StringComparer]::Ordinal)
    foreach ($label in $labels) {
        $result = $results[$label]
        $summary = $summaries[$label]
        if ($result.Configuration -cne $summary.Configuration) { throw "Mismatched BEP configuration for '$label'." }
        foreach ($name in 'run', 'shard', 'attempt') {
            if ((ConvertTo-DogfoodCount (Get-DogfoodField $result.Identity $name) "BEP $name") -ne 1) {
                throw "BEP pilot requires one run, shard and attempt for '$label'."
            }
        }
        foreach ($name in 'totalRunCount', 'runCount', 'attemptCount') {
            if ((ConvertTo-DogfoodCount (Get-DogfoodField $summary.Data $name) "BEP $name") -ne 1) {
                throw "BEP pilot requires one run, shard and attempt for '$label'."
            }
        }
        $shards = Get-DogfoodField $summary.Data 'shardCount'
        if ($null -ne $shards -and (ConvertTo-DogfoodCount $shards 'BEP shardCount') -ne 0) {
            throw "BEP pilot requires unsharded tests for '$label'."
        }
        $status = Get-DogfoodField $result.Data 'status'
        if ($status -cnotin @('PASSED', 'FLAKY', 'TIMEOUT', 'FAILED', 'INCOMPLETE', 'REMOTE_FAILURE', 'FAILED_TO_BUILD', 'TOOL_HALTED_BEFORE_TESTING')) {
            throw "Missing or unclassified BEP test status for '$label'."
        }
        if ($status -cne (Get-DogfoodField $summary.Data 'overallStatus')) { throw "Contradictory BEP test status for '$label'." }
        $locallyCached = Read-DogfoodFlag $result.Data 'cachedLocally'
        $execution = Get-DogfoodField $result.Data 'executionInfo'
        if ($null -ne $execution -and $execution -isnot [Collections.IDictionary]) { throw "Invalid BEP executionInfo for '$label'." }
        $remotelyCached = Read-DogfoodFlag $execution 'cachedRemotely'
        $strategy = Get-DogfoodField $execution 'strategy'
        if (-not $locallyCached -and -not $remotelyCached -and
            ($strategy -isnot [string] -or [string]::IsNullOrWhiteSpace($strategy))) {
            throw "Missing BEP execution/cache evidence for '$label'."
        }
        $cached = $locallyCached -or $remotelyCached
        $cachedCount = Get-DogfoodField $summary.Data 'totalNumCached'
        $cachedCount = if ($null -eq $cachedCount) { 0L } else { ConvertTo-DogfoodCount $cachedCount 'BEP totalNumCached' }
        if ($cachedCount -ne [int] $cached) { throw "Contradictory BEP cache evidence for '$label'." }
        $duration = Get-DogfoodBepDuration $summary.Data 'totalRunDuration' 'totalRunDurationMillis'
        $attemptDuration = Get-DogfoodBepDuration $result.Data 'testAttemptDuration' 'testAttemptDurationMillis'
        if ($null -ne $duration -and $null -ne $attemptDuration -and [Math]::Abs([decimal] $duration - [decimal] $attemptDuration) -ge [decimal] 0.001) {
            throw "Contradictory BEP result/summary durations for '$label'."
        }
        if ($null -eq $duration) { $duration = $attemptDuration }
        $targets.Add([pscustomobject]@{
            Label = $label
            Status = $status
            Cached = $cached
            RunCount = 1
            DurationSeconds = $duration
        })
    }
    $exitCode = $null
    if ($null -ne $finished) {
        $exit = Get-DogfoodField $finished 'exitCode'
        if ($exit -isnot [Collections.IDictionary]) { throw 'Missing BEP build exit code.' }
        $code = Get-DogfoodField $exit 'code'
        $name = Get-DogfoodField $exit 'name'
        # SUCCESS's code is protobuf's omitted zero, not missing exit evidence.
        if ($null -eq $code -and $name -ceq 'SUCCESS') { $code = 0 }
        $exitCode = ConvertTo-DogfoodCount $code 'BEP exit code'
        if (($name -ceq 'SUCCESS' -and $exitCode -ne 0) -or
            ($null -ne $name -and $name -cne 'SUCCESS' -and $exitCode -eq 0)) {
            throw 'Contradictory BEP exit name/code.'
        }
        $overall = Get-DogfoodField $finished 'overallSuccess'
        if ($null -ne $overall -and ($overall -isnot [bool] -or $overall -ne ($exitCode -eq 0))) {
            throw 'Contradictory BEP overall success/exit code.'
        }
    }
    $start = Get-DogfoodBepTimestamp $started 'startTime' 'startTimeMillis'
    $finish = Get-DogfoodBepTimestamp $finished 'finishTime' 'finishTimeMillis'
    $wall = $null
    if ($null -ne $start -and $null -ne $finish) {
        if ($finish -lt $start) { throw 'Negative BEP build interval.' }
        $wall = ($finish - $start).TotalSeconds
    }
    $reportedWall = Get-DogfoodField (Get-DogfoodField $metrics 'timingMetrics') 'wallTimeInMs'
    if ($null -ne $reportedWall) { $wall = (ConvertTo-DogfoodCount $reportedWall 'BEP wallTimeInMs') / 1000.0 }
    $completed = $null -ne $started -and $null -ne $finished -and $lastMessage
    $allPassed = @($targets | Where-Object Status -CNE 'PASSED').Count -eq 0
    return [pscustomobject]@{
        Completed = $completed
        Success = $completed -and $exitCode -eq 0 -and $allPassed
        ExitCode = $exitCode
        Targets = $targets.ToArray()
        Actions = Get-DogfoodField $metrics 'actionSummary'
        WallSeconds = $wall
    }
}

function Get-DogfoodTimelineInterval($Record, [string] $Context) {
    $startValue = Get-DogfoodField $Record 'startTime'
    $finishValue = Get-DogfoodField $Record 'finishTime'
    $start = if ($null -ne $startValue -and $startValue -cne '') { ConvertTo-DogfoodTimestamp $startValue "$Context startTime" } else { $null }
    $finish = if ($null -ne $finishValue -and $finishValue -cne '') { ConvertTo-DogfoodTimestamp $finishValue "$Context finishTime" } else { $null }
    $state = Get-DogfoodField $Record 'state'
    if ($null -ne $state -and $state -cnotin @('pending', 'inProgress', 'completed')) {
        throw "Invalid timeline state for $Context."
    }
    if (($null -ne $finish -and $state -cne 'completed') -or ($null -ne $start -and $state -ceq 'pending')) {
        throw "Contradictory timeline state/timestamps for $Context."
    }
    if ($null -ne $start -and $null -ne $finish -and $finish -lt $start) {
        throw "Negative timeline interval for $Context."
    }
    $seconds = if ($null -ne $start -and $null -ne $finish) { ($finish - $start).TotalSeconds } else { $null }
    return [pscustomobject]@{ Start = $start; Finish = $finish; Seconds = $seconds }
}

function Get-DogfoodTaskAncestry($Record, $Index) {
    $ancestors = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void] $seen.Add((Get-DogfoodField $Record 'id'))
    $parentId = Get-DogfoodField $Record 'parentId'
    while (-not [string]::IsNullOrWhiteSpace($parentId)) {
        if (-not $seen.Add($parentId)) { throw 'Cycle in timeline task parent hierarchy.' }
        if (-not $Index.ContainsKey($parentId)) { throw "Unresolved timeline task parent '$parentId'." }
        $parent = $Index[$parentId]
        $ancestors.Add($parent)
        if ((Get-DogfoodField $parent 'type') -ceq 'Job') {
            return [pscustomobject]@{ Job = $parent; Ancestors = $ancestors.ToArray() }
        }
        $parentId = Get-DogfoodField $parent 'parentId'
    }
    throw 'Unresolved timeline task Job ancestor.'
}

function Get-BazelDogfoodTimeline {
    <#
    .SYNOPSIS
    Summarizes one Azure job and its Cache@2 restore/post-job-save task timings.
    .NOTES
    JobName matches id (SYSTEM_JOBID, case-insensitive GUID), then identifier
    exactly, then name exactly. CacheTasks has six
    slots (Name, Kind, Id, State, Result, DurationSeconds); Kind is Restore/Save,
    State is missing/skipped/unknown or the Azure record state. Missing names optional
    cache tasks and unavailable state/timestamps. Missing optional task slots do
    not invalidate Complete, but missing timing on an existing task does.
    Complete measures availability/completion, NOT job success. Absent or
    unknown durations and aggregates are null, including skipped records without
    timestamps. Only Task records are summed; Job/Phase spans are never added.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Timeline,
        [Parameter(Mandatory)][string] $JobName
    )

    if ([string]::IsNullOrWhiteSpace($JobName)) { throw 'JobName must not be empty.' }
    # Preserve the distinction between an empty records array and no records.
    $records = if ($Timeline -is [Collections.IDictionary]) { ,$Timeline['records'] }
    elseif ($null -ne $Timeline.PSObject.Properties['records']) { ,$Timeline.PSObject.Properties['records'].Value }
    else { $null }
    if ($records -isnot [Collections.IList]) { throw 'Timeline requires a records array.' }
    $records = @($records)
    $index = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($record in $records) {
        $id = Get-DogfoodField $record 'id'
        if ($id -isnot [string] -or [string]::IsNullOrWhiteSpace($id)) { throw 'Missing timeline record id.' }
        if ($index.ContainsKey($id)) { throw "Duplicate timeline record id '$id'." }
        $index.Add($id, $record)
    }
    $jobs = @($records | Where-Object { (Get-DogfoodField $_ 'type') -ceq 'Job' })
    $jobMatches = @($jobs | Where-Object { (Get-DogfoodField $_ 'id') -ieq $JobName })
    if ($jobMatches.Count -eq 0) { $jobMatches = @($jobs | Where-Object { (Get-DogfoodField $_ 'identifier') -ceq $JobName }) }
    if ($jobMatches.Count -eq 0) { $jobMatches = @($jobs | Where-Object { (Get-DogfoodField $_ 'name') -ceq $JobName }) }
    if ($jobMatches.Count -gt 1) { throw "Ambiguous timeline Job '$JobName'." }
    $cacheNames = @('Cache Bazel actions', 'Cache Bazel downloads', 'Cache MSBuild packages')
    $tasks = [Collections.Generic.List[object]]::new()
    $slots = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $displayNames = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($kind in 'Restore', 'Save') {
        foreach ($name in $cacheNames) {
            $key = "${kind}:$name"
            $slot = [pscustomobject]@{ Name = $name; Kind = $kind; Id = $null; State = 'missing'; Result = $null; DurationSeconds = $null }
            $slots.Add($key, $slot)
            $tasks.Add($slot)
            $displayName = if ($kind -eq 'Save') { "Post-job: $name" } else { $name }
            $displayNames.Add($displayName, $key)
        }
    }
    $missing = [Collections.Generic.List[string]]::new()
    $jobSeconds = $null
    $complete = $false
    if ($jobMatches.Count -eq 0) {
        $missing.Add("Job:$JobName")
    }
    else {
        $job = $jobMatches[0]
        $jobId = Get-DogfoodField $job 'id'
        $jobInterval = Get-DogfoodTimelineInterval $job "Job:$JobName"
        $jobSeconds = $jobInterval.Seconds
        $complete = (Get-DogfoodField $job 'state') -ceq 'completed' -and $null -ne $jobSeconds
        if ((Get-DogfoodField $job 'state') -cne 'completed') { $missing.Add("Job:${JobName}:completed") }
        if ($null -eq $jobInterval.Start) { $missing.Add("Job:${JobName}:startTime") }
        if ($null -eq $jobInterval.Finish) { $missing.Add("Job:${JobName}:finishTime") }
        foreach ($record in $records) {
            if ((Get-DogfoodField $record 'type') -cne 'Task') { continue }
            $name = Get-DogfoodField $record 'name'
            if ($null -eq $name -or -not $displayNames.ContainsKey($name)) { continue }
            $ancestry = Get-DogfoodTaskAncestry $record $index
            if ((Get-DogfoodField $ancestry.Job 'id') -ine $jobId) { continue }
            $key = $displayNames[$name]
            $slot = $slots[$key]
            if ($null -ne $slot.Id) { throw "Duplicate timeline cache task '$key'." }
            $interval = Get-DogfoodTimelineInterval $record $key
            $childInterval = $interval
            foreach ($ancestor in $ancestry.Ancestors) {
                $parentInterval = if ((Get-DogfoodField $ancestor 'id') -ieq $jobId) { $jobInterval }
                else { Get-DogfoodTimelineInterval $ancestor "parent of $key" }
                # Check the task against every ancestor as well as neighboring
                # spans: an untimed Phase must not hide an out-of-Job task.
                foreach ($candidate in @($interval, $childInterval)) {
                    if (($null -ne $candidate.Start -and $null -ne $parentInterval.Start -and $candidate.Start -lt $parentInterval.Start) -or
                        ($null -ne $candidate.Finish -and $null -ne $parentInterval.Finish -and $candidate.Finish -gt $parentInterval.Finish) -or
                        ($null -ne $candidate.Start -and $null -ne $parentInterval.Finish -and $candidate.Start -gt $parentInterval.Finish) -or
                        ($null -ne $candidate.Finish -and $null -ne $parentInterval.Start -and $candidate.Finish -lt $parentInterval.Start)) {
                        throw "Timeline interval outside parent for '$key'."
                    }
                }
                $childInterval = $parentInterval
            }
            $slot.Id = Get-DogfoodField $record 'id'
            $slot.Result = Get-DogfoodField $record 'result'
            $state = Get-DogfoodField $record 'state'
            if ($slot.Result -ceq 'skipped' -and $state -cne 'completed') { throw "Contradictory timeline skipped state for '$key'." }
            $slot.State = if ($slot.Result -ceq 'skipped') { 'skipped' } elseif ($null -eq $state) { 'unknown' } else { $state }
            $slot.DurationSeconds = $interval.Seconds
            if ($state -cne 'completed') { $complete = $false; $missing.Add("${key}:completed") }
            if ($null -eq $interval.Start) { $complete = $false; $missing.Add("${key}:startTime") }
            if ($null -eq $interval.Finish) { $complete = $false; $missing.Add("${key}:finishTime") }
        }
    }
    foreach ($slot in $tasks) {
        if ($null -eq $slot.Id) { $missing.Add("$($slot.Kind):$($slot.Name)") }
    }
    $sums = @{}
    foreach ($kind in 'Restore', 'Save') {
        $present = @($tasks | Where-Object { $_.Kind -ceq $kind -and $null -ne $_.Id })
        $sum = $null
        if ($present.Count -gt 0 -and @($present | Where-Object { $null -eq $_.DurationSeconds }).Count -eq 0) {
            $sum = 0.0
            foreach ($task in $present) { $sum += $task.DurationSeconds }
        }
        $sums[$kind] = $sum
    }
    return [pscustomobject]@{
        Complete = $complete
        JobSeconds = $jobSeconds
        CacheRestoreSeconds = $sums.Restore
        CacheSaveSeconds = $sums.Save
        CacheTasks = $tasks.ToArray()
        Missing = $missing.ToArray()
    }
}

Export-ModuleMember -Function Read-BazelDogfoodTrx, Read-BazelDogfoodEvents, Get-BazelDogfoodTimeline
