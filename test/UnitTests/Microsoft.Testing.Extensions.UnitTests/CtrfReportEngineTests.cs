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
    public async Task GenerateReportAsync_PreservesAllResultsForDuplicateUids_AndAnnotatesAttemptOf()
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
        Assert.AreEqual(4, testArray.GetArrayLength());

        // All four rows must appear (no de-duplication on UID).
        var names = new List<string>();
        foreach (JsonElement t in testArray.EnumerateArray())
        {
            names.Add(t.GetProperty("name").GetString()!);
        }

        CollectionAssert.AreEquivalent(new[] { "Row A", "Row B", "Row C", "Solo" }, names);

        // Each duplicate row carries retries=2 + attemptIndex/attemptOf in labels.
        var retries = new List<int?>();
        var attemptIndices = new List<string?>();
        foreach (JsonElement t in testArray.EnumerateArray())
        {
            string name = t.GetProperty("name").GetString()!;
            if (name.StartsWith("Row ", StringComparison.Ordinal))
            {
                Assert.AreEqual(2, t.GetProperty("retries").GetInt32(), $"Row {name} should report 2 retries (3 total attempts).");
                Assert.IsTrue(t.GetProperty("labels").TryGetProperty("attemptIndex", out JsonElement idx));
                attemptIndices.Add(idx.GetString());
                Assert.AreEqual("3", t.GetProperty("labels").GetProperty("attemptOf").GetString());
            }
        }

        CollectionAssert.AreEquivalent(new[] { "1", "2", "3" }, attemptIndices);

        // The unique UID row does not get a retries field or attempts annotation.
        JsonElement solo = default;
        foreach (JsonElement t in testArray.EnumerateArray())
        {
            if (t.GetProperty("name").GetString() == "Solo")
            {
                solo = t;
                break;
            }
        }

        Assert.IsFalse(solo.TryGetProperty("retries", out _), "Unique UIDs should not carry retries.");
        if (solo.TryGetProperty("labels", out JsonElement labels))
        {
            Assert.IsFalse(labels.TryGetProperty("attemptIndex", out _), "Unique UIDs should not carry attemptIndex.");
            Assert.IsFalse(labels.TryGetProperty("attemptOf", out _), "Unique UIDs should not carry attemptOf.");
        }
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
    public async Task GenerateReportAsync_IncludesTraitsUnderLabels()
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
                    new KeyValuePair<string, string>("Category", "FastTest"),
                    new KeyValuePair<string, string>("Owner", "alice"),
                ],
            },
        ];

        await engine.GenerateReportAsync(tests);

        using var document = JsonDocument.Parse(memoryStream.GetUtf8Content());
        JsonElement labels = document.RootElement.GetProperty("results").GetProperty("tests")[0].GetProperty("labels");
        Assert.AreEqual("FastTest", labels.GetProperty("Category").GetString());
        Assert.AreEqual("alice", labels.GetProperty("Owner").GetString());
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
