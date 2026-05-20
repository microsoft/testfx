// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsTests
{
    [TestMethod]
    public void ReportsTheFirstExistingFileInStackTraceWithTheRightLineNumberAndEscaping()
    {
        Exception error;
        try
        {
            throw new Exception("this is an error\nwith\rnewline");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ##. If we keep it, then when the test fails, the assertion failure will get printed to screen and picked up incorrectly by AzDO, because it scans all output for the ##vso... pattern
        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=22;columnnumber=1]MyTestDisplayName [net9.0]%0Athis is an error%0Awith%0Dnewline", text, $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void ReportsTheFirstExistingFileInStackTraceWithTheRightLineNumberAndEscapingAndOverrideExceptionMessage()
    {
        Exception error;
        try
        {
            throw new Exception("this is an error\nwith\rnewline");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ##. If we keep it, then when the test fails, the assertion failure will get printed to screen and picked up incorrectly by AzDO, because it scans all output for the ##vso... pattern
        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", "Some custom reason\nwith\rnewline", error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=41;columnnumber=1]MyTestDisplayName [net9.0]%0ASome custom reason%0Awith%0Dnewline", text, $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void SkipsMSTestAssertImplementationFrameInPartialClassFile()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/6925.
        //
        // The Assert class is split into partial-class files such as Assert.IComparable.cs and
        // Assert.AreEqual.cs. Their file names do not end with "Assert.cs", so the previous
        // filename-based skip heuristic missed them and the reporter incorrectly annotated the
        // framework implementation. The fix matches on the fully-qualified type prefix in the
        // 'code' capture instead, so every Assert/CollectionAssert/StringAssert partial is
        // correctly recognized as framework internals.
        (string userFile, int userLine) = GetCurrentLocation();

        // The Assert.IComparable.cs file actually exists in the repo, so the file-existence
        // check would happily accept the framework frame if we did not skip it.
        string stackTrace = string.Join(
            Environment.NewLine,
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsLessThan[T](T upperBound, T value, String message) in /_/src/TestFramework/TestFramework/Assertions/Assert.IComparable.cs:line 138",
            $"   at Microsoft.Testing.Extensions.UnitTests.AzureDevOpsTests.SkipsMSTestAssertImplementationFrameInPartialClassFile() in {userFile}:line {userLine}");

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            $"vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber={userLine};columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void SkipsMSTestCollectionAssertImplementationFrameInPartialClassFile()
    {
        (string userFile, int userLine) = GetCurrentLocation();

        string stackTrace = string.Join(
            Environment.NewLine,
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.AreEqual(ICollection expected, ICollection actual, String message) in /_/src/TestFramework/TestFramework/Assertions/CollectionAssert.Equality.cs:line 42",
            $"   at Microsoft.Testing.Extensions.UnitTests.AzureDevOpsTests.SkipsMSTestCollectionAssertImplementationFrameInPartialClassFile() in {userFile}:line {userLine}");

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            $"vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber={userLine};columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void SkipsMSTestStringAssertImplementationFrame()
    {
        (string userFile, int userLine) = GetCurrentLocation();

        string stackTrace = string.Join(
            Environment.NewLine,
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 17",
            $"   at Microsoft.Testing.Extensions.UnitTests.AzureDevOpsTests.SkipsMSTestStringAssertImplementationFrame() in {userFile}:line {userLine}");

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            $"vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber={userLine};columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void SkipsMSTestAssertThatImplementationFrame()
    {
        (string userFile, int userLine) = GetCurrentLocation();

        string stackTrace = string.Join(
            Environment.NewLine,
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.That(Expression`1 condition, String message, String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.That.cs:line 27",
            $"   at Microsoft.Testing.Extensions.UnitTests.AzureDevOpsTests.SkipsMSTestAssertThatImplementationFrame() in {userFile}:line {userLine}");

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            $"vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber={userLine};columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void DoesNotSkipUserFrameWhoseFileNameEndsWithAssertCs()
    {
        // The previous heuristic skipped any frame whose file path ended with "Assert.cs",
        // which incorrectly hid user code in files named e.g. MyAssert.cs from PR annotations.
        // The fix is based on the fully-qualified type name in the frame, so user types are
        // never confused with the framework's Assert/CollectionAssert/StringAssert types.
        string repoRoot = RootFinder.Find();
        string userFile = Path.Combine(repoRoot, "src", "MyCompany", "MyAssert.cs");

        string stackTrace =
            $"   at MyCompany.Verification.MyAssert.Verify(Object value) in {userFile}:line 17";

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", fileSystem.Object, logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            "vso[task.logissue type=severity;sourcepath=src/MyCompany/MyAssert.cs;linenumber=17;columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void DoesNotSkipUserFrameWhoseTypeNameStartsWithAssert()
    {
        // A user type literally named "Assert" (e.g. MyCompany.Tests.Assert) must not be
        // mistaken for Microsoft.VisualStudio.TestTools.UnitTesting.Assert. The prefix check
        // is anchored on the full MSTest namespace, so user types in other namespaces are safe.
        string repoRoot = RootFinder.Find();
        string userFile = Path.Combine(repoRoot, "src", "MyCompany", "MyAssertions.cs");

        string stackTrace =
            $"   at MyCompany.Tests.Assert.Equal[T](T expected, T actual) in {userFile}:line 25";

        var error = new SyntheticStackTraceException("boom", stackTrace);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);

        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", fileSystem.Object, logger, "net9.0")?.TrimStart('#');

        Assert.AreEqual(
            "vso[task.logissue type=severity;sourcepath=src/MyCompany/MyAssertions.cs;linenumber=25;columnnumber=1]MyTestDisplayName [net9.0]%0Aboom",
            text,
            $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void RealAssertFailure_AnnotationPointsAtTestMethodAndNotAtFrameworkFile()
    {
        // End-to-end acceptance for https://github.com/microsoft/testfx/issues/8277.
        //
        // Trigger a *real* MSTest assertion failure (no synthetic stack trace) and ensure the
        // AzDO reporter annotates the test method's source file rather than the framework
        // partial-class file that actually holds the failing implementation.
        //
        // This regression is double-guarded:
        //   * On .NET 6+: every public Assert/CollectionAssert/StringAssert method carries
        //     [StackTraceHidden], so the framework frame should not even appear in the
        //     captured stack trace.
        //   * On older runtimes (.NET Framework / netstandard2.0 consumers): the attribute is
        //     a no-op, but the reporter's type-prefix heuristic still skips the framework
        //     frame.
        // Either way, the annotation must point at this test method's source file.
        (string testFile, _) = GetCurrentLocation();

        AssertFailedException error;
        try
        {
#pragma warning disable MSTEST0025 // Use 'Assert.Fail' instead of an always-failing assert - we deliberately want a real Assert.AreEqual failure so the captured stack trace reflects what users see.
            Assert.AreEqual(1, 2);
#pragma warning restore MSTEST0025
            throw new InvalidOperationException("Expected AssertFailedException was not thrown.");
        }
        catch (AssertFailedException ex)
        {
            error = ex;
        }

        var logger = new TextLogger();
        // Trim ##. If we keep it, then when the test fails, the assertion failure will get printed to screen and picked up incorrectly by AzDO, because it scans all output for the ##vso... pattern.
        string? text = AzureDevOpsReporter.GetErrorText(
            "MyTestDisplayName",
            explanation: null,
            error,
            "severity",
            new SystemFileSystem(),
            logger,
            "net9.0")?.TrimStart('#');

        // Also neutralize any '##vso[' that the trace logger captured (the reporter logs the full
        // command line at trace level) so the assertion failure messages below cannot smuggle a
        // real AzDO command into the build output when this test fails on CI.
        string safeLogs = string.Join("\n", logger.Logs).Replace("##vso[", "vso[");

        Assert.IsNotNull(text, $"AzDO reporter should have produced an annotation. Logs:\n{safeLogs}");

        // Mirror the path normalization performed by AzureDevOpsReporter.GetErrorText:
        //  * Strip the deterministic-build root '/_/' (set by <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
        //    in CI), then fall back to stripping the runtime repo root for local builds.
        //  * Normalize separators to '/'.
        // Without this, the test breaks in CI where [CallerFilePath] returns '/_/test/...'
        // but the reporter strips '/_/' and emits 'sourcepath=test/...'.
        const string DeterministicBuildRoot = "/_/";
        string normalizedTestFile = testFile.Replace('\\', '/');
        string repoRoot = RootFinder.Find().Replace('\\', '/');
        string expectedRelativeFile = normalizedTestFile.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase)
            ? normalizedTestFile.Substring(DeterministicBuildRoot.Length)
            : normalizedTestFile.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase)
                ? normalizedTestFile.Substring(repoRoot.Length).TrimStart('/')
                : normalizedTestFile;

        Assert.Contains(
            $"sourcepath={expectedRelativeFile};",
            text,
            $"AzDO annotation must point at the test method's source file. Got: {text}\nLogs:\n{safeLogs}");
        Assert.DoesNotContain(
            "/src/TestFramework/TestFramework/Assertions/",
            text,
            $"AzDO annotation must not point at a framework Assert partial-class file. Got: {text}\nLogs:\n{safeLogs}");
    }

    [TestMethod]
    public void FormatErrorMessage_PlacesTestNameAsTitleOnFirstLine()
    {
        // The reporter emits one message that is rendered both by AzDO and by GitHub PR
        // checks (via the dotnet problem matcher). Both UIs treat the first line as the
        // bold annotation title, so the test name should live there and the assertion
        // text should follow on the next line (encoded by AzDoEscaper as %0A).
        string formatted = AzureDevOpsReporter.FormatErrorMessage(
            "MyTestDisplayName",
            "net9.0",
            "Assert.AreEqual failed. Expected:<7>. Actual:<0>.");

        Assert.AreEqual("MyTestDisplayName [net9.0]\nAssert.AreEqual failed. Expected:<7>. Actual:<0>.", formatted);
    }

    [TestMethod]
    public void FormatErrorMessage_DoesNotDuplicateTfmWhenAlreadyInDisplayName()
    {
        // In multi-TFM runs MTP appends the TFM to the display name (e.g. "MyTest (net9.0)").
        // We must not append it again or annotations end up reading "MyTest (net9.0) [net9.0]".
        string formatted = AzureDevOpsReporter.FormatErrorMessage(
            "MyTest (net9.0)",
            "net9.0",
            "boom");

        Assert.AreEqual("MyTest (net9.0)\nboom", formatted);
    }

    [TestMethod]
    public void FormatErrorMessage_DoesNotDuplicateTfmWhenQuotedInDisplayName()
    {
        // Acceptance tests' display names sometimes show the TFM quoted, e.g.
        // 'HangDump_TemplateFileName_CreateDump ("net10.0")'. The dedup must catch that too.
        string formatted = AzureDevOpsReporter.FormatErrorMessage(
            "HangDump_TemplateFileName_CreateDump (\"net10.0\")",
            "net10.0",
            "boom");

        Assert.AreEqual("HangDump_TemplateFileName_CreateDump (\"net10.0\")\nboom", formatted);
    }

    [TestMethod]
    public void FormatErrorMessage_KeepsTfmSuffixWhenDisplayNameContainsDifferentTfm()
    {
        // Test asset's TFM (in display name) can legitimately differ from the host TFM
        // when an acceptance test runs against a target asset built for an older TFM.
        // In that case both pieces of info are useful and we should keep them.
        string formatted = AzureDevOpsReporter.FormatErrorMessage(
            "HangDump_TemplateFileName_CreateDump (\"net10.0\")",
            "net11.0",
            "boom");

        Assert.AreEqual("HangDump_TemplateFileName_CreateDump (\"net10.0\") [net11.0]\nboom", formatted);
    }

    private static (string FilePath, int LineNumber) GetCurrentLocation(
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
        => (filePath!, lineNumber);

    private sealed class SyntheticStackTraceException : Exception
    {
        public SyntheticStackTraceException(string message, string stackTrace)
            : base(message)
            => StackTrace = stackTrace;

        public override string? StackTrace { get; }
    }

    private class TextLogger : ILogger
    {
        public List<string> Logs { get; } = [];

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");
            return Task.CompletedTask;
        }
    }
}
