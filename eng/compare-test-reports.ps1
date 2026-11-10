#requires -Version 7.2
<#
.SYNOPSIS
Checks that MTP TRX, CTRF, and JUnit reports describe the same execution.
.DESCRIPTION
Directory mode compares sibling <stem>.trx, <stem>.ctrf.json, and <stem>.xml
files, without recursing into merged/retry/child-asset reports. Explicit-file
mode accepts arbitrary filenames. Inputs are never changed. Missing, invalid,
incomplete, ambiguous, or inconsistent reports fail the command. Consistent
zero-result reports are valid; agreement does not prove that tests executed.
Unrelated XML artifacts are ignored unless they occupy an expected JUnit path.
.EXAMPLE
pwsh -NoProfile -File eng/compare-test-reports.ps1 -ResultsDirectory artifacts/TestResults/Release
.EXAMPLE
pwsh -NoProfile -File eng/compare-test-reports.ps1 -TrxPath run.trx -CtrfPath run.json -JUnitPath run.xml
.NOTES
See docs/decisions/test-report-consistency.md for the comparison contract.
#>
[CmdletBinding(DefaultParameterSetName = "Directory")]
param(
    [Parameter(ParameterSetName = "Directory", Position = 0)]
    [string]$ResultsDirectory,
    [Parameter(ParameterSetName = "Files")]
    [string]$TrxPath,
    [Parameter(ParameterSetName = "Files")]
    [string]$CtrfPath,
    [Parameter(ParameterSetName = "Files")]
    [string]$JUnitPath,
    [ValidateScript({ $_ -ge 0 })]
    [decimal]$ToleranceMilliseconds = 1
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Format-ReportValue {
    param([object]$Value, [int]$Limit = 200)

    $text = ConvertTo-Json -InputObject ([string]$Value) -Compress
    if ($text.Length -gt $Limit) { return $text.Substring(0, $Limit) + "..." }
    return $text
}

function Assert-ReportEqual {
    param([object]$Expected, [object]$Actual, [string]$Context)

    if (-not [string]::Equals([string]$Expected, [string]$Actual, [StringComparison]::Ordinal)) {
        throw "${Context}: expected $(Format-ReportValue $Expected); got $(Format-ReportValue $Actual)."
    }
}

function ConvertTo-ReportNumber {
    param([object]$Value, [string]$Context, [switch]$Json)

    [decimal]$number = 0
    $text = [Convert]::ToString($Value, [Globalization.CultureInfo]::InvariantCulture)
    if (($Json -and ($Value -isnot [ValueType] -or $Value -is [bool])) -or
        -not [decimal]::TryParse($text, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number) -or
        $number -lt 0) {
        throw "${Context}: expected a finite nonnegative number, got '$text'."
    }
    return $number
}

function Assert-ReportTiming {
    param([decimal]$Expected, [decimal]$Actual, [decimal]$Tolerance, [string]$Context)

    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance) {
        $culture = [Globalization.CultureInfo]::InvariantCulture
        throw "${Context}: expected $($Expected.ToString($culture)) ms; got $($Actual.ToString($culture)) ms (tolerance $($Tolerance.ToString($culture)) ms)."
    }
}

function Get-ReportAttribute {
    param([Xml.XmlElement]$Node, [string]$Name)

    if ($null -eq $Node -or -not $Node.HasAttribute($Name) -or $Node.GetAttribute($Name).Length -eq 0) {
        throw "Missing required XML attribute '$Name'."
    }
    return $Node.GetAttribute($Name)
}

function ConvertFrom-ReportJsonValue {
    param([AllowNull()][object]$Value)

    if ($Value -is [Newtonsoft.Json.Linq.JObject] -or $Value -is [Newtonsoft.Json.Linq.JArray]) {
        $serializer = [Newtonsoft.Json.JsonSerializer]::Create()
        $serializer.DateParseHandling = [Newtonsoft.Json.DateParseHandling]::None
        $serializer.MetadataPropertyHandling = [Newtonsoft.Json.MetadataPropertyHandling]::Ignore
        $serializer.TypeNameHandling = [Newtonsoft.Json.TypeNameHandling]::None
        $serializer.MaxDepth = 100
        $type = if ($Value -is [Newtonsoft.Json.Linq.JObject]) { [hashtable] } else { [object[]] }
        return ,$Value.ToObject($type, $serializer)
    }
    if ($Value -is [Newtonsoft.Json.Linq.JValue]) { return ,$Value.Value }
    return ,$Value
}

function Read-ReportJson {
    param([string]$Path)

    $json = Get-Content -LiteralPath $Path -Raw -Encoding utf8
    if ((Get-Command ConvertFrom-Json -CommandType Cmdlet).Parameters.ContainsKey("DateKind")) {
        return ,($json | ConvertFrom-Json -AsHashtable -Depth 100 -DateKind String)
    }
    Add-Type -AssemblyName Newtonsoft.Json
    $textReader = [IO.StringReader]::new($json)
    $reader = [Newtonsoft.Json.JsonTextReader]::new($textReader)
    try {
        $reader.DateParseHandling = [Newtonsoft.Json.DateParseHandling]::None
        $reader.MaxDepth = 100
        $token = [Newtonsoft.Json.Linq.JToken]::ReadFrom($reader)
        if ($reader.Read()) { throw "Conversion from JSON failed for '$Path': more than one root value." }
        return ,(ConvertFrom-ReportJsonValue $token)
    } catch [Newtonsoft.Json.JsonException] {
        throw "Conversion from JSON failed for '${Path}': $($_.Exception.Message)"
    } finally {
        $reader.Dispose()
        $textReader.Dispose()
    }
}

function Get-ReportJsonValue {
    param([object]$Node, [string]$Name, [switch]$Optional)

    if ($Node -isnot [Collections.IDictionary]) {
        throw "Expected a JSON object containing '$Name'."
    }
    if (-not $Node.Contains($Name)) {
        if ($Optional) { return $null }
        throw "Missing required JSON property '$Name'."
    }
    $value = $Node[$Name]
    if ($value -is [Newtonsoft.Json.Linq.JToken]) { return ,(ConvertFrom-ReportJsonValue $value) }
    return ,$value
}

function Get-ReportJsonString {
    param([object]$Node, [string]$Name, [switch]$Optional)

    $value = Get-ReportJsonValue $Node $Name -Optional:$Optional
    if ($Optional -and $null -eq $value) { return $null }
    if ($value -isnot [string] -or $value.Length -eq 0) {
        throw "JSON property '$Name' must be a nonempty string."
    }
    return $value
}

function Get-ReportJsonText {
    param([Collections.IDictionary]$Node, [string]$Name)

    if (-not $Node.Contains($Name)) { return "" }
    if ($Node[$Name] -isnot [string]) { throw "JSON property '$Name' must be a string." }
    return $Node[$Name]
}

function Read-ReportXml {
    param([string]$Path)

    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.Path]::GetFullPath($Path), $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.PreserveWhitespace = $true
        $document.Load($reader)
        return ,$document
    } finally {
        $reader.Dispose()
    }
}

function ConvertTo-ReportXmlText {
    param([AllowNull()][string]$Text, [switch]$Trx, [switch]$PreserveNewlines)

    $Text = [string]$Text
    # TRX escapes invalid UTF-16 code units; JUnit replaces them with U+FFFD.
    $invalidXml = '[\x00-\x08\x0B\x0C\x0E-\x1F\uFFFE\uFFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]'
    $result = $Text
    # Avoid constructing a PowerShell callback for the overwhelmingly common valid-text path.
    if ([regex]::IsMatch($Text, $invalidXml)) {
        $result = [regex]::Replace($Text, $invalidXml, [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            if ($Trx) { return '\u{0:x4}' -f [int][char]$match.Value }
            return [string][char]0xFFFD
        })
    }
    if ($PreserveNewlines) { return $result }
    return $result.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Limit-ReportCapturedText {
    param([string]$Text, [int]$Limit = 4096)

    if ($Text.Length -le $Limit) { return $Text }
    $cut = $Limit
    if ([char]::IsHighSurrogate($Text[$cut - 1])) { $cut-- }
    return $Text.Substring(0, $cut) + "`n$([char]0x2026)[truncated, original length: $($Text.Length)]"
}

function Get-ReportCaptureInfo {
    param([string]$Text)

    if ($Text.Length -le 4096) {
        return [pscustomobject]@{ Prefix = $Text; OriginalLength = $Text.Length; Truncated = $false }
    }
    $marker = [regex]::Escape("`n$([char]0x2026)[truncated, original length: ")
    $match = [regex]::Match($Text, "(?s)^(?<prefix>.*)$marker(?<length>[0-9]+)\]$")
    [int]$length = 0
    if (-not $match.Success -or $match.Groups["prefix"].Value.Length -notin 4095, 4096 -or
        -not [int]::TryParse($match.Groups["length"].Value, [ref]$length) -or $length -le 4096) {
        throw "Invalid captured identity truncation marker."
    }
    return [pscustomobject]@{ Prefix = $match.Groups["prefix"].Value; OriginalLength = $length; Truncated = $true }
}

function Test-TrxCapturedName {
    param([string]$CapturedName, [string]$TrxName)

    if ($CapturedName.Length -le 4096) {
        return [string]::Equals((ConvertTo-ReportXmlText $CapturedName -Trx), (ConvertTo-ReportXmlText $TrxName -Trx), [StringComparison]::Ordinal)
    }
    $capture = Get-ReportCaptureInfo $CapturedName
    $prefix = ConvertTo-ReportXmlText $capture.Prefix -Trx -PreserveNewlines
    if (-not $TrxName.StartsWith($prefix, [StringComparison]::Ordinal)) { return $false }
    $tail = $TrxName.Substring($prefix.Length)
    $extra = $tail.Length - ($capture.OriginalLength - $capture.Prefix.Length)
    if ($extra -lt 0 -or $extra % 5 -ne 0) { return $false }
    if ($extra -eq 0) { return $true }

    # A TRX escape can also be literal backslash-u text. Check possible length
    # expansion without decoding it or inventing the uncaptured suffix.
    $possibleEscapes = 0
    foreach ($match in [regex]::Matches($tail, '\\u([0-9a-f]{4})')) {
        $code = [int]::Parse($match.Groups[1].Value, [Globalization.NumberStyles]::HexNumber, [Globalization.CultureInfo]::InvariantCulture)
        if ($code -le 8 -or $code -in 11, 12 -or ($code -ge 14 -and $code -le 31) -or
            ($code -ge 0xD800 -and $code -le 0xDFFF) -or $code -in 0xFFFE, 0xFFFF) {
            $possibleEscapes++
        }
    }
    return $extra / 5 -le $possibleEscapes
}

function Test-ReportClassCapture {
    param([string[]]$Suite, [string]$TrxClass)

    if ($Suite.Count -eq 1) {
        return [string]::Equals((ConvertTo-ReportXmlText $Suite[0] -Trx), (ConvertTo-ReportXmlText (Limit-ReportCapturedText $TrxClass) -Trx), [StringComparison]::Ordinal)
    }
    if ($Suite.Count -ne 2) { return $false }
    if ($Suite[0].Length -le 4096 -and $Suite[1].Length -le 4096 -and ($Suite -join ".") -ceq $TrxClass) { return $true }
    $namespaceLength = (Get-ReportCaptureInfo $Suite[0]).OriginalLength
    if ($TrxClass.Length -le $namespaceLength -or $TrxClass[$namespaceLength] -cne ".") { return $false }
    $namespace = Limit-ReportCapturedText $TrxClass.Substring(0, $namespaceLength)
    $type = Limit-ReportCapturedText $TrxClass.Substring($namespaceLength + 1)
    return [string]::Equals((ConvertTo-ReportXmlText $Suite[0] -Trx), (ConvertTo-ReportXmlText $namespace -Trx), [StringComparison]::Ordinal) -and
        [string]::Equals((ConvertTo-ReportXmlText $Suite[1] -Trx), (ConvertTo-ReportXmlText $type -Trx), [StringComparison]::Ordinal)
}

function ConvertTo-ReportStream {
    param([AllowNull()][string]$Text)

    $text = ConvertTo-ReportXmlText $Text
    $lineCount = 0
    if ($text.Length -gt 0) {
        $lineCount = $text.Split("`n").Length
        if ($text.EndsWith("`n", [StringComparison]::Ordinal)) {
            $lineCount--
            $text = $text.Substring(0, $text.Length - 1)
        }
    }
    return "${lineCount} lines: $text"
}

function ConvertTo-ReportTraits {
    param([string[]]$Traits)

    [Array]::Sort($Traits, [StringComparer]::Ordinal)
    return ConvertTo-Json -InputObject $Traits -Compress
}

function Read-TrxComparisonReport {
    param([string]$Path)

    $document = Read-ReportXml $Path
    $root = $document.DocumentElement
    if ($root.LocalName -cne "TestRun" -or $root.NamespaceURI -cne "http://microsoft.com/schemas/VisualStudio/TeamTest/2010") {
        throw "Expected an MTP TRX TestRun document."
    }
    $definitions = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($definition in $root.SelectNodes("*[local-name()='TestDefinitions']/*[local-name()='UnitTest']")) {
        $id = ([guid](Get-ReportAttribute $definition "id")).ToString("D")
        if (-not $definitions.TryAdd($id, $definition)) { throw "Duplicate TRX definition '$id'." }
    }
    $tests = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $executions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($node in $root.SelectNodes("*[local-name()='Results']/*[local-name()='UnitTestResult']")) {
        $id = ([guid](Get-ReportAttribute $node "testId")).ToString("D")
        if (-not $definitions.ContainsKey($id)) { throw "TRX result '$id' has no test definition." }
        if (-not $executions.Add((Get-ReportAttribute $node "executionId"))) { throw "Duplicate TRX execution ID." }
        $method = $definitions[$id].SelectSingleNode("*[local-name()='TestMethod']")
        $outcome = switch -CaseSensitive (Get-ReportAttribute $node "outcome") {
            "Passed" { "passed" }
            "Failed" { "failed" }
            "NotExecuted" { "skipped" }
            default { throw "Unsupported TRX outcome '$($_)'." }
        }
        $duration = [TimeSpan]::Parse((Get-ReportAttribute $node "duration"), [Globalization.CultureInfo]::InvariantCulture)
        $start = [DateTimeOffset]::Parse((Get-ReportAttribute $node "startTime"), [Globalization.CultureInfo]::InvariantCulture)
        $stop = [DateTimeOffset]::Parse((Get-ReportAttribute $node "endTime"), [Globalization.CultureInfo]::InvariantCulture)
        if ($duration.Ticks -lt 0 -or $stop -lt $start) { throw "TRX result '$id' has invalid timing." }
        $test = [pscustomobject]@{
            Id = $id
            Name = Get-ReportAttribute $node "testName"
            Class = Get-ReportAttribute $method "className"
            Method = Get-ReportAttribute $method "name"
            Outcome = $outcome
            Duration = [decimal]$duration.Ticks / [TimeSpan]::TicksPerMillisecond
            Start = [decimal]$start.UtcTicks / [TimeSpan]::TicksPerMillisecond
            Stop = [decimal]$stop.UtcTicks / [TimeSpan]::TicksPerMillisecond
        }
        if (-not $tests.TryAdd($id, $test)) { throw "Ambiguous repeated TRX test ID '$id'." }
    }
    Assert-ReportEqual $tests.Count $definitions.Count "TRX definition/result count"
    $counters = $root.SelectSingleNode("*[local-name()='ResultSummary']/*[local-name()='Counters']")
    $counts = @{}
    foreach ($name in @("total", "executed", "passed", "failed", "timeout", "notExecuted")) {
        $counts[$name] = ConvertTo-ReportNumber (Get-ReportAttribute $counters $name) "TRX counter '$name'"
    }
    Assert-ReportEqual $tests.Count $counts.total "TRX total counter"
    Assert-ReportEqual @($tests.Values | Where-Object Outcome -CEQ "passed").Count $counts.passed "TRX passed counter"
    Assert-ReportEqual @($tests.Values | Where-Object Outcome -CEQ "skipped").Count $counts.notExecuted "TRX skipped counter"
    Assert-ReportEqual @($tests.Values | Where-Object Outcome -CEQ "failed").Count ($counts.failed + $counts.timeout) "TRX failed/timeout counters"
    Assert-ReportEqual ($counts.passed + $counts.failed) $counts.executed "TRX executed counter"
    return [pscustomobject]@{ Tests = $tests; Timeouts = $counts.timeout }
}

function Read-CtrfComparisonReport {
    param([string]$Path)

    $document = Read-ReportJson $Path
    $extra = Get-ReportJsonValue $document "extra" -Optional
    $platformExtra = if ($null -ne $extra) { Get-ReportJsonValue $extra "microsoft.testingplatform" -Optional } else { $null }
    if ($null -ne $platformExtra -and (Get-ReportJsonString $platformExtra "documentRole" -Optional) -ceq "merged") {
        throw "Merged CTRF reports cannot be compared alongside raw execution reports."
    }
    $results = Get-ReportJsonValue $document "results"
    $environment = Get-ReportJsonValue $results "environment" -Optional
    $environmentExtra = if ($null -ne $environment) { Get-ReportJsonValue $environment "extra" -Optional } else { $null }
    if ($null -ne $environmentExtra -and
        ($environmentExtra["incomplete"] -eq $true -or $environmentExtra["runStatus"] -ceq "aborted")) {
        throw "Incomplete CTRF execution report."
    }
    $nodes = Get-ReportJsonValue $results "tests"
    if ($nodes -isnot [Collections.IList]) { throw "CTRF results.tests must be an array." }
    $tests = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $junitIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $statuses = @{}
    foreach ($node in $nodes) {
        $node = ConvertFrom-ReportJsonValue $node
        $extra = Get-ReportJsonValue $node "extra"
        $uid = Get-ReportJsonString $extra "uid"
        if ($tests.ContainsKey($uid)) { throw "Ambiguous repeated CTRF UID '$uid'." }
        $junitId = ConvertTo-ReportXmlText $uid -PreserveNewlines
        if (-not $junitIds.Add($junitId)) { throw "Ambiguous CTRF UIDs sanitize to the same JUnit identity." }
        $status = Get-ReportJsonString $node "status"
        $rawStatus = Get-ReportJsonString $node "rawStatus" -Optional
        $kind = switch -CaseSensitive ("$status/$rawStatus") {
            "passed/" { "passed" }
            "failed/" { "failure" }
            "failed/errored" { "error" }
            "failed/timedOut" { "error" }
            "skipped/" { "skipped" }
            "other/cancelled" { "error" }
            default { throw "Unsupported CTRF status/rawStatus '$status/$rawStatus' for '$uid'." }
        }
        $outcome = if ($kind -ceq "error" -or $kind -ceq "failure") { "failed" } else { $kind }
        $statuses[$status] = 1 + [int]$statuses[$status]
        $retryAttempts = Get-ReportJsonValue $node "retryAttempts" -Optional
        $retries = Get-ReportJsonValue $node "retries" -Optional
        if (($null -ne $retryAttempts -and @($retryAttempts).Count -gt 0) -or ($null -ne $retries -and $retries -ne 0)) {
            throw "Retry-collapsed CTRF result '$uid' cannot be compared to raw attempts."
        }
        $suite = Get-ReportJsonValue $node "suite" -Optional
        if ($null -ne $suite -and ($suite -isnot [Collections.IList] -or @($suite | Where-Object { $_ -isnot [string] }).Count -ne 0)) {
            throw "CTRF suite for '$uid' must be an array of strings."
        }
        $class = if ($null -ne $suite) { $suite -join "." } else { $null }
        $start = Get-ReportJsonValue $node "start" -Optional
        $stop = Get-ReportJsonValue $node "stop" -Optional
        if ($null -ne $start -or $null -ne $stop) {
            $start = ConvertTo-ReportNumber $start "CTRF '$uid' start" -Json
            $stop = ConvertTo-ReportNumber $stop "CTRF '$uid' stop" -Json
            if ($stop -lt $start) { throw "CTRF result '$uid' stops before it starts." }
        }
        $streams = @{}
        foreach ($stream in @("stdout", "stderr")) {
            $lines = Get-ReportJsonValue $node $stream -Optional
            if ($null -ne $lines -and ($lines -isnot [Collections.IList] -or @($lines | Where-Object { $_ -isnot [string] }).Count -ne 0)) {
                throw "CTRF '$uid' $stream must be an array of strings."
            }
            $text = ConvertTo-ReportXmlText ($lines -join "`n")
            $lineCount = if ($null -eq $lines -or $lines.Count -eq 0) { 0 } else { $text.Split("`n").Length }
            $streams[$stream] = "${lineCount} lines: $text"
        }
        $traits = [Collections.Generic.List[string]]::new()
        $labels = Get-ReportJsonValue $node "labels" -Optional
        if ($null -ne $labels) {
            if ($labels -isnot [Collections.IDictionary]) { throw "CTRF labels for '$uid' must be an object." }
            foreach ($key in $labels.Keys) {
                $values = ConvertFrom-ReportJsonValue $labels[$key]
                foreach ($value in @($values)) {
                    if ($value -isnot [string]) { throw "CTRF label '$key' must contain strings." }
                    $traits.Add((ConvertTo-Json -InputObject @((ConvertTo-ReportXmlText $key), (ConvertTo-ReportXmlText $value)) -Compress))
                }
            }
        }
        $test = [pscustomobject]@{
            Id = $uid
            JUnitId = $junitId
            Name = Get-ReportJsonString $node "name"
            Class = $class
            Suite = $suite
            HasTruncatedClass = $null -ne $suite -and @($suite | Where-Object { $_.Length -gt 4096 }).Count -gt 0
            Method = Get-ReportJsonString $extra "method" -Optional
            Outcome = $outcome
            Kind = $kind
            RawStatus = $rawStatus
            Duration = ConvertTo-ReportNumber (Get-ReportJsonValue $node "duration") "CTRF '$uid' duration" -Json
            Start = $start
            Stop = $stop
            Message = Get-ReportJsonText $node "message"
            Trace = Get-ReportJsonText $node "trace"
            ExceptionType = Get-ReportJsonText $extra "exceptionType"
            StdOut = $streams.stdout
            StdErr = $streams.stderr
            Traits = ConvertTo-ReportTraits $traits.ToArray()
        }
        if (-not $tests.TryAdd($uid, $test)) { throw "Ambiguous repeated CTRF UID '$uid'." }
    }
    $summary = Get-ReportJsonValue $results "summary"
    Assert-ReportEqual $tests.Count (ConvertTo-ReportNumber (Get-ReportJsonValue $summary "tests") "CTRF total counter" -Json) "CTRF total counter"
    foreach ($status in @("passed", "failed", "skipped", "pending", "other")) {
        $actual = ConvertTo-ReportNumber (Get-ReportJsonValue $summary $status) "CTRF '$status' counter" -Json
        Assert-ReportEqual ([int]$statuses[$status]) $actual "CTRF '$status' counter"
    }
    if ($summary.Contains("start") -or $summary.Contains("stop") -or $summary.Contains("duration")) {
        $start = ConvertTo-ReportNumber (Get-ReportJsonValue $summary "start") "CTRF summary start" -Json
        $stop = ConvertTo-ReportNumber (Get-ReportJsonValue $summary "stop") "CTRF summary stop" -Json
        if ($stop -lt $start) { throw "CTRF summary stops before it starts." }
        if ($summary.Contains("duration")) {
            $duration = ConvertTo-ReportNumber $summary["duration"] "CTRF summary duration" -Json
            Assert-ReportTiming ($stop - $start) $duration 0 "CTRF summary wall-clock duration"
        }
    }
    return $tests
}

function Assert-JUnitAggregateTiming {
    param([Xml.XmlElement]$Node)

    [decimal]$total = 0
    $cases = $Node.SelectNodes(".//*[local-name()='testcase']")
    foreach ($case in $cases) {
        $total += 1000 * (ConvertTo-ReportNumber (Get-ReportAttribute $case "time") "JUnit testcase duration")
    }
    $reported = 1000 * (ConvertTo-ReportNumber (Get-ReportAttribute $Node "time") "JUnit aggregate duration")
    # Each testcase and the sum are rounded independently to the nearest millisecond.
    Assert-ReportTiming $total $reported ([decimal]0.5 * ($cases.Count + 1)) "JUnit aggregate duration"
}

function Read-JUnitComparisonReport {
    param([string]$Path)

    $document = Read-ReportXml $Path
    $root = $document.DocumentElement
    if ($root.LocalName -cnotin @("testsuites", "testsuite")) { throw "Expected a JUnit testsuites/testsuite document." }
    $suites = if ($root.LocalName -ceq "testsuite") { @($root) } else { @($root.SelectNodes("*[local-name()='testsuite']")) }
    $tests = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($suite in $suites) {
        if ($null -ne $suite.SelectSingleNode("*[local-name()='properties']/*[local-name()='property'][@name='incomplete' and @value='true']") -or
            $null -ne $suite.SelectSingleNode("*[local-name()='properties']/*[local-name()='property'][@name='run-status' and @value='aborted']")) {
            throw "Incomplete JUnit execution report."
        }
        $nodes = @($suite.SelectNodes("*[local-name()='testcase']"))
        $kinds = @{}
        foreach ($node in $nodes) {
            $properties = @($node.SelectNodes("*[local-name()='properties']/*[local-name()='property']"))
            $uidProperties = @($properties | Where-Object { $_.GetAttribute("name") -ceq "uid" })
            if ($uidProperties.Count -ne 1) { throw "JUnit testcase must have exactly one uid property." }
            $uid = Get-ReportAttribute $uidProperties[0] "value"
            $outcomes = @($node.SelectNodes("*[local-name()='failure' or local-name()='error' or local-name()='skipped']"))
            if ($outcomes.Count -gt 1) { throw "JUnit result '$uid' has multiple outcomes." }
            $kind = if ($outcomes.Count -eq 0) { "passed" } else { $outcomes[0].LocalName }
            $kinds[$kind] = 1 + [int]$kinds[$kind]
            $traits = [Collections.Generic.List[string]]::new()
            $reserved = @{}
            foreach ($property in $properties) {
                $name = Get-ReportAttribute $property "name"
                $value = $property.GetAttribute("value")
                if ($name.StartsWith("trait.", [StringComparison]::Ordinal)) {
                    $traits.Add((ConvertTo-Json -InputObject @((ConvertTo-ReportXmlText $name.Substring(6)), (ConvertTo-ReportXmlText $value)) -Compress))
                } elseif ($name -cin @("testpath", "original-name", "attempt-index", "attempt-of")) {
                    if ($reserved.ContainsKey($name)) { throw "Repeated JUnit property '$name' for '$uid'." }
                    $reserved[$name] = $value
                }
            }
            $name = Get-ReportAttribute $node "name"
            if ($reserved.ContainsKey("original-name")) {
                $attempt = ConvertTo-ReportNumber $reserved["attempt-index"] "JUnit '$uid' attempt-index"
                $total = ConvertTo-ReportNumber $reserved["attempt-of"] "JUnit '$uid' attempt-of"
                if ($attempt -lt 1 -or $attempt -gt $total -or $attempt -ne [Math]::Floor($attempt)) { throw "Invalid JUnit attempt for '$uid'." }
                Assert-ReportEqual "$($reserved['original-name']) [attempt $attempt]" $name "JUnit '$uid' attempt name"
                $name = $reserved["original-name"]
            }
            $test = [pscustomobject]@{
                Id = $uid
                Name = $name
                Class = Get-ReportAttribute $node "classname"
                Kind = $kind
                Duration = 1000 * (ConvertTo-ReportNumber (Get-ReportAttribute $node "time") "JUnit '$uid' duration")
                Path = [string]$reserved["testpath"]
                Message = if ($outcomes.Count -eq 1) { $outcomes[0].GetAttribute("message") } else { "" }
                ExceptionType = if ($outcomes.Count -eq 1) { $outcomes[0].GetAttribute("type") } else { "" }
                Body = if ($outcomes.Count -eq 1) { $outcomes[0].InnerText } else { "" }
                StdOut = if ($null -ne ($stream = $node.SelectSingleNode("*[local-name()='system-out']"))) { ConvertTo-ReportStream $stream.InnerText } else { "0 lines: " }
                StdErr = if ($null -ne ($stream = $node.SelectSingleNode("*[local-name()='system-err']"))) { ConvertTo-ReportStream $stream.InnerText } else { "0 lines: " }
                Traits = ConvertTo-ReportTraits $traits.ToArray()
            }
            if (-not $tests.TryAdd($uid, $test)) { throw "Ambiguous repeated JUnit UID '$uid'." }
        }
        Assert-ReportEqual $nodes.Count (ConvertTo-ReportNumber (Get-ReportAttribute $suite "tests") "JUnit suite tests") "JUnit suite tests counter"
        foreach ($pair in @(@("failure", "failures"), @("error", "errors"), @("skipped", "skipped"))) {
            Assert-ReportEqual ([int]$kinds[$pair[0]]) (ConvertTo-ReportNumber (Get-ReportAttribute $suite $pair[1]) "JUnit suite $($pair[1])") "JUnit suite $($pair[1]) counter"
        }
        Assert-JUnitAggregateTiming $suite
    }
    Assert-ReportEqual $tests.Count $document.SelectNodes("//*[local-name()='testcase']").Count "JUnit flat suite/result count"
    if ($root.LocalName -ceq "testsuites") {
        Assert-ReportEqual $tests.Count (ConvertTo-ReportNumber (Get-ReportAttribute $root "tests") "JUnit total tests") "JUnit total tests counter"
        foreach ($pair in @(@("failure", "failures"), @("error", "errors"), @("skipped", "skipped"))) {
            $count = @($tests.Values | Where-Object Kind -CEQ $pair[0]).Count
            Assert-ReportEqual $count (ConvertTo-ReportNumber (Get-ReportAttribute $root $pair[1]) "JUnit total $($pair[1])") "JUnit total $($pair[1]) counter"
        }
        Assert-JUnitAggregateTiming $root
    }
    return $tests
}

function Compare-TestReportSet {
    param([string]$TrxPath, [string]$CtrfPath, [string]$JUnitPath, [decimal]$ToleranceMilliseconds = 1)

    $trx = Read-TrxComparisonReport $TrxPath
    $ctrf = Read-CtrfComparisonReport $CtrfPath
    $junit = Read-JUnitComparisonReport $JUnitPath
    Assert-ReportEqual $ctrf.Count $trx.Tests.Count "TRX/CTRF test count"
    Assert-ReportEqual $ctrf.Count $junit.Count "JUnit/CTRF test count"
    $nameIndex = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($test in $trx.Tests.Values) {
        $name = ConvertTo-ReportXmlText $test.Name -Trx
        $class = ConvertTo-ReportXmlText $test.Class -Trx
        $prefix = $name.Substring(0, [Math]::Min(256, $name.Length))
        foreach ($key in @("name:$name", "prefix:$prefix", "class:$($class.Length):$class$name")) {
            if (-not $nameIndex.ContainsKey($key)) { $nameIndex[$key] = [Collections.Generic.List[object]]::new() }
            $nameIndex[$key].Add($test)
        }
    }
    $matchedTrx = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $withoutTimestamps = 0
    foreach ($test in $ctrf.Values) {
        $context = "Test $(Format-ReportValue $test.Id 100) ($(Format-ReportValue $test.Name 100))"
        if (-not $junit.ContainsKey($test.JUnitId)) { throw "$context is missing from JUnit." }
        $junitTest = $junit[$test.JUnitId]
        $guid = [guid]::Empty
        if ([guid]::TryParse($test.Id, [ref]$guid)) {
            $id = $guid.ToString("D")
            if (-not $trx.Tests.ContainsKey($id)) { throw "$context is missing from TRX." }
            $trxTest = $trx.Tests[$id]
        } else {
            # TRX hashes non-GUID UIDs. Do not guess when that format lost correlation.
            $name = if ($test.Name.Length -gt 4096) {
                ConvertTo-ReportXmlText (Get-ReportCaptureInfo $test.Name).Prefix -Trx
            } else {
                ConvertTo-ReportXmlText $test.Name -Trx
            }
            $class = ConvertTo-ReportXmlText $test.Class -Trx
            $key = if ($test.Name.Length -gt 4096) {
                "prefix:" + $name.Substring(0, [Math]::Min(256, $name.Length))
            } elseif ($null -eq $test.Class -or $test.HasTruncatedClass) {
                "name:$name"
            } else {
                "class:$($class.Length):$class$name"
            }
            $candidates = @(if ($nameIndex.ContainsKey($key)) {
                foreach ($candidate in $nameIndex[$key]) {
                    if (-not (Test-TrxCapturedName $test.Name $candidate.Name)) { continue }
                    if ($null -ne $test.Suite -and -not (Test-ReportClassCapture $test.Suite $candidate.Class)) { continue }
                    $candidate
                }
            })
            if ($candidates.Count -ne 1) { throw "$context has $($candidates.Count) TRX name/class matches; correlation is missing or ambiguous." }
            $trxTest = $candidates[0]
        }
        if (-not $matchedTrx.Add($trxTest.Id)) { throw "$context reuses a TRX result already matched to another UID." }
        if (-not (Test-TrxCapturedName $test.Name $trxTest.Name)) {
            throw "$context TRX display name: expected $(Format-ReportValue (ConvertTo-ReportXmlText $test.Name -Trx)); got $(Format-ReportValue $trxTest.Name)."
        }
        if ($null -ne $test.Class) {
            if (-not (Test-ReportClassCapture $test.Suite $trxTest.Class)) {
                throw "$context TRX class: expected $(Format-ReportValue $test.Class); got $(Format-ReportValue $trxTest.Class)."
            }
            Assert-ReportEqual (ConvertTo-ReportXmlText (Limit-ReportCapturedText $trxTest.Class)) (ConvertTo-ReportXmlText $junitTest.Class) "$context JUnit class"
        }
        $method = if ($null -ne $test.Method) { $test.Method } else { $test.Name }
        if ($null -ne $test.Method) {
            Assert-ReportEqual (ConvertTo-ReportXmlText $method -Trx) (ConvertTo-ReportXmlText (Limit-ReportCapturedText $trxTest.Method) -Trx) "$context TRX method"
        }
        Assert-ReportEqual (ConvertTo-ReportXmlText $method) (ConvertTo-ReportXmlText $junitTest.Name) "$context JUnit method name"
        $displayName = ConvertTo-ReportXmlText $test.Name
        $testPath = ConvertTo-ReportXmlText $junitTest.Path
        if ($testPath -cne $displayName -and -not $testPath.EndsWith("/$displayName", [StringComparison]::Ordinal)) {
            throw "$context JUnit testpath does not preserve its display name."
        }
        Assert-ReportEqual $test.Outcome $trxTest.Outcome "$context TRX outcome"
        Assert-ReportEqual $test.Kind $junitTest.Kind "$context JUnit outcome"
        Assert-ReportTiming $trxTest.Duration $test.Duration $ToleranceMilliseconds "$context TRX/CTRF duration"
        Assert-ReportTiming $trxTest.Duration $junitTest.Duration $ToleranceMilliseconds "$context TRX/JUnit duration"
        Assert-ReportTiming $test.Duration $junitTest.Duration $ToleranceMilliseconds "$context CTRF/JUnit duration"
        if ($null -ne $test.Start) {
            $epoch = [decimal][DateTimeOffset]::UnixEpoch.UtcTicks / [TimeSpan]::TicksPerMillisecond
            Assert-ReportTiming ($trxTest.Start - $epoch) $test.Start $ToleranceMilliseconds "$context start timestamp"
            Assert-ReportTiming ($trxTest.Stop - $epoch) $test.Stop $ToleranceMilliseconds "$context stop timestamp"
        } else {
            $withoutTimestamps++
        }
        if ($test.Kind -cne "passed") {
            Assert-ReportEqual (ConvertTo-ReportXmlText $test.Message) (ConvertTo-ReportXmlText $junitTest.Message) "$context JUnit diagnostic message"
        }
        if ($test.Kind -cin @("failure", "error")) {
            $body = $test.ExceptionType
            if ($body.Length -gt 0 -and $test.Message.Length -gt 0) { $body += ": " }
            $body += $test.Message
            if ($test.Trace.Length -gt 0) {
                if ($body.Length -gt 0) { $body += "`n" }
                $body += $test.Trace
            }
            $type = if ($test.ExceptionType.Length -gt 0) { $test.ExceptionType } else { $test.Kind }
            Assert-ReportEqual (ConvertTo-ReportXmlText $type) $junitTest.ExceptionType "$context JUnit exception type"
            Assert-ReportEqual (ConvertTo-ReportXmlText $body) (ConvertTo-ReportXmlText $junitTest.Body) "$context JUnit diagnostic body"
        }
        Assert-ReportEqual $test.StdOut (ConvertTo-ReportXmlText $junitTest.StdOut) "$context JUnit stdout"
        Assert-ReportEqual $test.StdErr (ConvertTo-ReportXmlText $junitTest.StdErr) "$context JUnit stderr"
        Assert-ReportEqual $test.Traits $junitTest.Traits "$context JUnit traits"
    }
    Assert-ReportEqual @($ctrf.Values | Where-Object RawStatus -CEQ "timedOut").Count $trx.Timeouts "TRX/CTRF timeout count"
    return [pscustomobject]@{ Tests = $ctrf.Count; WithoutTimestamps = $withoutTimestamps }
}

function Get-TestReportInput {
    param([string]$Directory)

    $inputs = [Collections.Generic.List[object]]::new()
    $expectedXmlStems = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File | Sort-Object Name) {
        foreach ($format in @(@(".ctrf.json", "CtrfPath"), @(".trx", "TrxPath"), @(".xml", "JUnitPath"))) {
            if ($file.Name.EndsWith($format[0], [StringComparison]::OrdinalIgnoreCase)) {
                $stem = $file.Name.Substring(0, $file.Name.Length - $format[0].Length)
                $inputs.Add([pscustomobject]@{ Path = $file.FullName; Stem = $stem; Kind = $format[1] })
                if ($format[1] -cne "JUnitPath") { $null = $expectedXmlStems.Add($stem) }
                break
            }
        }
    }
    foreach ($reportInput in $inputs) {
        if ($reportInput.Kind -ceq "JUnitPath" -and -not $expectedXmlStems.Contains($reportInput.Stem)) {
            $document = Read-ReportXml $reportInput.Path
            if ($document.DocumentElement.LocalName -cnotin @("testsuite", "testsuites")) { continue }
        }
        $reportInput
    }
}

function Invoke-TestReportComparison {
    param([string]$ResultsDirectory, [string]$TrxPath, [string]$CtrfPath, [string]$JUnitPath, [decimal]$ToleranceMilliseconds = 1)

    $sets = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    if ($ResultsDirectory) {
        if ($TrxPath -or $CtrfPath -or $JUnitPath) { throw "Use a results directory or three explicit files, not both." }
        if (-not (Test-Path -LiteralPath $ResultsDirectory -PathType Container)) { throw "Results directory does not exist: '$ResultsDirectory'." }
        foreach ($reportInput in Get-TestReportInput -Directory $ResultsDirectory) {
            $stem = $reportInput.Stem
            $kind = $reportInput.Kind
            if (-not $sets.ContainsKey($stem)) { $sets.Add($stem, @{ TrxPath = ""; CtrfPath = ""; JUnitPath = "" }) }
            if ($sets[$stem][$kind]) { throw "Duplicate $kind report for '$stem'." }
            $sets[$stem][$kind] = $reportInput.Path
        }
        if ($sets.Count -eq 0) { throw "No report sets found in '$ResultsDirectory'." }
    } else {
        $sets.Add("explicit files", @{ TrxPath = $TrxPath; CtrfPath = $CtrfPath; JUnitPath = $JUnitPath })
    }
    $failures = [Collections.Generic.List[string]]::new()
    $total = 0
    foreach ($entry in $sets.GetEnumerator()) {
        try {
            foreach ($format in @("TrxPath", "CtrfPath", "JUnitPath")) {
                if (-not $entry.Value[$format] -or -not (Test-Path -LiteralPath $entry.Value[$format] -PathType Leaf)) {
                    throw "Missing $format report."
                }
            }
            $result = Compare-TestReportSet -TrxPath $entry.Value.TrxPath -CtrfPath $entry.Value.CtrfPath -JUnitPath $entry.Value.JUnitPath -ToleranceMilliseconds $ToleranceMilliseconds
            $total += $result.Tests
            Write-Output "PASS '$($entry.Key)': $($result.Tests) tests agree; $($result.WithoutTimestamps) without optional timestamps."
        } catch {
            $failures.Add("Report set '$($entry.Key)' [TRX=$($entry.Value.TrxPath); CTRF=$($entry.Value.CtrfPath); JUnit=$($entry.Value.JUnitPath)]: $($_.Exception.Message)")
        }
    }
    if ($failures.Count -gt 0) {
        throw "Test report comparison failed for $($failures.Count) set(s):`n$($failures -join "`n")"
    }
    Write-Output "Compared $($sets.Count) report set(s), $total tests: TRX, CTRF, and JUnit agree."
}

if ($MyInvocation.InvocationName -ne ".") {
    Invoke-TestReportComparison @PSBoundParameters
}
