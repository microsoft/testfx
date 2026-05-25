// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class HtmlReportEngineTests
{
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ICommandLineOptions> _commandLineOptionsMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    [TestMethod]
    public async Task GenerateReportAsync_WritesValidHtml_WithEmbeddedJson()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("p1", "Passing test", "passed"),
            Captured("f1", "Failing test", "failed", errorMessage: "expected 1, got 2"),
            Captured("s1", "Skipped test", "skipped", errorMessage: "not relevant"),
        ];

        (string fileName, string? warning) = await engine.GenerateReportAsync(tests);

        Assert.IsNotNull(fileName);
        Assert.IsNull(warning);
        string html = memoryStream.GetUtf8Content();
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("id=\"mtp-data\"", html);
        Assert.Contains("\"passed\":1", html);
        Assert.Contains("\"failed\":1", html);
        Assert.Contains("\"skipped\":1", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_EscapesScriptInjection_InDisplayName()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        const string Hostile = "evil</script><img src=x onerror=alert(1)>";
        CapturedTestResult[] tests =
        [
            Captured("hostile", Hostile, "failed", errorMessage: Hostile),
        ];

        (string fileName, _) = await engine.GenerateReportAsync(tests);

        Assert.IsNotNull(fileName);
        string html = memoryStream.GetUtf8Content();

        // The literal hostile sequence MUST NOT appear in the HTML — it must be escaped to
        // \u003C / \u003E / \u0026 etc. so the browser parser cannot escape the JSON island.
        Assert.DoesNotContain("</script><img", html, "Unescaped hostile content found in the report HTML, which would allow XSS.");

        // The hostile content MUST be present in escaped form.
        Assert.Contains("evil\\u003C/script\\u003E", html);
    }

    [TestMethod]
    [DataRow('<', "\\u003C")]
    [DataRow('>', "\\u003E")]
    [DataRow('&', "\\u0026")]
    [DataRow('\'', "\\u0027")]
    public async Task GenerateReportAsync_EscapesHtmlUnsafeCharacters_AsUnicode(char raw, string expected)
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        string display = "x" + raw + "y";
        CapturedTestResult[] tests = [Captured("u", display, "passed")];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();
        Assert.Contains("x" + expected + "y", html);
        Assert.DoesNotContain("\"x" + raw + "y\"", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_EscapesLineSeparators_U2028_AndU2029()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        string display = "line1\u2028line2\u2029line3";
        CapturedTestResult[] tests = [Captured("ls", display, "passed")];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();
        Assert.Contains("line1\\u2028line2\\u2029line3", html);
        Assert.DoesNotContain("\u2028", html);
        Assert.DoesNotContain("\u2029", html);
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
    public void TestResultCapture_Does_Not_Truncate_When_Exactly_At_MaxLength()
    {
        string atMax = new('a', TestResultCapture.MaxStandardStreamLength);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new StandardOutputProperty(atMax));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual(atMax, result.StandardOutput);
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
    [DataRow("passed", typeof(PassedTestNodeStateProperty))]
    [DataRow("skipped", typeof(SkippedTestNodeStateProperty))]
    [DataRow("failed", typeof(FailedTestNodeStateProperty))]
    [DataRow("errored", typeof(ErrorTestNodeStateProperty))]
    [DataRow("timedOut", typeof(TimeoutTestNodeStateProperty))]
    public void TestResultCapture_ClassifiesEveryWellKnownTerminalOutcome(string expected, Type stateType)
    {
        TestNodeStateProperty state = stateType switch
        {
            Type t when t == typeof(PassedTestNodeStateProperty) => PassedTestNodeStateProperty.CachedInstance,
            Type t when t == typeof(SkippedTestNodeStateProperty) => SkippedTestNodeStateProperty.CachedInstance,
            Type t when t == typeof(FailedTestNodeStateProperty) => new FailedTestNodeStateProperty("x"),
            Type t when t == typeof(ErrorTestNodeStateProperty) => new ErrorTestNodeStateProperty("x"),
            Type t when t == typeof(TimeoutTestNodeStateProperty) => new TimeoutTestNodeStateProperty("x"),
            _ => throw new InvalidOperationException(),
        };

        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(state) };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.AreEqual(expected, result.Outcome);
    }

    [TestMethod]
    public async Task GenerateReportAsync_CountsAllOutcomeKindsSeparately()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("p1", "Passed", "passed"),
            Captured("f1", "Failed", "failed"),
            Captured("s1", "Skipped", "skipped"),
            Captured("e1", "Errored", "errored"),
            Captured("t1", "Timed out", "timedOut"),
        ];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();
        Assert.Contains("\"total\":5", html);
        Assert.Contains("\"passed\":1", html);
        Assert.Contains("\"failed\":1", html);
        Assert.Contains("\"skipped\":1", html);
        Assert.Contains("\"errored\":1", html);
        Assert.Contains("\"timedOut\":1", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PreservesAllResultsForDuplicateUids_AndAnnotatesAttemptOf()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("dup", "Row A", "failed", errorMessage: "first failure"),
            Captured("dup", "Row B", "failed", errorMessage: "second failure"),
            Captured("dup", "Row C", "passed"),
            Captured("unique", "Solo", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();

        // All three rows for the same UID must appear in the report.
        Assert.Contains("\"displayName\":\"Row A\"", html);
        Assert.Contains("\"displayName\":\"Row B\"", html);
        Assert.Contains("\"displayName\":\"Row C\"", html);

        // Each duplicate row carries attemptIndex/attemptOf annotation.
        Assert.Contains("\"attemptIndex\":1,\"attemptOf\":3", html);
        Assert.Contains("\"attemptIndex\":2,\"attemptOf\":3", html);
        Assert.Contains("\"attemptIndex\":3,\"attemptOf\":3", html);

        // Counts reflect every observation, not just unique UIDs.
        Assert.Contains("\"total\":4", html);
        Assert.Contains("\"failed\":2", html);
        Assert.Contains("\"passed\":2", html);

        // The unique UID row does not get an attempts annotation.
        int soloIdx = html.IndexOf("\"displayName\":\"Solo\"", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, soloIdx);
        string soloFragment = html.Substring(soloIdx, Math.Min(400, html.Length - soloIdx));
        Assert.DoesNotContain("\"attemptOf\"", soloFragment, "Unique UIDs should not carry attemptOf annotation.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_EmitsStableRowKey_Per_Result()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            Captured("a", "A", "passed"),
            Captured("a", "A2", "passed"),
            Captured("b", "B", "passed"),
        ];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();
        // The engine must emit a unique row key per result (used by the UI for expand
        // state), independent of UID, so multiple rows sharing the same UID never
        // collide and a UID like "a#1" can never collide with a derived key.
        Assert.Contains("\"rowKey\":0", html);
        Assert.Contains("\"rowKey\":1", html);
        Assert.Contains("\"rowKey\":2", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_IncludesTraits()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        CapturedTestResult[] tests =
        [
            new CapturedTestResult
            {
                Uid = "id",
                DisplayName = "T",
                Outcome = "passed",
                Duration = TimeSpan.Zero,
                Traits =
                [
                    new KeyValuePair<string, string>("Category", "FastTest"),
                    new KeyValuePair<string, string>("Owner", "alice"),
                ],
            },
        ];

        await engine.GenerateReportAsync(tests);

        string html = memoryStream.GetUtf8Content();
        Assert.Contains("\"traits\":[", html);
        Assert.Contains("\"key\":\"Category\"", html);
        Assert.Contains("\"value\":\"FastTest\"", html);
        Assert.Contains("\"key\":\"Owner\"", html);
        Assert.Contains("\"value\":\"alice\"", html);
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

        var engine = new HtmlReportEngine(
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

        const string ExpectedFileNamePattern = "^u_M_My\\.Test\\.Module_net[0-9]+(\\.[0-9]+)?_2026-02-03_04_05_06\\.html$";
        Assert.AreEqual(pathSeen, finalPath);
        Assert.IsTrue(Regex.IsMatch(Path.GetFileName(finalPath), ExpectedFileNamePattern));
    }

    [TestMethod]
    public async Task GenerateReportAsync_AppendsDisambiguatingSuffix_When_DefaultFileExists()
    {
        // Set up file system: pretend the default file already exists, then succeed on
        // the second name. The engine must retry rather than throwing IOException.
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

        // The retry only kicks in when the candidate path actually exists, so the file
        // system must report the first candidate as already on disk.
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("M");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("u");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("app");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("F");
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var engine = new HtmlReportEngine(
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
        Assert.Contains("_1.html", finalPath);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PropagatesIOException_When_FileDoesNotExist()
    {
        // Simulate an IOException that is not caused by the candidate already existing
        // (e.g. disk full, permission denied). The engine must propagate the failure
        // immediately rather than spinning up disambiguating suffixes for 5 seconds.
        int callCount = 0;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.CreateNew))
            .Returns<string, FileMode>((path, _) =>
            {
                callCount++;
                throw new IOException("disk full");
            });

        // ExistFile reports false so the IOException is not interpreted as a collision.
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("M");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("u");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("app");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("F");
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var engine = new HtmlReportEngine(
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

    [TestMethod]
    public void TestResultCapture_TruncatesIdentityFields_AtBoundary()
    {
        string huge = new('a', TestResultCapture.MaxIdentityFieldLength + 7);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new TestMethodIdentifierProperty("asm", "n", "t", huge, 0, [], "void"));
        TestNode node = new() { Uid = huge, DisplayName = huge, Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result);
        Assert.Contains("[truncated, original length:", result.Uid);
        Assert.Contains("[truncated, original length:", result.DisplayName);
        Assert.IsNotNull(result.MethodName);
        Assert.Contains("[truncated, original length:", result.MethodName!);
    }

    [TestMethod]
    public void TestResultCapture_TruncatesTraitKeysAndValues_AtBoundary()
    {
        string hugeKey = new('k', TestResultCapture.MaxTraitFieldLength + 3);
        string hugeValue = new('v', TestResultCapture.MaxTraitFieldLength + 5);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new TestMetadataProperty(hugeKey, hugeValue));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Traits);
        Assert.HasCount(1, result.Traits!);
        Assert.Contains("[truncated, original length:", result.Traits![0].Key);
        Assert.Contains("[truncated, original length:", result.Traits![0].Value);
    }

    [TestMethod]
    public async Task GenerateReportAsync_WithUserSuppliedFileName_ResolvesPlaceholders()
    {
        using var memoryStream = new MemoryFileStream();
        string[]? providedFileName = ["report_{pname}_{tfm}_{time}.html"];
        _ = _commandLineOptionsMock
            .Setup(_ => _.TryGetOptionArgumentList(HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName, out providedFileName))
            .Returns(true);
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2025, 9, 22, 13, 49, 34, TimeSpan.Zero));
        HtmlReportEngine engine = CreateEngine(memoryStream);
        // CreateEngine sets up GetCurrentTestApplicationFullPath to return "TestAppPath"; override it so the
        // resolved {pname} is something more recognizable in the assertion.
        _ = _testApplicationModuleInfoMock
            .Setup(_ => _.GetCurrentTestApplicationFullPath())
            .Returns(Path.Combine(Path.GetTempPath(), "MyTestApp.dll"));

        (string fileName, string? warning) = await engine.GenerateReportAsync([Captured("p1", "Passing test", "passed")]);

        Assert.IsNull(warning);
        Assert.IsNotNull(fileName);
        Assert.DoesNotContain("{pname}", fileName);
        Assert.DoesNotContain("{tfm}", fileName);
        Assert.DoesNotContain("{time}", fileName);
        Assert.Contains("MyTestApp", fileName);
        Assert.Contains("2025-09-22_13-49-34.0000000", fileName);
        Assert.EndsWith(".html", fileName);
    }

    private static CapturedTestResult Captured(string uid, string name, string outcome,
        TimeSpan? duration = null, string? errorMessage = null)
        => new()
        {
            Uid = uid,
            DisplayName = name,
            Outcome = outcome,
            Duration = duration ?? TimeSpan.Zero,
            ErrorMessage = errorMessage,
        };

    private HtmlReportEngine CreateEngine(MemoryFileStream stream)
    {
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>())).Returns(stream);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _environmentMock.Setup(_ => _.GetEnvironmentVariable(It.IsAny<string>())).Returns("user");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(_ => _.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(_ => _.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(_ => _.DisplayName).Returns("Fake");

        return new HtmlReportEngine(
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
