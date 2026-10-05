// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// FailedTestInfoRequest is source-linked into both referenced MSBuild assemblies.
extern alias PlatformMSBuild;

using Microsoft.Testing.Platform.MSBuild;

using FailedTestInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.FailedTestInfoRequest;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class FailedTestHelperTests
{
    [TestMethod]
    public void FromFailedTest_StackTraceLocation_TakesPrecedenceOverCodeFilePath()
    {
        FailedTestInfoRequest request = CreateRequest(
            errorStackTrace: "   at Program.Main() in /repo/Program.cs:line 42",
            codeFilePath: "/repo/Fallback.cs",
            lineNumber: 7);

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out _, out string file, out int lineNumber, out _, out _);

        Assert.AreEqual("/repo/Program.cs", file);
        Assert.AreEqual(42, lineNumber);
    }

    [TestMethod]
    public void FromFailedTest_CodeFilePathFallback_UsesProvidedLineNumber()
    {
        FailedTestInfoRequest request = CreateRequest(
            errorStackTrace: "   at Program.Main()",
            codeFilePath: "/repo/Program.cs",
            lineNumber: 24);

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out _, out string file, out int lineNumber, out _, out _);

        Assert.AreEqual("/repo/Program.cs", file);
        Assert.AreEqual(24, lineNumber);
    }

    [TestMethod]
    public void FromFailedTest_TargetPathFallback_UsesTargetPathWhenNoSourceLocationExists()
    {
        FailedTestInfoRequest request = CreateRequest(errorStackTrace: "   at Program.Main()");

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out _, out string file, out int lineNumber, out _, out _);

        Assert.AreEqual("/repo/Target.dll", file);
        Assert.AreEqual(0, lineNumber);
    }

    [TestMethod]
    public void FromFailedTest_CanceledTest_UsesCanceledErrorCode()
    {
        FailedTestInfoRequest request = CreateRequest(isCanceled: true);

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out string errorCode, out _, out _, out _, out _);

        Assert.AreEqual("test canceled", errorCode);
    }

    [TestMethod]
    public void FromFailedTest_FailedTest_UsesFailedErrorCode()
    {
        FailedTestInfoRequest request = CreateRequest();

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out string errorCode, out _, out _, out _, out _);

        Assert.AreEqual("test failed", errorCode);
    }

    [TestMethod]
    public void FromFailedTest_MultilineOutput_IncludesExpectedActualAndStackTrace()
    {
        const string errorStackTrace = "   at Program.Main() in /repo/Program.cs:line 42";
        FailedTestInfoRequest request = CreateRequest(
            errorMessage: "Values did not match.",
            errorStackTrace: errorStackTrace,
            expected: "expected",
            actual: "actual");

        request.FromFailedTest(outputSupportsMultiline: true, "/repo/Target.dll",
            out _, out _, out _, out string message, out string? lowPriorityMessage);

        Assert.Contains("Test (10ms): Values did not match.", message);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, Resources.MSBuildResources.ExpectedValue, "expected") + Environment.NewLine, message);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, Resources.MSBuildResources.ActualValue, "actual") + Environment.NewLine, message);
        Assert.Contains($"{Resources.MSBuildResources.StackTrace}{Environment.NewLine}{errorStackTrace}", message);
        Assert.IsNull(lowPriorityMessage);
    }

    [TestMethod]
    public void FromFailedTest_SingleLineOutput_UsesPlaceAndStripsNewlinesFromMessage()
    {
        FailedTestInfoRequest request = CreateRequest(
            errorMessage: "First line\r\nSecond line",
            errorStackTrace: "   at Program.Main() in /repo/Program.cs:line 42");

        request.FromFailedTest(outputSupportsMultiline: false, "/repo/Target.dll",
            out _, out _, out _, out string message, out string? lowPriorityMessage);

        Assert.AreEqual("Test (10ms): Program.Main() First line  Second line", message);
        Assert.IsNotNull(lowPriorityMessage);
        Assert.Contains($"First line\r\nSecond line{Environment.NewLine}", lowPriorityMessage);
    }

    [TestMethod]
    public void FromFailedTest_SingleLineOutputWithoutPlace_OmitsPlaceAndPopulatesLowPriorityMessage()
    {
        FailedTestInfoRequest request = CreateRequest(
            errorMessage: "First line\nSecond line",
            errorStackTrace: "   at Program.Main()");

        request.FromFailedTest(outputSupportsMultiline: false, "/repo/Target.dll",
            out _, out _, out _, out string message, out string? lowPriorityMessage);

        Assert.AreEqual("Test (10ms) First line Second line", message);
        Assert.IsNotNull(lowPriorityMessage);
        Assert.Contains($"Stack Trace:{Environment.NewLine}   at Program.Main(){Environment.NewLine}", lowPriorityMessage);
    }

    [TestMethod]
    public void FromFailedTest_SingleLineOutput_ShortensEachMessageSegmentTo1000Characters()
    {
        string displayName = new('D', 1100);
        string errorMessage = new('E', 1100);
        FailedTestInfoRequest request = CreateRequest(displayName: displayName, duration: null, errorMessage: errorMessage);

        request.FromFailedTest(outputSupportsMultiline: false, "/repo/Target.dll",
            out _, out _, out _, out string message, out _);

        Assert.AreEqual($"{new string('D', 1000)} {new string('E', 1000)}", message);
    }

    private static FailedTestInfoRequest CreateRequest(
        string displayName = "Test",
        bool isCanceled = false,
        string? duration = "10ms",
        string? errorMessage = "Failure",
        string? errorStackTrace = null,
        string? expected = null,
        string? actual = null,
        string? codeFilePath = null,
        int lineNumber = 0)
        => new(displayName, isCanceled, duration, errorMessage, errorStackTrace, expected, actual, codeFilePath, lineNumber);
}
