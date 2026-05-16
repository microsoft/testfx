// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

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
        // Arrange
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        TestNodeUpdateMessage[] tests =
        [
            CreateTestNode("p1", "Passing test", PassedTestNodeStateProperty.CachedInstance),
            CreateTestNode("f1", "Failing test", new FailedTestNodeStateProperty("expected 1, got 2")),
            CreateTestNode("s1", "Skipped test", new SkippedTestNodeStateProperty("not relevant")),
        ];

        // Act
        (string fileName, string? warning) = await engine.GenerateReportAsync(tests);

        // Assert
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
        // Arrange
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        const string Hostile = "evil</script><img src=x onerror=alert(1)>";
        TestNodeUpdateMessage[] tests =
        [
            CreateTestNode("hostile", Hostile, new FailedTestNodeStateProperty(Hostile)),
        ];

        // Act
        (string fileName, _) = await engine.GenerateReportAsync(tests);

        // Assert
        Assert.IsNotNull(fileName);
        string html = memoryStream.GetUtf8Content();

        // The literal hostile sequence MUST NOT appear in the HTML — it must be escaped to
        // \u003C / \u003E / \u0026 etc. so the browser parser cannot escape the JSON island.
        Assert.IsFalse(
            html.Contains("</script><img"),
            "Unescaped hostile content found in the report HTML, which would allow XSS.");

        // The hostile content MUST be present in escaped form.
        Assert.Contains("evil\\u003C/script\\u003E", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_TruncatesOverLongStandardOutput()
    {
        // Arrange
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        string huge = new string('a', HtmlReportEngine.MaxStandardStreamLength + 5_000);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new StandardOutputProperty(huge));
        TestNodeUpdateMessage[] tests =
        [
            new TestNodeUpdateMessage(
                new SessionUid("1"),
                new TestNode { Uid = "id", DisplayName = "Test", Properties = bag }),
        ];

        // Act
        await engine.GenerateReportAsync(tests);

        // Assert
        string html = memoryStream.GetUtf8Content();
        Assert.Contains("[truncated, original length:", html);
    }

    [TestMethod]
    public async Task GenerateReportAsync_CountsAllOutcomeKindsSeparately()
    {
        // Arrange
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);
        TestNodeUpdateMessage[] tests =
        [
            CreateTestNode("p1", "Passed", PassedTestNodeStateProperty.CachedInstance),
            CreateTestNode("f1", "Failed", new FailedTestNodeStateProperty("x")),
            CreateTestNode("s1", "Skipped", new SkippedTestNodeStateProperty("x")),
            CreateTestNode("e1", "Errored", new ErrorTestNodeStateProperty("x")),
            CreateTestNode("t1", "Timed out", new TimeoutTestNodeStateProperty("x")),
        ];

        // Act
        await engine.GenerateReportAsync(tests);

        // Assert
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

        // Same UID, three distinct results (parameterized rows / framework that doesn't
        // give unique UIDs / in-process retries — we must surface all of them).
        TestNodeUpdateMessage[] nodes =
        [
            CreateTestNode("dup", "Row A", new FailedTestNodeStateProperty("first failure")),
            CreateTestNode("dup", "Row B", new FailedTestNodeStateProperty("second failure")),
            CreateTestNode("dup", "Row C", PassedTestNodeStateProperty.CachedInstance),
            CreateTestNode("unique", "Solo", PassedTestNodeStateProperty.CachedInstance),
        ];

        await engine.GenerateReportAsync(nodes);

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
        Assert.IsTrue(soloIdx >= 0);
        string soloFragment = html.Substring(soloIdx, Math.Min(400, html.Length - soloIdx));
        Assert.IsFalse(soloFragment.Contains("\"attemptOf\""), "Unique UIDs should not carry attemptOf annotation.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_IncludesTraits_FromTestMetadataProperty()
    {
        using var memoryStream = new MemoryFileStream();
        HtmlReportEngine engine = CreateEngine(memoryStream);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new TestMetadataProperty("Category", "FastTest"));
        bag.Add(new TestMetadataProperty("Owner", "alice"));
        TestNodeUpdateMessage[] nodes =
        [
            new TestNodeUpdateMessage(new SessionUid("1"), new TestNode { Uid = "id", DisplayName = "T", Properties = bag }),
        ];

        await engine.GenerateReportAsync(nodes);

        string html = memoryStream.GetUtf8Content();
        Assert.Contains("\"traits\":[", html);
        Assert.Contains("\"key\":\"Category\"", html);
        Assert.Contains("\"value\":\"FastTest\"", html);
        Assert.Contains("\"key\":\"Owner\"", html);
        Assert.Contains("\"value\":\"alice\"", html);
    }

    private static TestNodeUpdateMessage CreateTestNode(string uid, string name, TestNodeStateProperty state)
        => new(new SessionUid("1"), new TestNode { Uid = uid, DisplayName = name, Properties = new PropertyBag(state) });

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

        public string GetUtf8Content()
        {
            // We don't dispose the underlying stream here, just read the content.
            return Encoding.UTF8.GetString(Stream.ToArray());
        }

        void IDisposable.Dispose() => Stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => Stream.DisposeAsync();
#endif
    }
}
