// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitXmlWriterBehaviorTests
{
    [TestMethod]
    public async Task WriteXmlAsync_EmitsCompleteOrderedCaseAndMetadata()
    {
        var fixture = new ReportBehaviorFixture();
        DateTimeOffset epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var skipped = new TestCase
        {
            ClassName = "Suite",
            Name = "row [attempt 2]",
            OriginalName = "row",
            TestPath = "Root/row",
            DuplicateIndex = 2,
            DuplicateOf = 3,
            Result = new CapturedTestResult
            {
                RawUid = "raw",
                Uid = "uid",
                Outcome = "skipped",
                ErrorMessage = "not applicable",
                Duration = TimeSpan.FromMilliseconds(1250),
                StandardOutput = "line one\nline two",
                StandardError = "diagnostic",
                Traits = [new("Category", "a&b")],
            },
        };
        var plain = new TestCase
        {
            ClassName = "Suite",
            Name = "plain",
            OriginalName = "plain",
            TestPath = string.Empty,
            DuplicateIndex = 0,
            DuplicateOf = 0,
            Result = new CapturedTestResult { RawUid = "raw2", Outcome = "passed" },
        };
        var suites = new SuiteSet
        {
            Name = "Module",
            TotalTests = 2,
            TotalFailures = 0,
            TotalErrors = 0,
            TotalSkipped = 1,
            TotalDuration = TimeSpan.FromMilliseconds(1250),
            Timestamp = epoch,
            Suites =
            [
                new Suite
                {
                    Name = "Suite",
                    Tests = [skipped, plain],
                    Failures = 0,
                    Errors = 0,
                    Skipped = 1,
                    TotalDuration = TimeSpan.FromMilliseconds(1250),
                    Timestamp = epoch,
                },
            ],
        };

        await new JUnitXmlWriter(fixture.FileSystem.Object, fixture.Environment.Object, fixture.Framework.Object, 2, CancellationToken.None)
            .WriteXmlAsync("report.xml", suites);

        const string expected = """
            <testsuites name="Module" tests="2" failures="0" errors="0" skipped="1" time="1.250" timestamp="1970-01-01T00:00:00.000">
              <testsuite name="Suite" tests="2" failures="0" errors="0" skipped="1" time="1.250" timestamp="1970-01-01T00:00:00.000" hostname="agent" id="0">
                <properties>
                  <property name="test-framework" value="Test Framework" />
                  <property name="test-framework-version" value="1.2.3" />
                  <property name="test-framework-uid" value="fw" />
                  <property name="exit-code" value="2" />
                </properties>
                <testcase name="row [attempt 2]" classname="Suite" time="1.250">
                  <properties>
                    <property name="uid" value="uid" />
                    <property name="testpath" value="Root/row" />
                    <property name="original-name" value="row" />
                    <property name="attempt-index" value="2" />
                    <property name="attempt-of" value="3" />
                    <property name="trait.Category" value="a&amp;b" />
                  </properties>
                  <skipped message="not applicable" />
                  <system-out>line one
            line two</system-out>
                  <system-err>diagnostic</system-err>
                </testcase>
                <testcase name="plain" classname="Suite" time="0.000" />
              </testsuite>
            </testsuites>
            """;
        string actual = fixture.WrittenFiles["report.xml"].Text;
        Assert.AreEqual(XDocument.Parse(expected).ToString(), XDocument.Parse(actual).ToString());
        Assert.IsTrue(fixture.WrittenFiles["report.xml"].IsDisposed);
    }

    [TestMethod]
    [DataRow(null, "", DisplayName = "Null text")]
    [DataRow("", "", DisplayName = "Empty text")]
    [DataRow("a\uD83D\uDE00z", "a\uD83D\uDE00z", DisplayName = "Valid supplementary scalar")]
    [DataRow("a\u0001\uD83D\uDE00z", "a\uFFFD\uD83D\uDE00z", DisplayName = "Replacement before supplementary scalar")]
    [DataRow("LoneSurrogates", "\uFFFDz\uFFFD", DisplayName = "Lone surrogate code units")]
    [DataRow("\t\n\r\uFFFE\uFFFF", "\t\n\r\uFFFD\uFFFD", DisplayName = "XML whitespace and noncharacters")]
    public void XmlSafeText_PreservesLegalScalarsAndReplacesIllegalCodeUnits(string? input, string expected)
    {
        // Invalid UTF-16 cannot be transported faithfully through custom-attribute UTF-8 metadata.
        // Build it at runtime while keeping the result/report identity safe.
        if (input == "LoneSurrogates")
        {
            input = new string(['\uD800', 'z', '\uDC00']);
        }

        Assert.AreEqual(expected, JUnitXmlWriter.XmlSafeText(input));
    }
}
