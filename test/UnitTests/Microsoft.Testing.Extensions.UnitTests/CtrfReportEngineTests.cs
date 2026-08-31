// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class CtrfReportEngineTests
{
    private static readonly Type CaptureHelperType = typeof(CtrfReportEngine).Assembly
        .GetType("Microsoft.Testing.Extensions.TestResultCaptureHelper", throwOnError: true)!;

    // Bound to the production constant (resolved via reflection from the CtrfReport assembly, since the
    // linked TestResultCaptureHelper type is ambiguous across extension assemblies) so the test stays
    // aligned with the shared truncation behavior.
    private static readonly int MaxStandardStreamLength =
        (int)CaptureHelperType.GetField(nameof(MaxStandardStreamLength), BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

    private static readonly int MaxIdentityFieldLength =
        (int)CaptureHelperType.GetField(nameof(MaxIdentityFieldLength), BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

    private static readonly int MaxMessageLength =
        (int)CaptureHelperType.GetField(nameof(MaxMessageLength), BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ICommandLineOptions> _commandLineOptionsMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    [TestMethod]
    public async Task GenerateReportAsync_WritesValidCtrfJson_WithRequiredTopLevelFields()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("p1", "Passing test", "passed"),
            Captured("f1", "Failing test", "failed", errorMessage: "expected 1, got 2"),
            Captured("s1", "Skipped test", "skipped", errorMessage: "not relevant"),
        ];

        (string fileName, string? warning) = await engine.GenerateReportAsync(tests);

        Assert.IsNotNull(fileName);
        Assert.IsNull(warning);

        // Parse the produced JSON to validate the CTRF document structure (this is the
        // schema contract for the consumers at https://github.com/ctrf-io/ctrf).
        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement root = document.RootElement;

        Assert.AreEqual("CTRF", root.GetProperty("reportFormat").GetString());
        Assert.AreEqual("0.0.0", root.GetProperty("specVersion").GetString());
        Assert.IsGreaterThan(0, root.GetProperty("reportId").GetString()!.Length);
        Assert.IsGreaterThan(0, root.GetProperty("timestamp").GetString()!.Length);
        Assert.IsTrue(root.GetProperty("generatedBy").GetString()!.StartsWith("Microsoft.Testing.Extensions.CtrfReport", StringComparison.Ordinal));

        JsonElement results = root.GetProperty("results");

        JsonElement tool = results.GetProperty("tool");
        Assert.IsNotNull(tool.GetProperty("name").GetString());
        Assert.IsNotNull(tool.GetProperty("version").GetString());

        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(3, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("skipped").GetInt32());

        JsonElement testArray = results.GetProperty("tests");
        Assert.AreEqual(3, testArray.GetArrayLength());
    }

    [TestMethod]
    public async Task GenerateReportAsync_WhenRecoveredAfterCrash_MarksEnvironmentIncomplete()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream, isIncomplete: true);

        await engine.GenerateReportAsync([Captured("p1", "Recovered test", "passed")]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement extra = document.RootElement.GetProperty("results").GetProperty("environment").GetProperty("extra");
        Assert.IsTrue(extra.GetProperty("incomplete").GetBoolean());
        Assert.AreEqual("aborted", extra.GetProperty("runStatus").GetString());
    }

    [TestMethod]
    [DataRow("passed", typeof(PassedTestNodeStateProperty))]
    [DataRow("skipped", typeof(SkippedTestNodeStateProperty))]
    [DataRow("failed", typeof(FailedTestNodeStateProperty))]
    public void TestResultCapture_ClassifiesTerminalOutcomes_ToCtrfStatus(string expectedStatus, Type stateType)
    {
        TestNodeStateProperty state = stateType switch
        {
            Type t when t == typeof(PassedTestNodeStateProperty) => PassedTestNodeStateProperty.CachedInstance,
            Type t when t == typeof(SkippedTestNodeStateProperty) => SkippedTestNodeStateProperty.CachedInstance,
            Type t when t == typeof(FailedTestNodeStateProperty) => new FailedTestNodeStateProperty("x"),
            _ => throw new InvalidOperationException(),
        };

        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(state) };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual(expectedStatus, result.Status);
        Assert.IsNull(result.RawStatus, "Pure CTRF outcomes should not carry rawStatus.");
    }

    [TestMethod]
    public void TestResultCapture_ErrorState_MapsToFailed_With_RawStatus_Errored()
    {
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(new ErrorTestNodeStateProperty("boom")) };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual("failed", result.Status);
        Assert.AreEqual("errored", result.RawStatus);
    }

    [TestMethod]
    public void TestResultCapture_TimeoutState_MapsToFailed_With_RawStatus_TimedOut()
    {
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(new TimeoutTestNodeStateProperty("slow")) };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual("failed", result.Status);
        Assert.AreEqual("timedOut", result.RawStatus);
    }

    [TestMethod]
    public void TestResultCapture_CapturesRetryAttemptMetadata()
    {
        var bag = new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new RetryAttemptProperty(2, isSuperseded: false));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual(2, result.RetryAttemptNumber);
        Assert.IsFalse(result.IsSupersededRetryAttempt);
    }

    [TestMethod]
    public void TestResultCapture_Truncates_OverLength_StandardOutput_AtBoundary()
    {
        string huge = new('a', MaxStandardStreamLength + 7);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new StandardOutputProperty(huge));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.StandardOutput);
        Assert.StartsWith(new string('a', MaxStandardStreamLength), result.StandardOutput!);
        Assert.Contains("[truncated, original length:", result.StandardOutput);
        Assert.Contains((MaxStandardStreamLength + 7).ToString(CultureInfo.InvariantCulture), result.StandardOutput);
    }

    [TestMethod]
    public async Task TestResultCapture_CapturesAndSerializesFileArtifacts()
    {
        string screenshotPath = Path.Combine(Path.GetTempPath(), "screenshot.png");
        string diagnosticsPath = Path.Combine(Path.GetTempPath(), "diagnostics.unknown");
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new FileArtifactProperty(new FileInfo(screenshotPath), "Screenshot", "Browser failure"));
        bag.Add(new FileArtifactProperty(new FileInfo(diagnosticsPath), string.Empty));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result.Attachments);
        Assert.HasCount(2, result.Attachments);
        CapturedAttachment screenshot = result.Attachments.Single(a => a.Name == "Screenshot");
        CapturedAttachment diagnostics = result.Attachments.Single(a => a.Name == "diagnostics.unknown");
        Assert.AreEqual("image/png", screenshot.ContentType);
        Assert.AreEqual(new FileInfo(screenshotPath).FullName, screenshot.Path);
        Assert.AreEqual("Browser failure", screenshot.Description);
        Assert.AreEqual("application/octet-stream", diagnostics.ContentType);

        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        await engine.GenerateReportAsync([result]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement attachments = document.RootElement.GetProperty("results").GetProperty("tests")[0].GetProperty("attachments");
        Assert.AreEqual(2, attachments.GetArrayLength());
        JsonElement screenshotJson = attachments.EnumerateArray().Single(a => a.GetProperty("name").GetString() == "Screenshot");
        JsonElement diagnosticsJson = attachments.EnumerateArray().Single(a => a.GetProperty("name").GetString() == "diagnostics.unknown");
        Assert.AreEqual("image/png", screenshotJson.GetProperty("contentType").GetString());
        Assert.AreEqual(new FileInfo(screenshotPath).FullName, screenshotJson.GetProperty("path").GetString());
        Assert.AreEqual("Browser failure", screenshotJson.GetProperty("extra").GetProperty("description").GetString());
        Assert.AreEqual("application/octet-stream", diagnosticsJson.GetProperty("contentType").GetString());
        Assert.IsFalse(diagnosticsJson.TryGetProperty("extra", out _));
    }

    [TestMethod]
    public void TestResultCapture_InvalidAttachmentPath_IsSkippedWithoutLosingValidAttachments()
    {
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new FileArtifactProperty(new FileInfo("valid.log"), "Valid"));
        bag.Add(new FileArtifactProperty(new FileInfo("invalid.log"), "Invalid"));
        Func<FileInfo, string> resolveFullPath = fileInfo
            => fileInfo.Name == "invalid.log"
                ? throw new IOException("Invalid path")
                : fileInfo.FullName;

        IReadOnlyList<CapturedAttachment>? attachments = CaptureAttachmentsForTest(bag, resolveFullPath);

        Assert.IsNotNull(attachments);
        Assert.HasCount(1, attachments);
        Assert.AreEqual("Valid", attachments[0].Name);
        Assert.EndsWith("valid.log", attachments[0].Path, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TestResultCapture_TruncatesAttachmentFieldsWithoutSplittingSurrogatePairs()
    {
        string name = new string('n', MaxIdentityFieldLength - 1) + "\U0001F642-name";
        string path = new string('p', MaxIdentityFieldLength - 1) + "\U0001F642-path.json";
        string description = new string('d', MaxMessageLength - 1) + "\U0001F642-description";
        var bag = new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new FileArtifactProperty(new FileInfo("artifact.json"), name, description));

        IReadOnlyList<CapturedAttachment>? attachments = CaptureAttachmentsForTest(bag, _ => path);

        Assert.IsNotNull(attachments);
        Assert.HasCount(1, attachments);
        AssertTruncated(attachments[0].Name, MaxIdentityFieldLength - 1);
        AssertTruncated(attachments[0].Path, MaxIdentityFieldLength - 1);
        Assert.IsNotNull(attachments[0].Description);
        AssertTruncated(attachments[0].Description!, MaxMessageLength - 1);

        static void AssertTruncated(string value, int expectedPrefixLength)
        {
            Assert.AreEqual(expectedPrefixLength, value.IndexOf('\n'));
            Assert.DoesNotContain(char.IsSurrogate, value);
            Assert.DoesNotContain("\uFFFD", value);
            Assert.Contains("[truncated, original length:", value);
        }
    }

    [DataRow("artifact.BMP", "image/bmp")]
    [DataRow("artifact.csv", "text/csv")]
    [DataRow("artifact.gif", "image/gif")]
    [DataRow("artifact.htm", "text/html")]
    [DataRow("artifact.HTML", "text/html")]
    [DataRow("artifact.jpeg", "image/jpeg")]
    [DataRow("artifact.jpg", "image/jpeg")]
    [DataRow("artifact.json", "application/json")]
    [DataRow("artifact.log", "text/plain")]
    [DataRow("artifact.txt", "text/plain")]
    [DataRow("artifact.pdf", "application/pdf")]
    [DataRow("artifact.png", "image/png")]
    [DataRow("artifact.svg", "image/svg+xml")]
    [DataRow("artifact.tif", "image/tiff")]
    [DataRow("artifact.tiff", "image/tiff")]
    [DataRow("artifact.webp", "image/webp")]
    [DataRow("artifact.xml", "application/xml")]
    [DataRow("artifact.zip", "application/zip")]
    [DataRow("artifact.unknown", "application/octet-stream")]
    [TestMethod]
    public void TestResultCapture_MapsAttachmentExtensionToContentType(string fileName, string expectedContentType)
    {
        var bag = new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new FileArtifactProperty(new FileInfo(fileName), "Artifact"));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result.Attachments);
        Assert.HasCount(1, result.Attachments);
        Assert.AreEqual(expectedContentType, result.Attachments[0].ContentType);
    }

    [TestMethod]
    public void TestResultCapture_Returns_Null_For_NonTerminalStates()
    {
        TestNode discovered = new() { Uid = "a", DisplayName = "x", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) };
        TestNode inProgress = new() { Uid = "b", DisplayName = "y", Properties = new(InProgressTestNodeStateProperty.CachedInstance) };

        Assert.IsNull(TestResultCapture.TryCapture(discovered));
        Assert.IsNull(TestResultCapture.TryCapture(inProgress));
    }

    [TestMethod]
    public async Task GenerateReportAsync_CountsAllOutcomeKindsSeparately()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("p1", "Passed", "passed"),
            Captured("f1", "Failed", "failed"),
            Captured("s1", "Skipped", "skipped"),
            CapturedRaw("e1", "Errored", "failed", "errored"),
            CapturedRaw("t1", "Timed out", "failed", "timedOut"),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement summary = document.RootElement.GetProperty("results").GetProperty("summary");
        Assert.AreEqual(5, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(3, summary.GetProperty("failed").GetInt32(), "errored + timedOut + failed all map to CTRF 'failed'.");
        Assert.AreEqual(1, summary.GetProperty("skipped").GetInt32());
    }

    [TestMethod]
    public async Task GenerateReportAsync_PreservesDuplicateUidsAsDistinctResults()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("dup", "Row A", "failed", errorMessage: "first failure"),
            Captured("dup", "Row B", "failed", errorMessage: "second failure"),
            Captured("dup", "Row C", "passed"),
            Captured("unique", "Solo", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        JsonElement testArray = results.GetProperty("tests");

        Assert.AreEqual(4, testArray.GetArrayLength(), "MTP permits distinct executions to share a UID.");

        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(4, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(2, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(2, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("flaky").GetInt32());
        Assert.AreSequenceEqual(
            ["Row A", "Row B", "Row C", "Solo"],
            testArray.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToArray());
        Assert.IsTrue(testArray.EnumerateArray().All(t => !t.TryGetProperty("retries", out _)));
        Assert.IsTrue(testArray.EnumerateArray().All(t => !t.TryGetProperty("retryAttempts", out _)));
        Assert.IsTrue(testArray.EnumerateArray().All(t => !t.TryGetProperty("flaky", out _)));
    }

    [TestMethod]
    public async Task GenerateReportAsync_CollapsesExplicitRetryAttemptsAndFlagsFlaky()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedAttachment priorAttachment = new()
        {
            Name = "first.log",
            ContentType = "text/plain",
            Path = "artifacts/first.log",
            Description = "First attempt diagnostics",
        };
        CapturedTestResult[] tests =
        [
            Captured(
                "retry",
                "Retrying",
                "failed",
                errorMessage: "first failure",
                retryAttemptNumber: 1,
                isSupersededRetryAttempt: true,
                attachments: [priorAttachment]),
            Captured(
                "retry",
                "Retrying",
                "failed",
                errorMessage: "second failure",
                retryAttemptNumber: 2,
                isSupersededRetryAttempt: true),
            Captured("retry", "Retrying", "passed", retryAttemptNumber: 3),
            Captured("unique", "Solo", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(2, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(2, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("flaky").GetInt32());

        JsonElement retry = results.GetProperty("tests")[0];
        Assert.AreEqual("passed", retry.GetProperty("status").GetString());
        Assert.AreEqual(2, retry.GetProperty("retries").GetInt32());
        Assert.IsTrue(retry.GetProperty("flaky").GetBoolean());
        JsonElement[] priorAttempts = [.. retry.GetProperty("retryAttempts").EnumerateArray()];
        Assert.HasCount(2, priorAttempts);
        Assert.AreSequenceEqual(
            [1, 2],
            priorAttempts.Select(attempt => attempt.GetProperty("attempt").GetInt32()).ToArray());
        Assert.AreSequenceEqual(
            ["first failure", "second failure"],
            priorAttempts.Select(attempt => attempt.GetProperty("message").GetString()!).ToArray());

        JsonElement attachment = Assert.ContainsSingle(priorAttempts[0].GetProperty("attachments").EnumerateArray());
        Assert.AreEqual("first.log", attachment.GetProperty("name").GetString());
        Assert.AreEqual("text/plain", attachment.GetProperty("contentType").GetString());
        Assert.AreEqual("artifacts/first.log", attachment.GetProperty("path").GetString());
        Assert.AreEqual("First attempt diagnostics", attachment.GetProperty("extra").GetProperty("description").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_PreservesIncompleteExplicitRetrySequence()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("retry", "Retrying", "failed", retryAttemptNumber: 1, isSupersededRetryAttempt: true),
            Captured("retry", "Retrying", "failed", retryAttemptNumber: 2, isSupersededRetryAttempt: true),
            Captured("unique", "Solo", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        Assert.AreEqual(3, results.GetProperty("summary").GetProperty("tests").GetInt32());
        Assert.AreEqual(3, results.GetProperty("tests").GetArrayLength());
        Assert.DoesNotContain(
            test => test.TryGetProperty("retryAttempts", out _),
            results.GetProperty("tests").EnumerateArray());
    }

    [TestMethod]
    public async Task GenerateReportAsync_CollapsesAlwaysFailingRetryWithoutFlaggingFlaky()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("retry", "Retrying", "failed", retryAttemptNumber: 1, isSupersededRetryAttempt: true),
            Captured("retry", "Retrying", "failed", retryAttemptNumber: 2),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(1, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("flaky").GetInt32());

        JsonElement retry = Assert.ContainsSingle(results.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("failed", retry.GetProperty("status").GetString());
        Assert.AreEqual(1, retry.GetProperty("retries").GetInt32());
        Assert.IsFalse(retry.TryGetProperty("flaky", out _));
        Assert.HasCount(1, retry.GetProperty("retryAttempts").EnumerateArray());
    }

    [TestMethod]
    public async Task GenerateReportAsync_PreservesAmbiguousOverlappingRetrySequences()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("same", "A", "failed", retryAttemptNumber: 1, isSupersededRetryAttempt: true),
            Captured("same", "B", "failed", retryAttemptNumber: 1, isSupersededRetryAttempt: true),
            Captured("same", "B", "passed", retryAttemptNumber: 2),
            Captured("same", "A", "passed", retryAttemptNumber: 2),
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        JsonElement testArray = results.GetProperty("tests");
        Assert.AreEqual(4, results.GetProperty("summary").GetProperty("tests").GetInt32());
        Assert.AreSequenceEqual(
            ["A", "B", "B", "A"],
            testArray.EnumerateArray().Select(test => test.GetProperty("name").GetString()!).ToArray());
        Assert.DoesNotContain(test => test.TryGetProperty("retryAttempts", out _), testArray.EnumerateArray());
    }

    [TestMethod]
    public async Task GenerateReportAsync_PerTest_ContainsRequiredFields()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "id-1",
                DisplayName = "MyTest",
                Status = "passed",
                Duration = TimeSpan.FromMilliseconds(42),
                Namespace = "MyNs",
                ClassName = "MyClass",
                MethodName = "MyMethod",
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        // CTRF spec required fields per test: name, status, duration.
        Assert.AreEqual("MyTest", test.GetProperty("name").GetString());
        Assert.AreEqual("passed", test.GetProperty("status").GetString());
        Assert.AreEqual(42, test.GetProperty("duration").GetInt64());

        // CTRF `suite` (array of strings) when class/namespace are known.
        JsonElement suite = test.GetProperty("suite");
        Assert.AreEqual(2, suite.GetArrayLength());
        Assert.AreEqual("MyNs", suite[0].GetString());
        Assert.AreEqual("MyClass", suite[1].GetString());

        // UID must be surfaced under `extra` for cross-tool correlation.
        Assert.AreEqual("id-1", test.GetProperty("extra").GetProperty("uid").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_OmitsSuite_WhenNoClassOrNamespace()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests = [Captured("u", "T", "passed")];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];
        Assert.IsFalse(test.TryGetProperty("suite", out _), "suite must be omitted when className/namespace are unknown (CTRF requires minItems:1).");
    }

    [TestMethod]
    public async Task GenerateReportAsync_RoundTripsErrorMessageAndStackTrace()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "u",
                DisplayName = "T",
                Status = "failed",
                Duration = TimeSpan.Zero,
                ErrorMessage = "expected 1 got 2",
                StackTrace = "at MyAssembly.MyType.MyMethod()",
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];
        Assert.AreEqual("expected 1 got 2", test.GetProperty("message").GetString());
        Assert.AreEqual("at MyAssembly.MyType.MyMethod()", test.GetProperty("trace").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_PromotesTraitsToLabelsAndTestCategoryToTags()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "id",
                DisplayName = "T",
                Status = "passed",
                Duration = TimeSpan.Zero,
                Traits =
                [
                    // Multiple [TestCategory] attributes on the same MSTest method produce
                    // repeated trait entries with the same key. Per ctrf-io/ctrf#53 the
                    // CTRF maintainer confirmed array values for top-level `labels`
                    // (spec 9.15), so multi-valued keys serialize as JSON arrays and
                    // single-valued keys serialize as scalar strings.
                    new KeyValuePair<string, string>("TestCategory", "Fast"),
                    new KeyValuePair<string, string>("TestCategory", "Smoke"),
                    new KeyValuePair<string, string>("Owner", "alice"),
                ],
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        // TestCategory values are promoted to the CTRF top-level `tags` array (spec 9.14)
        // so consumers can filter/group by category without walking the labels object.
        // The values are preserved in declaration order.
        JsonElement tags = test.GetProperty("tags");
        Assert.AreEqual(JsonValueKind.Array, tags.ValueKind);
        Assert.AreEqual(2, tags.GetArrayLength());
        Assert.AreEqual("Fast", tags[0].GetString());
        Assert.AreEqual("Smoke", tags[1].GetString());

        // All traits — including TestCategory — round-trip under the CTRF top-level
        // `labels` object (spec 9.15). Single-valued keys are emitted as scalar
        // strings; multi-valued keys are emitted as arrays of strings.
        JsonElement labels = test.GetProperty("labels");
        Assert.AreEqual(JsonValueKind.Object, labels.ValueKind);

        JsonElement testCategory = labels.GetProperty("TestCategory");
        Assert.AreEqual(JsonValueKind.Array, testCategory.ValueKind);
        Assert.AreEqual(2, testCategory.GetArrayLength());
        Assert.AreEqual("Fast", testCategory[0].GetString());
        Assert.AreEqual("Smoke", testCategory[1].GetString());

        JsonElement owner = labels.GetProperty("Owner");
        Assert.AreEqual(JsonValueKind.String, owner.ValueKind);
        Assert.AreEqual("alice", owner.GetString());

        // `extra.traits` was the previous home for these values; it is no longer
        // emitted now that the spec-defined `labels` field is populated.
        Assert.IsFalse(
            test.GetProperty("extra").TryGetProperty("traits", out _),
            "Traits are now emitted under top-level labels, not extra.traits.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_DefaultFileName_IncludesModuleNameAndTargetFramework()
    {
        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("M");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("u");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns(Path.Combine("tmp", "My.Test.Module.dll"));
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("F");
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));

        var engine = new CtrfReportEngine(new(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None));

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        const string ExpectedFileNamePattern = "^u_M_My\\.Test\\.Module_net[0-9]+(\\.[0-9]+)?_2026-02-03_04_05_06\\.ctrf\\.json$";
        Assert.AreEqual(pathSeen, finalPath);
        Assert.IsTrue(Regex.IsMatch(Path.GetFileName(finalPath), ExpectedFileNamePattern));
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitRelativePath_IsResolvedUnderResultsDirectory()
    {
        string[]? jsonFileName = [Path.Combine("nested", "custom.json")];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName, out jsonFileName)).Returns(true);

        string? pathSeen = null;
        var directories = new List<string>();
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Callback<string>(directories.Add)
            .Returns<string>(path => path);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        CtrfReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        string expectedPath = Path.Combine("out", "nested", "custom.json");
        Assert.AreEqual(expectedPath, finalPath);
        Assert.AreEqual(expectedPath, pathSeen);
        Assert.Contains(Path.Combine("out", "nested"), directories);
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitAbsolutePath_OverridesResultsDirectory()
    {
        string absolutePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        string[]? jsonFileName = [absolutePath];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName, out jsonFileName)).Returns(true);

        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns<string>(path => path);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        CtrfReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(absolutePath, finalPath);
        Assert.AreEqual(absolutePath, pathSeen);
    }

    [TestMethod]
    public async Task GenerateReportAsync_OverwritesAndWarns_When_DefaultFileExists()
    {
        // Default-name path uses the same overwrite-and-warn semantics as the explicit-name
        // path: a single, predictable rule. When the file already exists, the engine
        // overwrites it (FileMode.Create) and surfaces the CtrfReportFileExistsAndWillBeOverwritten
        // warning.
        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("M");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("u");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("app");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("F");
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var engine = new CtrfReportEngine(new(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None));

        (string finalPath, string? warning) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(pathSeen, finalPath);
        Assert.DoesNotContain("_1.ctrf.json", finalPath);
        Assert.IsNotNull(warning);
        Assert.Contains(finalPath, warning!);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PropagatesIOException_When_WriteFails()
    {
        // An IOException during the write (e.g. disk full, permission denied, path too
        // long) must propagate to the caller — there is no longer any disambiguation
        // loop that could mask such failures behind a retry budget.
        int callCount = 0;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                callCount++;
                throw new IOException("disk full");
            });

        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("M");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("u");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("app");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("F");
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var engine = new CtrfReportEngine(new(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None));

        await Assert.ThrowsExactlyAsync<IOException>(() => engine.GenerateReportAsync([Captured("a", "A", "passed")]));
        Assert.AreEqual(1, callCount);
    }

    private static CapturedTestResult Captured(
        string uid,
        string name,
        string status,
        TimeSpan? duration = null,
        string? errorMessage = null,
        int? retryAttemptNumber = null,
        bool isSupersededRetryAttempt = false,
        IReadOnlyList<CapturedAttachment>? attachments = null)
        => new()
        {
            Uid = uid,
            DisplayName = name,
            Status = status,
            Duration = duration ?? TimeSpan.Zero,
            ErrorMessage = errorMessage,
            RetryAttemptNumber = retryAttemptNumber,
            IsSupersededRetryAttempt = isSupersededRetryAttempt,
            Attachments = attachments,
        };

    private static CapturedTestResult CapturedRaw(string uid, string name, string status, string rawStatus)
        => new()
        {
            Uid = uid,
            DisplayName = name,
            Status = status,
            RawStatus = rawStatus,
            Duration = TimeSpan.Zero,
        };

    private static IReadOnlyList<CapturedAttachment>? CaptureAttachmentsForTest(
        PropertyBag properties,
        Func<FileInfo, string>? resolveFullPath = null)
    {
        MethodInfo captureAttachments = typeof(TestResultCapture).GetMethod(
            "CaptureAttachments",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        resolveFullPath ??= static fileInfo => fileInfo.FullName;
        return (IReadOnlyList<CapturedAttachment>?)captureAttachments.Invoke(null, [properties, resolveFullPath]);
    }

    [TestMethod]
    public async Task GenerateReportAsync_Environment_HasSchemaCompliantShape()
    {
        // CTRF schema (additionalProperties: false on environment):
        //   * `extra` MUST be an object — emitting a string here breaks strict validators.
        //   * `osPlatform` is the short identifier (win32/linux/darwin/...); the full
        //     descriptive string belongs in `osVersion`.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests = [Captured("u", "T", "passed")];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement env = document.RootElement.GetProperty("results").GetProperty("environment");

        // `extra` must be an object (the most common spec violation).
        JsonElement extra = env.GetProperty("extra");
        Assert.AreEqual(JsonValueKind.Object, extra.ValueKind, "environment.extra MUST be a JSON object per CTRF schema.");
        Assert.AreEqual("user", extra.GetProperty("user").GetString());
        Assert.AreEqual("MachineName", extra.GetProperty("machine").GetString());
        Assert.AreEqual(0, extra.GetProperty("exitCode").GetInt32());
        Assert.AreEqual("TestAppPath", extra.GetProperty("testApplication").GetString());

        // `osPlatform` is one of the short identifiers, not the descriptive name.
        string osPlatform = env.GetProperty("osPlatform").GetString()!;
        Assert.Contains(osPlatform, ["win32", "linux", "darwin", "freebsd", "unknown"]);

        // Descriptive OS string goes into `osVersion`.
        Assert.IsGreaterThan(0, env.GetProperty("osVersion").GetString()!.Length);
    }

    [TestMethod]
    public async Task GenerateReportAsync_TestExtra_CarriesMethodNameAndExceptionType()
    {
        // method, exceptionType, and uid all live under `extra` so the per-test object
        // surfaces framework-defined metadata next to the CTRF-defined `labels`
        // (spec 9.15) and `tags` (spec 9.14) fields. A test with no traits should
        // emit none of those optional fields.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "u-1",
                DisplayName = "T",
                Status = "failed",
                Duration = TimeSpan.Zero,
                MethodName = "MyMethod",
                ExceptionType = "System.InvalidOperationException",
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        JsonElement extra = test.GetProperty("extra");
        Assert.AreEqual("u-1", extra.GetProperty("uid").GetString());
        Assert.AreEqual("MyMethod", extra.GetProperty("method").GetString());
        Assert.AreEqual("System.InvalidOperationException", extra.GetProperty("exceptionType").GetString());

        // No `labels` is emitted when there are no traits at all (spec 9.15).
        Assert.IsFalse(test.TryGetProperty("labels", out _), "labels is only emitted when traits exist.");

        // No `tags` is emitted when there is no TestCategory trait, and no `traits` is
        // emitted under `extra` (we no longer write `extra.traits` — see labels above).
        Assert.IsFalse(test.TryGetProperty("tags", out _), "tags is only emitted when TestCategory traits exist.");
        Assert.IsFalse(extra.TryGetProperty("traits", out _), "extra.traits is no longer emitted; traits go to top-level labels.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_ToolName_FallsBackToUnknown_WhenFrameworkDisplayNameEmpty()
    {
        // CTRF spec: results.tool.name MUST be a non-empty string.
        using var memoryStream = new MemoryFileStream();
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns(string.Empty);
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests = [Captured("u", "T", "passed")];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement tool = document.RootElement.GetProperty("results").GetProperty("tool");
        string name = tool.GetProperty("name").GetString()!;
        Assert.IsGreaterThan(0, name.Length, "CTRF requires tool.name to be a non-empty string.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_EmptyResults_ProducesValidDocument()
    {
        // The summary block must still be present with zeroed counts when no tests
        // ran (the schema requires summary fields to exist, and `tests[]` should be
        // an empty array rather than absent).
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);

        await engine.GenerateReportAsync([]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement results = document.RootElement.GetProperty("results");
        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(0, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("skipped").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("pending").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("other").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("flaky").GetInt32());

        JsonElement testsArray = results.GetProperty("tests");
        Assert.AreEqual(JsonValueKind.Array, testsArray.ValueKind);
        Assert.AreEqual(0, testsArray.GetArrayLength());
    }

    [TestMethod]
    public async Task GenerateReportAsync_TestName_IsNeverEmpty()
    {
        // CTRF schema: tests[i].name has minLength: 1. We must surface a non-empty
        // value even if the test framework forwarded an empty DisplayName.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "uid-with-no-display-name",
                DisplayName = string.Empty,
                Status = "passed",
                Duration = TimeSpan.Zero,
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];
        string name = test.GetProperty("name").GetString()!;
        Assert.IsGreaterThan(0, name.Length, "CTRF requires tests[].name to be non-empty (minLength: 1).");
        Assert.AreEqual("uid-with-no-display-name", name, "Empty DisplayName must fall back to UID.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_SpecialCharactersInName_AreSafelyEscaped()
    {
        // Test that names with HTML/JS metacharacters are escaped (no
        // UnsafeRelaxedJsonEscaping). The bytes must contain a unicode-escaped
        // form rather than the raw `<script>` payload so downstream CTRF
        // consumers embedding into HTML/JS contexts can't be XSS'd.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("u-xss", "<script>alert('x')</script>", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        byte[] raw = System.Text.Encoding.UTF8.GetBytes(memoryStream.GetUtf8Content());
        string rawString = System.Text.Encoding.UTF8.GetString(raw);

        // The raw `<` must not appear in the encoded JSON — it must be \u003C.
        Assert.IsFalse(rawString.Contains("<script>", StringComparison.Ordinal), "JSON output must escape `<` for HTML/JS-safe consumption.");

        // The string round-trips correctly through JsonDocument so we still
        // emit a structurally valid value.
        using var document = JsonDocument.Parse(raw);
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];
        Assert.AreEqual("<script>alert('x')</script>", test.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_SplitsStandardOutputAndError_OnNewlines()
    {
        // CTRF schema types stdout/stderr as an array of "lines of output";
        // we must split on LF (handling CRLF) and not include a trailing empty
        // entry for inputs that end with a newline.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "id-multiline",
                DisplayName = "MultiLine",
                Status = "passed",
                Duration = TimeSpan.Zero,
                StandardOutput = "line1\nline2\r\nline3\n",
                StandardError = "errA\nerrB",
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        JsonElement stdout = test.GetProperty("stdout");
        Assert.AreEqual(JsonValueKind.Array, stdout.ValueKind);
        Assert.AreEqual(3, stdout.GetArrayLength(), "Trailing newline must not produce an extra empty entry.");
        Assert.AreEqual("line1", stdout[0].GetString());
        Assert.AreEqual("line2", stdout[1].GetString(), "CR before LF must be stripped (CRLF normalization).");
        Assert.AreEqual("line3", stdout[2].GetString());

        JsonElement stderr = test.GetProperty("stderr");
        Assert.AreEqual(2, stderr.GetArrayLength());
        Assert.AreEqual("errA", stderr[0].GetString());
        Assert.AreEqual("errB", stderr[1].GetString(), "Final segment without trailing newline must still be emitted.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_SingleLineOutput_EmitsOneArrayEntry()
    {
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "id-single",
                DisplayName = "SingleLine",
                Status = "passed",
                Duration = TimeSpan.Zero,
                StandardOutput = "only-line",
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        JsonElement stdout = test.GetProperty("stdout");
        Assert.AreEqual(1, stdout.GetArrayLength());
        Assert.AreEqual("only-line", stdout[0].GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_RunId_UsesTheSharedLogicalRunId()
    {
        // ctrf-io/ctrf#58: every process of one logical run — notably the successive attempts of
        // --retry-failed-tests — must stamp the same runId so consumers can tie those documents back
        // together. The retry orchestrator publishes that id through this environment variable.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_LOGICAL_RUN_ID")).Returns("run-42");
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID")).Returns("execution-7");

        await engine.GenerateReportAsync([Captured("p1", "Passing test", "passed")]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        Assert.AreEqual("run-42", document.RootElement.GetProperty("runId").GetString());

        // The run and the document are different things: a correlated run must not leak into the artifact id.
        Assert.AreNotEqual("run-42", document.RootElement.GetProperty("reportId").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_RunId_FallsBackToTheDotnetTestExecutionId()
    {
        // The execution id identifies this test application's own process tree. It is per root test application,
        // not per 'dotnet test' invocation, so it correlates a module with its child processes — not with the
        // sibling modules of a multi-project run, which legitimately report different logical runs.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_LOGICAL_RUN_ID")).Returns((string?)null);
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID")).Returns("execution-7");

        await engine.GenerateReportAsync([Captured("p1", "Passing test", "passed")]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        Assert.AreEqual("execution-7", document.RootElement.GetProperty("runId").GetString());
        Assert.AreNotEqual("execution-7", document.RootElement.GetProperty("reportId").GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_RunId_IsGenerated_WhenNothingCorrelatedTheProcess()
    {
        // A standalone run is its own logical run, and CTRF requires runId to be a non-empty string when present.
        using var memoryStream = new MemoryFileStream();
        CtrfReportEngine engine = CreateEngine(memoryStream);
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_LOGICAL_RUN_ID")).Returns((string?)null);
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID")).Returns((string?)null);

        await engine.GenerateReportAsync([Captured("p1", "Passing test", "passed")]);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        string runId = document.RootElement.GetProperty("runId").GetString()!;
        Assert.IsTrue(Guid.TryParse(runId, out _), $"Expected a generated id, got '{runId}'.");
        Assert.AreNotEqual(document.RootElement.GetProperty("reportId").GetString(), runId);
    }

    private CtrfReportEngine CreateEngine(MemoryFileStream stream, bool isIncomplete = false)
    {
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>())).Returns(stream);

        return CreateEngine(isIncomplete);
    }

    private CtrfReportEngine CreateEngine(bool isIncomplete = false)
    {
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("user");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("Fake");

        return new CtrfReportEngine(new(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None,
            IsIncomplete: isIncomplete));
    }

    internal sealed class MemoryFileStream : IFileStream
    {
        public MemoryFileStream() => Stream = new MemoryStream();

        public MemoryStream Stream { get; }

        Stream IFileStream.Stream => Stream;

        string IFileStream.Name => string.Empty;

        public string GetUtf8Content() => Encoding.UTF8.GetString(Stream.ToArray());

        void IDisposable.Dispose() => Stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => Stream.DisposeAsync();
#endif
    }
}
