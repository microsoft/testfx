#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$ComparatorScript = (Join-Path $PSScriptRoot "compare-test-reports.ps1")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$ComparatorScript = [IO.Path]::GetFullPath($ComparatorScript)
. $ComparatorScript

function Assert-TestCondition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function New-ComparisonFixture {
    param([string]$Directory)

    New-Item -ItemType Directory -Path $Directory | Out-Null
    $trx = [xml]@'
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="11111111-1111-1111-1111-111111111111" executionId="first" testName="Row &quot;one&quot; &lt;&amp;&gt; / alpha" outcome="Passed" duration="00:00:01.2349000" startTime="2026-11-10T12:00:00+00:00" endTime="2026-11-10T12:00:01.2349000+00:00" />
    <UnitTestResult testId="22222222-2222-2222-2222-222222222222" executionId="second" testName="Row two" outcome="Passed" duration="00:00:03.0000100" startTime="2026-11-10T12:00:00+00:00" endTime="2026-11-10T12:00:03.0000100+00:00" />
    <UnitTestResult testId="33333333-3333-3333-3333-333333333333" executionId="third" testName="Skipped" outcome="NotExecuted" duration="00:00:00" startTime="2026-11-10T12:00:00+00:00" endTime="2026-11-10T12:00:00+00:00" />
    <UnitTestResult testId="44444444-4444-4444-4444-444444444444" executionId="fourth" testName="Failed" outcome="Failed" duration="00:00:00.0100001" startTime="2026-11-10T12:00:00+00:00" endTime="2026-11-10T12:00:00.0100001+00:00" />
  </Results>
  <TestDefinitions>
    <UnitTest id="11111111-1111-1111-1111-111111111111"><TestMethod className="Example.Tests" name="Data" /></UnitTest>
    <UnitTest id="22222222-2222-2222-2222-222222222222"><TestMethod className="Example.Tests" name="Data" /></UnitTest>
    <UnitTest id="33333333-3333-3333-3333-333333333333"><TestMethod className="Example.Tests" name="Skipped" /></UnitTest>
    <UnitTest id="44444444-4444-4444-4444-444444444444"><TestMethod className="Example.Tests" name="Failed" /></UnitTest>
  </TestDefinitions>
  <ResultSummary><Counters total="4" executed="3" passed="2" failed="1" timeout="0" notExecuted="1" /></ResultSummary>
</TestRun>
'@
    $ctrf = @'
{
  "reportFormat": "CTRF",
  "specVersion": "0.2.0",
  "results": {
    "summary": { "tests": 4, "passed": 2, "failed": 1, "skipped": 1, "pending": 0, "other": 0 },
    "tests": [
      { "name": "Row \"one\" <&> / alpha", "status": "passed", "duration": 1234, "suite": ["Example", "Tests"],
        "labels": { "TestCategory": ["Fast", "Reports"], "Owner": "Team" },
        "extra": { "uid": "11111111-1111-1111-1111-111111111111", "method": "Data" } },
      { "name": "Row two", "status": "passed", "duration": 3000, "suite": ["Example", "Tests"],
        "extra": { "uid": "22222222-2222-2222-2222-222222222222", "method": "Data" } },
      { "name": "Skipped", "status": "skipped", "duration": 0, "suite": ["Example", "Tests"], "message": "Not supported.",
        "extra": { "uid": "33333333-3333-3333-3333-333333333333", "method": "Skipped" } },
      { "name": "Failed", "status": "failed", "duration": 10, "suite": ["Example", "Tests"],
        "message": "Expected <1> & \"two\".", "trace": "   at Example.Tests.Failed()", "stdout": ["first", "second", ""], "stderr": ["error"],
        "extra": { "uid": "44444444-4444-4444-4444-444444444444", "method": "Failed", "exceptionType": "Example.Failure" } }
    ]
  }
}
'@ | ConvertFrom-Json -AsHashtable
    $ctrf.results.tests[0].start = [DateTimeOffset]::Parse("2026-11-10T12:00:00+00:00").ToUnixTimeMilliseconds()
    $ctrf.results.tests[0].stop = $ctrf.results.tests[0].start + 1234
    $junit = [xml]@'
<?xml version="1.0" encoding="utf-8"?>
<testsuites name="Example" tests="4" failures="1" errors="0" skipped="1" time="4.245">
  <testsuite name="Example.Tests" tests="4" failures="1" errors="0" skipped="1" time="4.245">
    <testcase name="Data [attempt 2]" classname="Example.Tests" time="3.000">
      <properties><property name="uid" value="22222222-2222-2222-2222-222222222222" /><property name="testpath" value="Root/Row two" /><property name="original-name" value="Data" /><property name="attempt-index" value="2" /><property name="attempt-of" value="2" /></properties>
    </testcase>
    <testcase name="Data [attempt 1]" classname="Example.Tests" time="1.235">
      <properties>
        <property name="uid" value="11111111-1111-1111-1111-111111111111" /><property name="testpath" value="Root/Row &quot;one&quot; &lt;&amp;&gt; / alpha" />
        <property name="original-name" value="Data" /><property name="attempt-index" value="1" /><property name="attempt-of" value="2" />
        <property name="trait.Owner" value="Team" /><property name="trait.TestCategory" value="Reports" /><property name="trait.TestCategory" value="Fast" />
      </properties>
    </testcase>
    <testcase name="Skipped" classname="Example.Tests" time="0.000">
      <properties><property name="uid" value="33333333-3333-3333-3333-333333333333" /><property name="testpath" value="Skipped" /></properties>
      <skipped message="Not supported." />
    </testcase>
    <testcase name="Failed" classname="Example.Tests" time="0.010">
      <properties><property name="uid" value="44444444-4444-4444-4444-444444444444" /><property name="testpath" value="Failed" /></properties>
      <failure message="Expected &lt;1&gt; &amp; &quot;two&quot;." type="Example.Failure">Example.Failure: Expected &lt;1&gt; &amp; "two".
   at Example.Tests.Failed()</failure>
      <system-out>first
second

</system-out><system-err>error</system-err>
    </testcase>
  </testsuite>
</testsuites>
'@
    return [pscustomobject]@{
        Directory = $Directory
        TrxPath = Join-Path $Directory "module.trx"
        CtrfPath = Join-Path $Directory "module.ctrf.json"
        JUnitPath = Join-Path $Directory "module.xml"
        Trx = $trx
        Ctrf = $ctrf
        JUnit = $junit
    }
}

function Save-ComparisonFixture {
    param([object]$Fixture)
    $Fixture.Trx.Save($Fixture.TrxPath)
    $Fixture.JUnit.Save($Fixture.JUnitPath)
    ConvertTo-Json -InputObject $Fixture.Ctrf -Depth 30 | Set-Content -LiteralPath $Fixture.CtrfPath -Encoding utf8
}

function Set-EmptyComparisonFixture {
    param([object]$Fixture)

    $Fixture.Trx.TestRun.Results.RemoveAll()
    $Fixture.Trx.TestRun.TestDefinitions.RemoveAll()
    foreach ($name in @("total", "executed", "passed", "failed", "timeout", "notExecuted")) {
        $Fixture.Trx.TestRun.ResultSummary.Counters.SetAttribute($name, "0")
    }
    $Fixture.Ctrf.results.tests = @()
    foreach ($name in @("tests", "passed", "failed", "skipped", "pending", "other")) { $Fixture.Ctrf.results.summary[$name] = 0 }
    $suite = $Fixture.JUnit.testsuites.testsuite
    $suite.RemoveAll()
    foreach ($node in @($Fixture.JUnit.DocumentElement, $suite)) {
        foreach ($name in @("tests", "failures", "errors", "skipped", "time")) { $node.SetAttribute($name, "0") }
    }
}

function Invoke-PipelineReportProbe {
    param([string]$Template, [string]$Directory)

    $repository = Split-Path $PSScriptRoot -Parent
    $text = Get-Content -LiteralPath (Join-Path $repository "eng" -AdditionalChildPath @("pipelines", "steps", $Template)) -Raw
    $match = [regex]::Match($text, "(?ms)^(?<indent> *)- pwsh: \|\r?\n(?<body>(?:(?!^\k<indent>  displayName:).)*)^\k<indent>  displayName: 'Check for Test Results folders'")
    if (-not $match.Success) { throw "Missing report-directory probe in $Template." }
    $body = [regex]::Replace($match.Groups["body"].Value, '(?m)^ +\$testResults = .+$', [Text.RegularExpressions.MatchEvaluator]{
        param($assignment)
        return "`$testResults = '$($Directory.Replace("'", "''"))'"
    })
    $body = $body.Replace(". ./eng/compare-test-reports.ps1", ". '$($ComparatorScript.Replace("'", "''"))'")
    $body = $body.Replace("Write-Host", "Write-Output")
    & ([scriptblock]::Create($body))
}

function Invoke-FixtureComparison {
    param([object]$Fixture, [decimal]$Tolerance = 1)
    Save-ComparisonFixture $Fixture
    return Compare-TestReportSet -TrxPath $Fixture.TrxPath -CtrfPath $Fixture.CtrfPath -JUnitPath $Fixture.JUnitPath -ToleranceMilliseconds $Tolerance
}

function Assert-FixtureRejected {
    param([object]$Fixture, [string]$Diagnostic)
    try {
        Invoke-FixtureComparison $Fixture | Out-Null
    } catch {
        if ($_.Exception.Message.Contains($Diagnostic, [StringComparison]::Ordinal)) { return }
        throw "Expected diagnostic '$Diagnostic', got: $($_.Exception.Message)"
    }
    throw "Expected rejection containing '$Diagnostic', but the comparison accepted inconsistent reports."
}

function Assert-DirectoryRejected {
    param([object]$Fixture, [string]$Diagnostic)
    try {
        Invoke-TestReportComparison -ResultsDirectory $Fixture.Directory | Out-Null
    } catch {
        if ($_.Exception.Message.Contains($Diagnostic, [StringComparison]::Ordinal)) { return }
        throw "Expected diagnostic '$Diagnostic', got: $($_.Exception.Message)"
    }
    throw "Expected rejection containing '$Diagnostic', but directory comparison succeeded."
}

$cases = [ordered]@{
    EquivalentRepresentationsAreAccepted = {
        param($f)
        $result = Invoke-FixtureComparison $f
        Assert-TestCondition ($result.Tests -eq 4 -and $result.WithoutTimestamps -eq 3) "All four reordered rows, escaped names, outcomes, diagnostics and traits must agree."
    }
    OneMillisecondBoundaryIsAccepted = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("duration", "00:00:01.2350000")
        $null = Invoke-FixtureComparison $f
    }
    DurationOutsideToleranceIsRejected = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("duration", "00:00:01.2350001")
        Assert-FixtureRejected $f "TRX/CTRF duration"
    }
    JUnitSecondsAreNotMilliseconds = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("time", "3000")
        $f.JUnit.testsuites.SetAttribute("time", "3001.245")
        $f.JUnit.testsuites.testsuite.SetAttribute("time", "3001.245")
        Assert-FixtureRejected $f "TRX/JUnit duration"
    }
    ExactToleranceCanBeRequested = {
        param($f)
        Save-ComparisonFixture $f
        try {
            Compare-TestReportSet $f.TrxPath $f.CtrfPath $f.JUnitPath -ToleranceMilliseconds 0 | Out-Null
        } catch {
            if ($_.Exception.Message.Contains("TRX/CTRF duration", [StringComparison]::Ordinal)) { return }
            throw
        }
        throw "Zero tolerance must reject rounded durations."
    }
    NegativeDurationIsRejected = {
        param($f)
        $f.Ctrf.results.tests[0].duration = -1
        Assert-FixtureRejected $f "finite nonnegative number"
    }
    NonFiniteDurationIsRejected = {
        param($f)
        $f.Ctrf.results.tests[0].duration = "NaN"
        Assert-FixtureRejected $f "finite nonnegative number"
    }
    MissingDurationIsRejected = {
        param($f)
        $null = $f.Ctrf.results.tests[0].Remove("duration")
        Assert-FixtureRejected $f "Missing required JSON property 'duration'"
    }
    TrxTimingReversalIsRejected = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("endTime", "2026-11-10T11:00:00Z")
        Assert-FixtureRejected $f "invalid timing"
    }
    CtrfTimingReversalIsRejected = {
        param($f)
        $f.Ctrf.results.tests[0].stop = $f.Ctrf.results.tests[0].start - 1
        Assert-FixtureRejected $f "stops before it starts"
    }
    TimestampDriftIsRejected = {
        param($f)
        $f.Ctrf.results.tests[0].start += 1000
        $f.Ctrf.results.tests[0].stop += 1000
        Assert-FixtureRejected $f "start timestamp"
    }
    OptionalTimingAbsenceIsAccepted = {
        param($f)
        $null = $f.Ctrf.results.tests[0].Remove("start")
        $null = $f.Ctrf.results.tests[0].Remove("stop")
        $result = Invoke-FixtureComparison $f
        Assert-TestCondition ($result.WithoutTimestamps -eq 4) "Unavailable timestamps must be counted, not invented."
    }
    MissingStopIsRejected = {
        param($f)
        $null = $f.Ctrf.results.tests[0].Remove("stop")
        Assert-FixtureRejected $f "finite nonnegative number"
    }
    SwappedNamesAreRejected = {
        param($f)
        $f.Ctrf.results.tests[0].name = "Row two"
        $f.Ctrf.results.tests[1].name = 'Row "one" <&> / alpha'
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", 'Root/Row "one" <&> / alpha')
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[1].SetAttribute("value", "Root/Row two")
        Assert-FixtureRejected $f "TRX display name"
    }
    DuplicateDisplayNamesRemainDistinct = {
        param($f)
        $f.Ctrf.results.tests[0].name = "Row two"
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("testName", "Row two")
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[1].SetAttribute("value", "Root/Row two")
        $null = Invoke-FixtureComparison $f
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("time", "1.235")
        $f.JUnit.testsuites.testsuite.testcase[1].SetAttribute("time", "3.000")
        Assert-FixtureRejected $f "TRX/JUnit duration"
    }
    ClassesAreCompared = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("classname", "Other.Tests")
        Assert-FixtureRejected $f "JUnit class"
    }
    MethodNamesAreCompared = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("name", "Other [attempt 2]")
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[2].SetAttribute("value", "Other")
        Assert-FixtureRejected $f "JUnit method name"
    }
    WrongDisplayPathIsRejected = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", "Root/Other row")
        Assert-FixtureRejected $f "JUnit testpath"
    }
    WrongUidCannotMatchByOrder = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "unknown")
        Assert-FixtureRejected $f "is missing from JUnit"
    }
    NonGuidUidUsesUnambiguousNameClassCorrelation = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "named-case"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "named-case")
        $null = Invoke-FixtureComparison $f
    }
    NonGuidMissingTrxCorrelationIsRejected = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "named-case"
        $f.Ctrf.results.tests[1].name = "Missing row"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "named-case")
        Assert-FixtureRejected $f "0 TRX name/class matches"
    }
    MissingOptionalIdentifierMetadataIsAccepted = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "native-row"
        $null = $f.Ctrf.results.tests[1].Remove("suite")
        $null = $f.Ctrf.results.tests[1].extra.Remove("method")
        $f.Trx.TestRun.TestDefinitions.UnitTest[1].TestMethod.SetAttribute("name", "Row two")
        $node = $f.JUnit.testsuites.testsuite.testcase[0]
        $node.SetAttribute("name", "Row two")
        $node.SetAttribute("classname", "ModuleFallback")
        $node.properties.property[0].SetAttribute("value", "native-row")
        foreach ($property in @($node.properties.property | Where-Object { $_.GetAttribute("name") -in @("original-name", "attempt-index", "attempt-of") })) {
            $null = $property.ParentNode.RemoveChild($property)
        }
        $null = Invoke-FixtureComparison $f
    }
    UidsAreCaseSensitive = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "named-case"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "NAMED-CASE")
        Assert-FixtureRejected $f "is missing from JUnit"
    }
    DateShapedStringsRemainStrings = {
        param($f)
        $name = "2026-11-11T03:04:05Z"
        $uid = "2026-11-12T03:04:05Z"
        $message = "2026-11-13T03:04:05Z"
        $trait = "2026-11-14T03:04:05Z"
        $f.Ctrf.results.tests[0].name = $name
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("testName", $name)
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[1].SetAttribute("value", "Root/$name")
        $f.Ctrf.results.tests[1].extra.uid = $uid
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", $uid)
        $f.Ctrf.results.tests[3].message = $message
        $f.JUnit.testsuites.testsuite.testcase[3].failure.SetAttribute("message", $message)
        $f.JUnit.testsuites.testsuite.testcase[3].failure.InnerText = "Example.Failure: $message`n   at Example.Tests.Failed()"
        $f.Ctrf.results.tests[0].labels.Owner = $trait
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[5].SetAttribute("value", $trait)
        $f.Ctrf.results.tests[3].stdout = @($name)
        $f.JUnit.testsuites.testsuite.testcase[3]."system-out" = $name
        $null = Invoke-FixtureComparison $f
    }
    JsonMetadataLikeTraitNamesRemainData = {
        param($f)
        $labels = [ordered]@{ '$type' = "NotAType"; '$id' = "literal-id"; '$ref' = "literal-reference" }
        $f.Ctrf.results.tests[0].labels = $labels
        $properties = $f.JUnit.testsuites.testsuite.testcase[1].properties
        foreach ($property in @($properties.property | Where-Object { $_.GetAttribute("name").StartsWith("trait.", [StringComparison]::Ordinal) })) {
            $null = $properties.RemoveChild($property)
        }
        foreach ($key in $labels.Keys) {
            $property = $f.JUnit.CreateElement("property")
            $property.SetAttribute("name", "trait.$key")
            $property.SetAttribute("value", $labels[$key])
            $null = $properties.AppendChild($property)
        }
        $null = Invoke-FixtureComparison $f
    }
    XmlInvalidUidIsCorrelated = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "row$([char]12)two"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "row$([char]0xFFFD)two")
        $null = Invoke-FixtureComparison $f
    }
    SanitizedUidCollisionsAreRejected = {
        param($f)
        $f.Ctrf.results.tests[0].extra.uid = "row$([char]12)one"
        $f.Ctrf.results.tests[1].extra.uid = "row$([char]0xFFFD)one"
        foreach ($node in @($f.JUnit.testsuites.testsuite.testcase[0], $f.JUnit.testsuites.testsuite.testcase[1])) {
            $node.properties.property[0].SetAttribute("value", "row$([char]0xFFFD)one")
        }
        Assert-FixtureRejected $f "Ambiguous"
    }
    UidLineEndingsRemainDistinct = {
        param($f)
        $f.Ctrf.results.tests[0].extra.uid = "line`r`nuid"
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[0].SetAttribute("value", "line`r`nuid")
        $f.Ctrf.results.tests[1].extra.uid = "line`nuid"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "line`nuid")
        $null = Invoke-FixtureComparison $f
    }
    AmbiguousNonGuidNamesAreRejected = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = "named-case"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "named-case")
        $f.Trx.TestRun.Results.UnitTestResult[0].SetAttribute("testName", "Row two")
        $f.Ctrf.results.tests[0].name = "Row two"
        $f.JUnit.testsuites.testsuite.testcase[1].properties.property[1].SetAttribute("value", "Root/Row two")
        Assert-FixtureRejected $f "2 TRX name/class matches"
    }
    RepeatedCtrfUidIsRejected = {
        param($f)
        $f.Ctrf.results.tests[1].extra.uid = $f.Ctrf.results.tests[0].extra.uid
        Assert-FixtureRejected $f "Ambiguous repeated CTRF UID"
    }
    RepeatedJUnitUidIsRejected = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "11111111-1111-1111-1111-111111111111")
        Assert-FixtureRejected $f "Ambiguous repeated JUnit UID"
    }
    RepeatedTrxIdIsRejected = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testId", "11111111-1111-1111-1111-111111111111")
        Assert-FixtureRejected $f "Ambiguous repeated TRX test ID"
    }
    MissingResultIsRejected = {
        param($f)
        $f.Ctrf.results.tests = @($f.Ctrf.results.tests[1..3])
        $f.Ctrf.results.summary.tests = 3
        $f.Ctrf.results.summary.passed = 1
        Assert-FixtureRejected $f "TRX/CTRF test count"
    }
    WrongOutcomeIsRejected = {
        param($f)
        $f.Ctrf.results.tests[0].status = "failed"
        $f.Ctrf.results.summary.passed = 1
        $f.Ctrf.results.summary.failed = 2
        Assert-FixtureRejected $f "TRX outcome"
    }
    WrongDiagnosticMessageIsRejected = {
        param($f)
        $f.Ctrf.results.tests[3].message = "Another failure"
        Assert-FixtureRejected $f "JUnit diagnostic message"
    }
    LostTraceIsRejected = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[3].failure.InnerText = 'Example.Failure: Expected <1> & "two".'
        Assert-FixtureRejected $f "JUnit diagnostic body"
    }
    WrongStandardOutputIsRejected = {
        param($f)
        $f.JUnit.testsuites.testsuite.testcase[3]."system-out" = "another row's output"
        Assert-FixtureRejected $f "JUnit stdout"
    }
    BlankOutputLinesArePreserved = {
        param($f)
        $f.Ctrf.results.tests[3].stdout = @()
        $f.JUnit.testsuites.testsuite.testcase[3]."system-out" = "`n"
        Assert-FixtureRejected $f "JUnit stdout"
        $f.Ctrf.results.tests[3].stdout = @("")
        $null = Invoke-FixtureComparison $f
        $f.JUnit.testsuites.testsuite.testcase[3]."system-out" = ""
        Assert-FixtureRejected $f "JUnit stdout"
    }
    BareCarriageReturnOutputIsNormalized = {
        param($f)
        $f.Ctrf.results.tests[3].stdout = @("first`rsecond")
        $f.JUnit.testsuites.testsuite.testcase[3]."system-out" = "first`rsecond`r"
        $null = Invoke-FixtureComparison $f
    }
    LostTraitIsRejected = {
        param($f)
        $property = $f.JUnit.testsuites.testsuite.testcase[1].properties.property[5]
        $null = $property.ParentNode.RemoveChild($property)
        Assert-FixtureRejected $f "JUnit traits"
    }
    XmlControlRepresentationsAreAccepted = {
        param($f)
        $f.Ctrf.results.tests[1].name = "Row$([char]12)two"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", 'Row\u000ctwo')
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", "Root/Row$([char]0xFFFD)two")
        $null = Invoke-FixtureComparison $f
    }
    TruncatedDisplayNamesAreAccepted = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", ("A" * 5000))
        $captured = ("A" * 4096) + "`n$([char]0x2026)[truncated, original length: 5000]"
        $f.Ctrf.results.tests[1].name = $captured
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $captured)
        $null = Invoke-FixtureComparison $f
    }
    BoundaryXmlInvalidNamesAreAccepted = {
        param($f)
        $f.Ctrf.results.tests[1].name = ("A" * 4091) + [char]12
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", (("A" * 4091) + '\u000c'))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", (("A" * 4091) + [char]0xFFFD))
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].extra.uid = "boundary-name-row"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "boundary-name-row")
        $null = Invoke-FixtureComparison $f
    }
    LiteralUnicodeEscapeNamesRemainLiteral = {
        param($f)
        $name = ("A" * 4090) + '\u000c'
        $f.Ctrf.results.tests[1].name = $name
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", $name)
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $name)
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].name = ("A" * 4091) + '\u000' + "`n$([char]0x2026)[truncated, original length: 4097]"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", (("A" * 4091) + '\u000c'))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $f.Ctrf.results.tests[1].name)
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].name = ("A" * 4096) + "`n$([char]0x2026)[truncated, original length: 4102]"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", (("A" * 4096) + '\u000c'))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $f.Ctrf.results.tests[1].name)
        $null = Invoke-FixtureComparison $f
    }
    TruncatedXmlInvalidNamesAreAccepted = {
        param($f)
        $suffix = "`n$([char]0x2026)[truncated, original length: 4122]"
        $f.Ctrf.results.tests[1].name = ("A" * 4091) + [char]12 + ("B" * 4) + $suffix
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", (("A" * 4091) + '\u000c' + ("B" * 30)))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", (("A" * 4091) + [char]0xFFFD + ("B" * 4) + $suffix))
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].extra.uid = "truncated-name-row"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "truncated-name-row")
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].name = ("A" * 4096) + "`n$([char]0x2026)[truncated, original length: 4097]"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", (("A" * 4096) + '\u000c'))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $f.Ctrf.results.tests[1].name)
        $null = Invoke-FixtureComparison $f
    }
    TruncatedNameLengthMismatchIsRejected = {
        param($f)
        $f.Ctrf.results.tests[1].name = ("A" * 4096) + "`n$([char]0x2026)[truncated, original length: 5001]"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", ("A" * 5000))
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $f.Ctrf.results.tests[1].name)
        Assert-FixtureRejected $f "TRX display name"
    }
    LongClassCaptureRepresentationsAreAccepted = {
        param($f)
        $type = "T" * 5000
        $f.Ctrf.results.tests[1].suite = @("Example", (("T" * 4096) + "`n$([char]0x2026)[truncated, original length: 5000]"))
        $f.Trx.TestRun.TestDefinitions.UnitTest[1].TestMethod.SetAttribute("className", "Example.$type")
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("classname", ("Example." + ("T" * 4088) + "`n$([char]0x2026)[truncated, original length: 5008]"))
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.tests[1].extra.uid = "long-class-row"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "long-class-row")
        $null = Invoke-FixtureComparison $f
    }
    LongNamespaceAndTypeCaptureAreAccepted = {
        param($f)
        $namespace = "N" * 5000
        $type = "T" * 5000
        $suffix = "`n$([char]0x2026)[truncated, original length: 5000]"
        $f.Ctrf.results.tests[1].suite = @((("N" * 4096) + $suffix), (("T" * 4096) + $suffix))
        $f.Trx.TestRun.TestDefinitions.UnitTest[1].TestMethod.SetAttribute("className", "$namespace.$type")
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("classname", (("N" * 4096) + "`n$([char]0x2026)[truncated, original length: 10001]"))
        $f.Ctrf.results.tests[1].extra.uid = "long-namespace-row"
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0].SetAttribute("value", "long-namespace-row")
        $null = Invoke-FixtureComparison $f
    }
    LongClassCaptureMismatchIsRejected = {
        param($f)
        $f.Ctrf.results.tests[1].suite = @("Example", (("T" * 4095) + "X`n$([char]0x2026)[truncated, original length: 5000]"))
        $f.Trx.TestRun.TestDefinitions.UnitTest[1].TestMethod.SetAttribute("className", ("Example." + ("T" * 5000)))
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("classname", ("Example." + ("T" * 4088) + "`n$([char]0x2026)[truncated, original length: 5008]"))
        Assert-FixtureRejected $f "TRX class"
    }
    MultilineNamesPreserveLineEndingEquivalence = {
        param($f)
        $f.Ctrf.results.tests[1].name = "Row`r`ntwo"
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", "Row`r`ntwo")
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", "Root/Row`ntwo")
        $null = Invoke-FixtureComparison $f
    }
    LongNamesProduceBoundedDiagnostics = {
        param($f)
        $f.Trx.TestRun.Results.UnitTestResult[1].SetAttribute("testName", ("A" * 5000))
        $captured = ("A" * 4096) + "`n$([char]0x2026)[truncated, original length: 5000]"
        $f.Ctrf.results.tests[1].name = $captured
        $f.JUnit.testsuites.testsuite.testcase[0].properties.property[1].SetAttribute("value", $captured)
        $f.JUnit.testsuites.testsuite.testcase[0].SetAttribute("classname", "Other.Tests")
        Save-ComparisonFixture $f
        try {
            Compare-TestReportSet $f.TrxPath $f.CtrfPath $f.JUnitPath | Out-Null
        } catch {
            Assert-TestCondition ($_.Exception.Message.Contains("JUnit class") -and $_.Exception.Message.Length -lt 500) "Long labels must not hide the actionable field or flood logs."
            return
        }
        throw "The deliberately wrong class must be rejected."
    }
    ExtraFormatSpecificDetailsAreAccepted = {
        param($f)
        $f.Ctrf.results.tests[1].attachments = @(@{ name = "details"; path = "unrelated-format-specific-path" })
        $f.Ctrf.results.tests[1].extra.additionalDetail = "not a shared field"
        $null = Invoke-FixtureComparison $f
    }
    CultureDoesNotChangeNumericParsing = {
        param($f)
        $previous = [Globalization.CultureInfo]::CurrentCulture
        try {
            [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo("fr-FR")
            $null = Invoke-FixtureComparison $f
        } finally {
            [Globalization.CultureInfo]::CurrentCulture = $previous
        }
    }
    IncompleteReportsAreRejected = {
        param($f)
        $f.Ctrf.results.environment = @{ extra = @{ incomplete = $true; runStatus = "aborted" } }
        Assert-FixtureRejected $f "Incomplete CTRF"
    }
    MergedReportsAreRejected = {
        param($f)
        $f.Ctrf.extra = @{ "microsoft.testingplatform" = @{ documentRole = "merged" } }
        Assert-FixtureRejected $f "Merged CTRF"
    }
    RetryCollapsedReportsAreRejected = {
        param($f)
        $f.Ctrf.results.tests[0].retries = 1
        Assert-FixtureRejected $f "Retry-collapsed CTRF"
    }
    MissingJUnitUidIsRejected = {
        param($f)
        $property = $f.JUnit.testsuites.testsuite.testcase[0].properties.property[0]
        $null = $property.ParentNode.RemoveChild($property)
        Assert-FixtureRejected $f "exactly one uid"
    }
    EmptyDirectoryIsRejected = {
        param($f)
        Assert-DirectoryRejected $f "No report sets"
    }
    OrphanReportIsRejected = {
        param($f)
        Save-ComparisonFixture $f
        Copy-Item -LiteralPath $f.JUnitPath -Destination (Join-Path $f.Directory "orphan.xml")
        Assert-DirectoryRejected $f "Missing TrxPath report"
    }
    UnrelatedXmlArtifactIsIgnored = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath (Join-Path $f.Directory "coverage.cobertura.xml") -Value "<coverage><sources /></coverage>"
        $output = @(Invoke-TestReportComparison -ResultsDirectory $f.Directory)
        Assert-TestCondition ($output[-1] -ceq "Compared 1 report set(s), 4 tests: TRX, CTRF, and JUnit agree.") "Non-JUnit XML must not become an orphan test-report set."
    }
    InvalidExpectedJUnitRootIsRejected = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath $f.JUnitPath -Value "<coverage />"
        Assert-DirectoryRejected $f "Expected a JUnit testsuites/testsuite document."
    }
    MalformedOrphanXmlRemainsAnError = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath (Join-Path $f.Directory "broken.xml") -Value "<testsuites>"
        try {
            Invoke-TestReportComparison -ResultsDirectory $f.Directory | Out-Null
        } catch {
            if ($_.Exception.Message.Contains("Unexpected end of file", [StringComparison]::Ordinal) -or
                $_.Exception.Message.Contains("Missing TrxPath report", [StringComparison]::Ordinal)) {
                return
            }
            throw
        }
        throw "Malformed XML must not be silently ignored during report discovery."
    }
    NestedReportsAreNotDoubleCounted = {
        param($f)
        Save-ComparisonFixture $f
        $nested = Join-Path $f.Directory "merged"
        New-Item -ItemType Directory -Path $nested | Out-Null
        Set-Content -LiteralPath (Join-Path $nested "module.ctrf.json") -Value "not a raw report"
        $output = @(Invoke-TestReportComparison -ResultsDirectory $f.Directory)
        Assert-TestCondition ($output[-1] -ceq "Compared 1 report set(s), 4 tests: TRX, CTRF, and JUnit agree.") "Nested merged/retry inputs must not be double counted."
    }
    MultipleModulesAreComparedIndependently = {
        param($f)
        Save-ComparisonFixture $f
        foreach ($extension in @(".trx", ".xml", ".ctrf.json")) {
            Copy-Item -LiteralPath (Join-Path $f.Directory "module$extension") -Destination (Join-Path $f.Directory "other$extension")
        }
        $output = @(Invoke-TestReportComparison -ResultsDirectory $f.Directory)
        Assert-TestCondition ($output[-1] -ceq "Compared 2 report set(s), 8 tests: TRX, CTRF, and JUnit agree.") "Both module sets must be checked, not merged by UID."
        Remove-Item -LiteralPath (Join-Path $f.Directory "other.xml")
        Assert-DirectoryRejected $f "Missing JUnitPath report"
    }
    InputReportsAreNeverChanged = {
        param($f)
        Save-ComparisonFixture $f
        $before = @(Get-ChildItem -LiteralPath $f.Directory -File | Sort-Object Name | Get-FileHash | ForEach-Object Hash) -join ","
        Invoke-TestReportComparison -ResultsDirectory $f.Directory | Out-Null
        $after = @(Get-ChildItem -LiteralPath $f.Directory -File | Sort-Object Name | Get-FileHash | ForEach-Object Hash) -join ","
        Assert-TestCondition ($before -ceq $after) "Comparison must not change its inputs."
    }
    CommandExitsReflectAgreement = {
        param($f)
        Save-ComparisonFixture $f
        $output = & (Join-Path $PSHOME "pwsh") -NoProfile -File $ComparatorScript -TrxPath $f.TrxPath -CtrfPath $f.CtrfPath -JUnitPath $f.JUnitPath 2>&1
        Assert-TestCondition ($LASTEXITCODE -eq 0 -and ($output -join "`n").Contains("4 tests: TRX, CTRF, and JUnit agree.")) "The real command must exit 0 only for agreement: $output"
        $f.Ctrf.results.tests[0].duration = 100
        Save-ComparisonFixture $f
        $output = & (Join-Path $PSHOME "pwsh") -NoProfile -File $ComparatorScript -ResultsDirectory $f.Directory 2>&1
        Assert-TestCondition ($LASTEXITCODE -ne 0 -and ($output -join "`n").Contains("TRX/CTRF duration")) "The real command must preserve the mismatch exit and diagnostic: $output"
    }
    ConsistentZeroResultReportsAreAccepted = {
        param($f)
        Set-EmptyComparisonFixture $f
        $result = Invoke-FixtureComparison $f
        Assert-TestCondition ($result.Tests -eq 0) "Consistent zero-result reports must not invent executions or fail an intentional no-work module."
    }
    ZeroAndNonzeroModulesRemainIndependent = {
        param($f)
        Save-ComparisonFixture $f
        $empty = New-ComparisonFixture (Join-Path $f.Directory "empty-source")
        Set-EmptyComparisonFixture $empty
        Save-ComparisonFixture $empty
        foreach ($extension in @(".trx", ".xml", ".ctrf.json")) {
            Copy-Item -LiteralPath (Join-Path $empty.Directory "module$extension") -Destination (Join-Path $f.Directory "empty$extension")
        }
        $output = @(Invoke-TestReportComparison -ResultsDirectory $f.Directory)
        Assert-TestCondition ($output[-1] -ceq "Compared 2 report set(s), 4 tests: TRX, CTRF, and JUnit agree.") "A zero-result module must neither hide nor duplicate another module's results."
    }
    JUnitAggregateTimingIsValidated = {
        param($f)
        $f.JUnit.testsuites.testsuite.SetAttribute("time", "10")
        Assert-FixtureRejected $f "JUnit aggregate duration"
    }
    WallClockTimeIsNotSummedTestTime = {
        param($f)
        $f.Ctrf.results.summary.start = 1000
        $f.Ctrf.results.summary.stop = 2000
        $f.Ctrf.results.summary.duration = 1000
        $null = Invoke-FixtureComparison $f
        $f.Ctrf.results.summary.duration = 4245
        Assert-FixtureRejected $f "CTRF summary wall-clock duration"
    }
    InvalidDiagnosticTypesAreRejected = {
        param($f)
        $f.Ctrf.results.tests[3].message = @{ unexpected = "not a string" }
        Assert-FixtureRejected $f "JSON property 'message' must be a string"
    }
    IncompleteJUnitIsRejected = {
        param($f)
        $properties = $f.JUnit.CreateElement("properties")
        $incomplete = $f.JUnit.CreateElement("property")
        $incomplete.SetAttribute("name", "incomplete")
        $incomplete.SetAttribute("value", "true")
        $null = $properties.AppendChild($incomplete)
        $null = $f.JUnit.testsuites.testsuite.PrependChild($properties)
        Assert-FixtureRejected $f "Incomplete JUnit"
    }
    MalformedJsonIsRejected = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath $f.CtrfPath -Value "{"
        Assert-DirectoryRejected $f "Conversion from JSON failed"
    }
    MalformedXmlIsRejected = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath $f.TrxPath -Value "<TestRun>"
        Assert-DirectoryRejected $f "Unexpected end of file"
    }
    ExternalEntitiesAreRejected = {
        param($f)
        Save-ComparisonFixture $f
        Set-Content -LiteralPath $f.TrxPath -Value '<!DOCTYPE TestRun [<!ENTITY leak SYSTEM "file:///must-not-be-read">]><TestRun>&leak;</TestRun>'
        Assert-DirectoryRejected $f "DTD is prohibited"
    }
    PipelineWiringPreservesScope = {
        param($f)
        $repository = Split-Path $PSScriptRoot -Parent
        foreach ($template in @("build-test-windows.yml", "test-non-windows.yml")) {
            $text = Get-Content -LiteralPath (Join-Path $repository "eng" -AdditionalChildPath @("pipelines", "steps", $template)) -Raw
            Assert-TestCondition ($text.Contains('./eng/compare-test-reports.ps1 -ResultsDirectory "$(Build.SourcesDirectory)/artifacts/TestResults/$(_BuildConfig)"')) "$template must compare the saved raw reports."
            Assert-TestCondition ($text.Contains("ne(variables._CollectAffectedTests, 'true'), eq(variables.HasTestReports, 'true')")) "$template must skip report-free collection/build failures."
        }
        $windows = Get-Content -LiteralPath (Join-Path $repository "eng" -AdditionalChildPath @("pipelines", "steps", "build-test-windows.yml")) -Raw
        Assert-TestCondition ($windows.Contains('- ${{ if eq(parameters.CollectCTS, false) }}:')) "TRX-only CTS jobs must not run this triplet gate."
        $targets = Get-Content -LiteralPath (Join-Path $repository "test" -AdditionalChildPath @("Directory.Build.targets")) -Raw
        Assert-TestCondition ($targets.Contains('--report-ctrf-filename $(MSBuildProjectName)_$(TargetFramework)_$(Architecture).ctrf.json')) "CTRF must use the same architecture-qualified stem as TRX/JUnit."
        Assert-TestCondition ($targets.Contains('--report-junit-filename $(MSBuildProjectName)_$(TargetFramework)_$(Architecture).xml')) "JUnit must be aligned even when the assembly name differs from the project name."
    }
    NoWorkWithOnlyBinlogIsNotCompared = {
        param($f)
        Set-Content -LiteralPath (Join-Path $f.Directory "TestAffected.binlog") -Value "No test modules selected."
        foreach ($template in @("build-test-windows.yml", "test-non-windows.yml")) {
            $output = Invoke-PipelineReportProbe $template $f.Directory
            Assert-TestCondition ($output -ccontains "##vso[task.setvariable variable=HasTestResults]true") "The binlog directory must still be published."
            Assert-TestCondition ($output -ccontains "##vso[task.setvariable variable=HasTestReports]false") "No-work invocations must skip comparison, not claim report agreement."
        }
    }
    NonJUnitXmlDoesNotEnablePipelineComparison = {
        param($f)
        Set-Content -LiteralPath (Join-Path $f.Directory "extension-settings.xml") -Value "<settings />"
        foreach ($template in @("build-test-windows.yml", "test-non-windows.yml")) {
            $output = Invoke-PipelineReportProbe $template $f.Directory
            Assert-TestCondition ($output -ccontains "##vso[task.setvariable variable=HasTestResults]true") "Unrelated XML artifacts must still be published."
            Assert-TestCondition ($output -ccontains "##vso[task.setvariable variable=HasTestReports]false") "Only actual test reports may enable comparison."
        }
    }
    MalformedXmlProbeStillPublishesArtifacts = {
        param($f)
        Set-Content -LiteralPath (Join-Path $f.Directory "broken.xml") -Value "<testsuites>"
        foreach ($template in @("build-test-windows.yml", "test-non-windows.yml")) {
            $output = [Collections.Generic.List[string]]::new()
            $rejected = $false
            try {
                Invoke-PipelineReportProbe $template $f.Directory | ForEach-Object { $output.Add([string]$_) }
            } catch {
                if (-not $_.Exception.Message.Contains("Unexpected end of file", [StringComparison]::Ordinal)) { throw }
                $rejected = $true
            }
            Assert-TestCondition ($rejected -or $output.Contains("##vso[task.setvariable variable=HasTestReports]true")) "A malformed report must be rejected now or passed to the comparison gate."
            Assert-TestCondition ($output.Contains("##vso[task.setvariable variable=HasTestResults]true")) "Report parsing failure must not suppress publication of the original test artifacts."
        }
    }
    PipelineProbeRecognizesEveryReportFormat = {
        param($f)
        foreach ($extension in @(".trx", ".ctrf.json", ".xml")) {
            $path = Join-Path $f.Directory "orphan$extension"
            if ($extension -ceq ".xml") {
                $f.JUnit.Save($path)
            } else {
                Set-Content -LiteralPath $path -Value "A lone report must trigger validation of its missing partners."
            }
            foreach ($template in @("build-test-windows.yml", "test-non-windows.yml")) {
                $output = Invoke-PipelineReportProbe $template $f.Directory
                Assert-TestCondition ($output -ccontains "##vso[task.setvariable variable=HasTestReports]true") "$extension must trigger comparison even when TRX or another partner is missing."
            }
            Remove-Item -LiteralPath $path
        }
    }
}

$caseData = @{}
foreach ($format in @("TrxPath", "CtrfPath", "JUnitPath")) {
    $caseData["Missing${format}IsRejected"] = $format
    $cases["Missing${format}IsRejected"] = {
        param($f, $missingFormat)
        Save-ComparisonFixture $f
        Remove-Item -LiteralPath $f.$missingFormat
        Assert-DirectoryRejected $f "Missing $missingFormat report"
    }
}

foreach ($counter in @("TRX", "CTRF", "JUnitRoot", "JUnitSuite")) {
    $caseData["${counter}CountersAreChecked"] = $counter
    $cases["${counter}CountersAreChecked"] = {
        param($f, $counterKind)
        switch ($counterKind) {
            "TRX" { $f.Trx.TestRun.ResultSummary.Counters.SetAttribute("total", "5"); $diagnostic = "TRX total counter" }
            "CTRF" { $f.Ctrf.results.summary.passed = 3; $diagnostic = "CTRF 'passed' counter" }
            "JUnitRoot" { $f.JUnit.testsuites.SetAttribute("tests", "5"); $diagnostic = "JUnit total tests counter" }
            "JUnitSuite" { $f.JUnit.testsuites.testsuite.SetAttribute("failures", "2"); $diagnostic = "JUnit suite failures counter" }
        }
        Assert-FixtureRejected $f $diagnostic
    }
}

foreach ($rawStatus in @("timedOut", "errored", "cancelled")) {
    $caseData["${rawStatus}MapsToJUnitErrorAndTrxFailure"] = $rawStatus
    $cases["${rawStatus}MapsToJUnitErrorAndTrxFailure"] = {
        param($f, $status)
        $f.Ctrf.results.tests[3].rawStatus = $status
        if ($status -ceq "cancelled") {
            $f.Ctrf.results.tests[3].status = "other"
            $f.Ctrf.results.summary.failed = 0
            $f.Ctrf.results.summary.other = 1
        }
        if ($status -ceq "timedOut") {
            $f.Trx.TestRun.ResultSummary.Counters.SetAttribute("failed", "0")
            $f.Trx.TestRun.ResultSummary.Counters.SetAttribute("timeout", "1")
            $f.Trx.TestRun.ResultSummary.Counters.SetAttribute("executed", "2")
        }
        $failure = $f.JUnit.testsuites.testsuite.testcase[3].failure
        $errorNode = $f.JUnit.CreateElement("error")
        $errorNode.SetAttribute("message", $failure.GetAttribute("message"))
        $errorNode.SetAttribute("type", $failure.GetAttribute("type"))
        $errorNode.InnerText = $failure.InnerText
        $null = $failure.ParentNode.ReplaceChild($errorNode, $failure)
        $f.JUnit.testsuites.SetAttribute("failures", "0")
        $f.JUnit.testsuites.SetAttribute("errors", "1")
        $f.JUnit.testsuites.testsuite.SetAttribute("failures", "0")
        $f.JUnit.testsuites.testsuite.SetAttribute("errors", "1")
        $null = Invoke-FixtureComparison $f
    }
}

$scratch = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts" -AdditionalChildPath @("tmp", ("report-comparison-" + [guid]::NewGuid().ToString("N")))
New-Item -ItemType Directory -Path $scratch | Out-Null
$failures = [Collections.Generic.List[string]]::new()
$passed = 0
try {
    foreach ($case in $cases.GetEnumerator()) {
        try {
            $fixture = New-ComparisonFixture (Join-Path $scratch $case.Key)
            & $case.Value $fixture $caseData[$case.Key]
            $passed++
            Write-Output "PASS $($case.Key)"
        } catch {
            $failures.Add("$($case.Key): $($_.Exception.Message)")
            Write-Output "FAIL $($failures[-1])"
        }
    }
    Write-Output "Report comparison tests: $passed passed; $($failures.Count) failed; $($cases.Count) executed."
    if ($failures.Count -gt 0) { throw ($failures -join "`n") }
} finally {
    Remove-Item -LiteralPath $scratch -Recurse -Force
}

exit 0
