// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportFailureBodyTests
{
    // DateTimeOffset.UnixEpoch is not available on .NET Framework, which this project also targets.
    private static readonly DateTimeOffset Timestamp = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void BuildFailureBody_WithExceptionTypeMessageAndStackTrace_MirrorsPrintStackTraceShape()
        => Assert.AreEqual(
            "System.InvalidOperationException: boom\n   at Foo.Bar()",
            JUnitXmlWriter.BuildFailureBody(CreateResult(
                exceptionType: "System.InvalidOperationException",
                errorMessage: "boom",
                stackTrace: "   at Foo.Bar()")));

    [TestMethod]
    public void BuildFailureBody_WithoutStackTrace_StillContainsTypeAndMessage()
        => Assert.AreEqual(
            "System.InvalidOperationException: boom",
            JUnitXmlWriter.BuildFailureBody(CreateResult(
                exceptionType: "System.InvalidOperationException",
                errorMessage: "boom",
                stackTrace: null)));

    [TestMethod]
    public void BuildFailureBody_WithoutExceptionType_OmitsTheTypePrefix()
        => Assert.AreEqual(
            "Assert.AreEqual failed\n   at Foo.Bar()",
            JUnitXmlWriter.BuildFailureBody(CreateResult(
                exceptionType: null,
                errorMessage: "Assert.AreEqual failed",
                stackTrace: "   at Foo.Bar()")));

    [TestMethod]
    public void BuildFailureBody_WithoutErrorMessage_OmitsTheSeparator()
        => Assert.AreEqual(
            "System.InvalidOperationException\n   at Foo.Bar()",
            JUnitXmlWriter.BuildFailureBody(CreateResult(
                exceptionType: "System.InvalidOperationException",
                errorMessage: null,
                stackTrace: "   at Foo.Bar()")));

    [TestMethod]
    public void BuildFailureBody_WithStackTraceOnly_ReturnsStackTraceUnchanged()
        => Assert.AreEqual(
            "   at Foo.Bar()",
            JUnitXmlWriter.BuildFailureBody(CreateResult(
                exceptionType: null,
                errorMessage: null,
                stackTrace: "   at Foo.Bar()")));

    [TestMethod]
    public void BuildFailureBody_WithNoExceptionDetails_ReturnsNull()
        => Assert.IsNull(JUnitXmlWriter.BuildFailureBody(CreateResult(
            exceptionType: null,
            errorMessage: null,
            stackTrace: null)));

    [TestMethod]
    public async Task WriteXmlAsync_FailedTest_WritesMessageIntoTheFailureBodyAndKeepsAttributes()
    {
        XElement failure = await WriteSingleTestCaseAndGetOutcomeElementAsync(
            CreateResult(
                exceptionType: "System.InvalidOperationException",
                errorMessage: "boom",
                stackTrace: "   at Foo.Bar()"));

        Assert.AreEqual("failure", failure.Name.LocalName);

        // Attributes keep their existing meaning so consumers reading them directly are unaffected.
        Assert.AreEqual("boom", failure.Attribute("message")!.Value);
        Assert.AreEqual("System.InvalidOperationException", failure.Attribute("type")!.Value);

        // The body now leads with the "type: message" header, like Throwable.printStackTrace().
        Assert.AreEqual("System.InvalidOperationException: boom\n   at Foo.Bar()", NormalizeLineEndings(failure.Value));
    }

    [TestMethod]
    public async Task WriteXmlAsync_FailedTestWithoutException_FallsBackToElementNameForTypeAttribute()
    {
        // A framework that reports a failure through `Explanation` alone gives us no exception
        // type, but the Ant/windyroad JUnit.xsd marks `type` as required.
        XElement failure = await WriteSingleTestCaseAndGetOutcomeElementAsync(
            CreateResult(exceptionType: null, errorMessage: "explanation only", stackTrace: null));

        Assert.AreEqual("failure", failure.Attribute("type")!.Value);
        Assert.AreEqual("explanation only", NormalizeLineEndings(failure.Value));
    }

    [TestMethod]
    public async Task WriteXmlAsync_ErroredTestWithoutException_FallsBackToElementNameForTypeAttribute()
    {
        XElement error = await WriteSingleTestCaseAndGetOutcomeElementAsync(
            CreateResult(exceptionType: null, errorMessage: "kaboom", stackTrace: null, outcome: "errored"));

        Assert.AreEqual("error", error.Name.LocalName);
        Assert.AreEqual("error", error.Attribute("type")!.Value);
        Assert.AreEqual("kaboom", error.Attribute("message")!.Value);
        Assert.AreEqual("kaboom", NormalizeLineEndings(error.Value));
    }

    private static async Task<XElement> WriteSingleTestCaseAndGetOutcomeElementAsync(CapturedTestResult result)
    {
        string xml = await WriteXmlAsync(result);
        XElement testCase = XDocument.Parse(xml).Descendants("testcase").Single();
        return testCase.Elements().Single(e => e.Name.LocalName is "failure" or "error" or "skipped");
    }

    private static async Task<string> WriteXmlAsync(CapturedTestResult result)
    {
        using var stream = new MemoryFileStream();
        var fileSystemMock = new Mock<IFileSystem>();
        var environmentMock = new Mock<IEnvironment>();
        var testFrameworkMock = new Mock<ITestFramework>();

        _ = fileSystemMock.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _ = environmentMock.SetupGet(x => x.MachineName).Returns("test-host");
        _ = testFrameworkMock.SetupGet(x => x.Uid).Returns("fake-uid");
        _ = testFrameworkMock.SetupGet(x => x.Version).Returns("1.2.3");
        _ = testFrameworkMock.SetupGet(x => x.DisplayName).Returns("Fake");

        var testCase = new TestCase
        {
            ClassName = "MyClass",
            Name = "MyTest",
            OriginalName = "MyTest",
            TestPath = "MyTest",
            Result = result,
            DuplicateIndex = 0,
            DuplicateOf = 0,
        };

        var suites = new SuiteSet
        {
            Name = "MyModule",
            Suites =
            [
                new Suite
                {
                    Name = "MyClass",
                    Tests = [testCase],
                    Failures = result.Outcome == "failed" ? 1 : 0,
                    Errors = result.Outcome == "errored" ? 1 : 0,
                    Skipped = result.Outcome == "skipped" ? 1 : 0,
                    TotalDuration = TimeSpan.Zero,
                    Timestamp = Timestamp,
                }
            ],
            TotalTests = 1,
            TotalFailures = result.Outcome == "failed" ? 1 : 0,
            TotalErrors = result.Outcome == "errored" ? 1 : 0,
            TotalSkipped = result.Outcome == "skipped" ? 1 : 0,
            TotalDuration = TimeSpan.Zero,
            Timestamp = Timestamp,
        };

        await new JUnitXmlWriter(fileSystemMock.Object, environmentMock.Object, testFrameworkMock.Object, exitCode: 2, CancellationToken.None)
            .WriteXmlAsync("report.xml", suites);

        // MemoryStream.ToArray() is still valid after the writer disposed the stream.
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(stream.Stream.ToArray());
    }

    private static CapturedTestResult CreateResult(string? exceptionType, string? errorMessage, string? stackTrace, string outcome = "failed")
        => new()
        {
            RawUid = "uid-1",
            Outcome = outcome,
            ExceptionType = exceptionType,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
        };

    // XML parsers normalize literal CRLF in text content to LF, but the writer's
    // NewLineHandling can still round-trip escaped carriage returns, so normalize
    // before comparing against the LF-based expectations above.
    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n").Replace("\r", "\n");

    private sealed class MemoryFileStream : IFileStream
    {
        public MemoryFileStream() => Stream = new MemoryStream();

        public MemoryStream Stream { get; }

        Stream IFileStream.Stream => Stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => Stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => Stream.DisposeAsync();
#endif
    }
}
