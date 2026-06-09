// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public void TestResultCapture_Truncates_OverLength_StandardOutput_AtBoundary()
    {
        string huge = new('a', TestResultCapture.MaxStandardStreamLength + 7);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new StandardOutputProperty(huge));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.StandardOutput);
        Assert.StartsWith(new string('a', TestResultCapture.MaxStandardStreamLength), result.StandardOutput!);
        Assert.Contains("[truncated, original length:", result.StandardOutput);
        Assert.Contains((TestResultCapture.MaxStandardStreamLength + 7).ToString(CultureInfo.InvariantCulture), result.StandardOutput);
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
    public async Task GenerateReportAsync_CollapsesDuplicateUidsIntoRetryAttempts_AndFlagsFlaky()
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

        // Duplicate-UID captures must collapse into a single CTRF test entry; the
        // earlier attempts are recorded as nested retryAttempts[]. Top-level
        // entries should therefore equal the number of unique UIDs.
        Assert.AreEqual(2, testArray.GetArrayLength(), "Duplicate UIDs must collapse to one tests[] row.");

        JsonElement summary = results.GetProperty("summary");
        Assert.AreEqual(2, summary.GetProperty("tests").GetInt32(), "summary.tests must count unique UIDs only.");
        Assert.AreEqual(2, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("flaky").GetInt32());

        JsonElement dupRow = default;
        JsonElement soloRow = default;
        foreach (JsonElement t in testArray.EnumerateArray())
        {
            if (t.GetProperty("name").GetString() == "Row C")
            {
                dupRow = t;
            }
            else if (t.GetProperty("name").GetString() == "Solo")
            {
                soloRow = t;
            }
        }

        Assert.AreNotEqual(JsonValueKind.Undefined, dupRow.ValueKind, "Final attempt name must surface as the collapsed test name.");
        Assert.AreEqual("passed", dupRow.GetProperty("status").GetString());
        Assert.AreEqual(2, dupRow.GetProperty("retries").GetInt32(), "retries must equal the number of prior attempts.");
        Assert.IsTrue(dupRow.GetProperty("flaky").GetBoolean(), "passed-after-failed runs must be marked flaky.");

        JsonElement retryAttempts = dupRow.GetProperty("retryAttempts");
        Assert.AreEqual(2, retryAttempts.GetArrayLength(), "retryAttempts must record every prior attempt.");

        var attemptNumbers = new List<int>();
        var attemptStatuses = new List<string>();
        foreach (JsonElement a in retryAttempts.EnumerateArray())
        {
            attemptNumbers.Add(a.GetProperty("attempt").GetInt32());
            attemptStatuses.Add(a.GetProperty("status").GetString()!);
        }

        Assert.AreSequenceEqual(new[] { 1, 2 }, attemptNumbers);
        Assert.AreSequenceEqual(new[] { "failed", "failed" }, attemptStatuses);

        // Single-attempt entries must not surface retry metadata.
        Assert.IsFalse(soloRow.TryGetProperty("retries", out _));
        Assert.IsFalse(soloRow.TryGetProperty("retryAttempts", out _));
        Assert.IsFalse(soloRow.TryGetProperty("flaky", out _));
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
    public async Task GenerateReportAsync_IncludesTraitsUnderExtraTraits_AndPromotesTestCategoryToTags()
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
                    // Multiple [TestCategory] attributes on one MSTest method produce repeated
                    // trait entries with the same key. The engine must group them as an array
                    // under extra.traits (the CTRF spec has no `labels` field, and emitting
                    // duplicate JSON keys would be non-interoperable per RFC 8259).
                    new KeyValuePair<string, string>("TestCategory", "Fast"),
                    new KeyValuePair<string, string>("TestCategory", "Smoke"),
                    new KeyValuePair<string, string>("Owner", "alice"),
                ],
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement test = document.RootElement.GetProperty("results").GetProperty("tests")[0];

        // No top-level `labels` — the CTRF Test schema doesn't define one.
        Assert.IsFalse(test.TryGetProperty("labels", out _), "labels is not part of the CTRF Test schema.");

        // TestCategory values are promoted to the CTRF top-level `tags` array so consumers
        // can filter/group by category. The values are preserved in declaration order.
        JsonElement tags = test.GetProperty("tags");
        Assert.AreEqual(JsonValueKind.Array, tags.ValueKind);
        Assert.AreEqual(2, tags.GetArrayLength());
        Assert.AreEqual("Fast", tags[0].GetString());
        Assert.AreEqual("Smoke", tags[1].GetString());

        // All traits (including TestCategory) round-trip under extra.traits as
        // { key: [value, ...] } so multi-value traits remain valid JSON.
        JsonElement traits = test.GetProperty("extra").GetProperty("traits");
        JsonElement testCategory = traits.GetProperty("TestCategory");
        Assert.AreEqual(JsonValueKind.Array, testCategory.ValueKind);
        Assert.AreEqual(2, testCategory.GetArrayLength());
        Assert.AreEqual("Fast", testCategory[0].GetString());
        Assert.AreEqual("Smoke", testCategory[1].GetString());

        JsonElement owner = traits.GetProperty("Owner");
        Assert.AreEqual(JsonValueKind.Array, owner.ValueKind);
        Assert.AreEqual(1, owner.GetArrayLength());
        Assert.AreEqual("alice", owner[0].GetString());
    }

    [TestMethod]
    public async Task GenerateReportAsync_DefaultFileName_IncludesModuleNameAndTargetFramework()
    {
        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.CreateNew))
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

        var engine = new CtrfReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None);

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
    public async Task GenerateReportAsync_AppendsDisambiguatingSuffix_When_DefaultFileExists()
    {
        var bytesSeen = new List<string>();
        int callCount = 0;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.CreateNew))
            .Returns<string, FileMode>((path, _) =>
            {
                callCount++;
                bytesSeen.Add(path);
                return callCount == 1
                    ? throw new IOException("file exists")
                    : new MemoryFileStream();
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

        var engine = new CtrfReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None);

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(2, callCount);
        Assert.AreEqual(bytesSeen[1], finalPath);
        Assert.Contains("_1.ctrf.json", finalPath);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PropagatesIOException_When_FileDoesNotExist()
    {
        int callCount = 0;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.CreateNew))
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

        var engine = new CtrfReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None);

        await Assert.ThrowsExactlyAsync<IOException>(() => engine.GenerateReportAsync([Captured("a", "A", "passed")]));
        Assert.AreEqual(1, callCount);
    }

    private static CapturedTestResult Captured(string uid, string name, string status,
        TimeSpan? duration = null, string? errorMessage = null)
        => new()
        {
            Uid = uid,
            DisplayName = name,
            Status = status,
            Duration = duration ?? TimeSpan.Zero,
            ErrorMessage = errorMessage,
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
        Assert.Contains(osPlatform, new[] { "win32", "linux", "darwin", "freebsd", "unknown" });

        // Descriptive OS string goes into `osVersion`.
        Assert.IsGreaterThan(0, env.GetProperty("osVersion").GetString()!.Length);
    }

    [TestMethod]
    public async Task GenerateReportAsync_TestExtra_CarriesMethodNameAndExceptionType()
    {
        // method, exceptionType, and uid all live under `extra` so the per-test object
        // remains aligned with the CTRF Test schema (which has no `labels` field).
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

        // No `labels` is ever emitted — it isn't part of the CTRF Test schema.
        Assert.IsFalse(test.TryGetProperty("labels", out _), "labels is not part of the CTRF Test schema.");

        // No `tags` is emitted when there is no TestCategory trait, and no `traits` is
        // emitted under `extra` when the test has no user-supplied traits at all.
        Assert.IsFalse(test.TryGetProperty("tags", out _), "tags is only emitted when TestCategory traits exist.");
        Assert.IsFalse(extra.TryGetProperty("traits", out _), "extra.traits is only emitted when traits exist.");
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

    private CtrfReportEngine CreateEngine(MemoryFileStream stream)
    {
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>())).Returns(stream);

        return CreateEngine();
    }

    private CtrfReportEngine CreateEngine()
    {
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("user");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("Fake");

        return new CtrfReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None);
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
