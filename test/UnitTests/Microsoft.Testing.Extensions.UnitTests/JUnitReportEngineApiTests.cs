// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportEngineApiTests
{
    [TestMethod]
    public void BuildSuites_PreservesInterleavedOrderAttemptsOutcomesDurationsAndEarliestTimestamp()
    {
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var module = new Mock<ITestApplicationModuleInfo>();
        module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns("My.Module.dll");
        JUnitSuiteBuilder builder = new(module.Object, start);
        CapturedTestResult Result(string uid, string suite, string name, string outcome, int seconds, int? minute) => new()
        {
            RawUid = uid,
            Uid = uid,
            ClassName = suite,
            DisplayName = name,
            Outcome = outcome,
            Duration = TimeSpan.FromSeconds(seconds),
            StartTime = minute is int value ? start.AddMinutes(value) : null,
        };

        SuiteSet actual = builder.BuildSuites(
            [
                Result("1", "A", "duplicate", "passed", 1, 4),
                Result("2", "B", "unique", "timedOut", 2, null),
                Result("3", "A", "duplicate", "failed", 3, 2),
                Result("4", "A", "error", "errored", 4, 5),
                Result("5", "A", "cancel", "cancelled", 5, null),
                Result("6", "A", "skip", "skipped", 6, null),
            ],
            new Dictionary<string, TestResultCapture.ParentChainEntry>(),
            start.AddMinutes(10));

        Assert.AreEqual("My.Module", actual.Name);
        Assert.AreSequenceEqual(["A", "B"], actual.Suites.Select(value => value.Name));
        Assert.AreEqual(6L, actual.TotalTests);
        Assert.AreEqual(1L, actual.TotalFailures);
        Assert.AreEqual(3L, actual.TotalErrors);
        Assert.AreEqual(1L, actual.TotalSkipped);
        Assert.AreEqual(TimeSpan.FromSeconds(21), actual.TotalDuration);
        Assert.AreEqual(start, actual.Timestamp);
        Suite first = actual.Suites[0];
        Assert.AreEqual(start.AddMinutes(2), first.Timestamp);
        Assert.AreEqual(start, actual.Suites[1].Timestamp);
        Assert.AreEqual(TimeSpan.FromSeconds(19), first.TotalDuration);
        Assert.AreEqual(1, first.Failures);
        Assert.AreEqual(2, first.Errors);
        Assert.AreEqual(1, first.Skipped);
        Assert.AreSequenceEqual(["duplicate [attempt 1]", "duplicate [attempt 2]", "error", "cancel", "skip"], first.Tests.Select(value => value.Name));
        Assert.AreEqual(1, first.Tests[0].DuplicateIndex);
        Assert.AreEqual(2, first.Tests[1].DuplicateIndex);
        Assert.AreEqual(2, first.Tests[0].DuplicateOf);
        Assert.AreEqual(2, first.Tests[1].DuplicateOf);
        Assert.AreEqual("duplicate", first.Tests[0].OriginalName);
        Assert.AreEqual(0, first.Tests[2].DuplicateOf);
    }

    [TestMethod]
    public void BuildSuites_ParentFallbackMissingParentsAndCyclesProduceExactRootFirstPaths()
    {
        var module = new Mock<ITestApplicationModuleInfo>();
        module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns("My.Module.dll");
        DateTimeOffset start = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        JUnitSuiteBuilder builder = new(module.Object, start);
        var parents = new Dictionary<string, TestResultCapture.ParentChainEntry>
        {
            ["root"] = new("Root", null),
            ["parent"] = new("Parent", "root"),
            ["empty"] = new(string.Empty, null),
            ["a"] = new("A", "b"),
            ["b"] = new("B", "a"),
        };
        static CapturedTestResult Result(string uid, string? parent, string? type = null) => new()
        {
            RawUid = uid,
            Uid = uid,
            DisplayName = "leaf",
            ParentRawUid = parent,
            ClassName = type,
            MethodName = "method",
            Outcome = "passed",
        };

        SuiteSet actual = builder.BuildSuites(
            [Result("1", "parent"), Result("2", "missing"), Result("3", "empty"), Result("4", "a"), Result("5", "parent", "Explicit")],
            parents,
            start);

        Assert.AreSequenceEqual(["Parent", "My.Module", "A", "Explicit"], actual.Suites.Select(value => value.Name));
        Assert.AreEqual("Root/Parent/leaf", Assert.ContainsSingle(actual.Suites[0].Tests).TestPath);
        Assert.AreSequenceEqual(["leaf", "/leaf"], actual.Suites[1].Tests.Select(value => value.TestPath));
        Assert.AreEqual("B/A/leaf", Assert.ContainsSingle(actual.Suites[2].Tests).TestPath);
        Assert.AreEqual("Root/Parent/leaf", Assert.ContainsSingle(actual.Suites[3].Tests).TestPath);
        Assert.AreEqual("method", Assert.ContainsSingle(actual.Suites[3].Tests).Name);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    public void BuildSuites_PathCapDistinguishesImmediatelyBeforeAtAndAfterBoundary(int delta)
    {
        string name = new('x', JUnitReportEngine.MaxTestPathLength + delta);
        string actual = BuildSinglePath(name, null, new Dictionary<string, TestResultCapture.ParentChainEntry>());
        string expected = delta <= 0
            ? name
            : new string('x', JUnitReportEngine.MaxTestPathLength) + "\n…[truncated, original length: 65537]";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BuildSuites_PathTruncationNeverSplitsSurrogateAndCountsUnwrittenRemainingSegments()
    {
        string prefix = new('x', JUnitReportEngine.MaxTestPathLength - 1);
        string name = prefix + "\uD83D\uDE00tail";
        Assert.AreEqual(prefix + "\n…[truncated, original length: 65541]", BuildSinglePath(name, null, new Dictionary<string, TestResultCapture.ParentChainEntry>()));

        var parents = new Dictionary<string, TestResultCapture.ParentChainEntry>
        {
            ["parent"] = new(new string('p', JUnitReportEngine.MaxTestPathLength + 1), null),
        };
        Assert.AreEqual(
            new string('p', JUnitReportEngine.MaxTestPathLength) + "\n…[truncated, original length: 65542]",
            BuildSinglePath("leaf", "parent", parents));
    }

    [TestMethod]
    public void BuildSuites_ParentWalkHasExactDepthBudgetWithoutDroppingTheLeaf()
    {
        var parents = new Dictionary<string, TestResultCapture.ParentChainEntry>();
        for (int i = 0; i < 1100; i++)
        {
            parents[i.ToString(CultureInfo.InvariantCulture)] = new("p" + i.ToString(CultureInfo.InvariantCulture), (i + 1).ToString(CultureInfo.InvariantCulture));
        }

        string expected = string.Join("/", Enumerable.Range(0, 1024).Reverse().Select(value => "p" + value.ToString(CultureInfo.InvariantCulture))) + "/leaf";
        Assert.AreEqual(expected, BuildSinglePath("leaf", "0", parents));
    }

    private static string BuildSinglePath(string name, string? parent, IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parents)
    {
        var module = new Mock<ITestApplicationModuleInfo>();
        module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns("My.Module.dll");
        DateTimeOffset start = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        JUnitSuiteBuilder builder = new(module.Object, start);
        SuiteSet result = builder.BuildSuites(
            [new CapturedTestResult { RawUid = "test", Uid = "test", DisplayName = name, ParentRawUid = parent, Outcome = "passed" }],
            parents,
            start);
        return Assert.ContainsSingle(Assert.ContainsSingle(result.Suites).Tests).TestPath;
    }

    [TestMethod]
    public void XmlSafeText_RemainsInternalAndSanitizesInvalidCharacters()
        => Assert.AreEqual("a\uFFFDb", JUnitReportEngine.XmlSafeText("a\u0001b"));

    [TestMethod]
    public async Task GenerateReportAsync_UsesReportEngineContextDependencies()
    {
        using var stream = new MemoryFileStream();
        var fileSystemMock = new Mock<IFileSystem>();
        var moduleInfoMock = new Mock<ITestApplicationModuleInfo>();
        var environmentMock = new Mock<IEnvironment>();
        var commandLineOptionsMock = new Mock<ICommandLineOptions>();
        var configurationMock = new Mock<IConfiguration>();
        var clockMock = new Mock<IClock>();
        var testFrameworkMock = new Mock<ITestFramework>();

        _ = fileSystemMock.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _ = fileSystemMock.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = moduleInfoMock.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("My.Test.Module.dll");
        _ = configurationMock.SetupGet(x => x[It.IsAny<string>()]).Returns("/results");
        _ = testFrameworkMock.SetupGet(x => x.Uid).Returns("fake-uid");
        _ = testFrameworkMock.SetupGet(x => x.Version).Returns("1.2.3");
        _ = testFrameworkMock.SetupGet(x => x.DisplayName).Returns("Fake");

        var engine = new JUnitReportEngine(new(
            fileSystemMock.Object,
            moduleInfoMock.Object,
            environmentMock.Object,
            commandLineOptionsMock.Object,
            configurationMock.Object,
            clockMock.Object,
            testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None));

        (string fileName, string? warning) = await engine.GenerateReportAsync([], new Dictionary<string, TestResultCapture.ParentChainEntry>());

        // The production code combines the results directory with the file name via Path.Combine,
        // so the separator is platform-specific ('/' on Unix, '\' on Windows).
        string expectedPrefix = "/results" + Path.DirectorySeparatorChar;

        Assert.IsNull(warning);
        Assert.StartsWith(expectedPrefix, fileName);
        Assert.EndsWith(".xml", fileName);
        fileSystemMock.Verify(x => x.NewFileStream(It.Is<string>(path => path.StartsWith(expectedPrefix) && path.EndsWith(".tmp")), FileMode.Create), Times.Once);
        fileSystemMock.Verify(x => x.MoveFile(It.Is<string>(path => path.StartsWith(expectedPrefix) && path.EndsWith(".tmp")), fileName, overwrite: true), Times.Once);
    }

    [TestMethod]
    public async Task GenerateReportAsync_WhenRecoveredAfterCrash_WritesMetadataOnlyIncompleteSuite()
    {
        using var stream = new MemoryFileStream();
        var fileSystem = new Mock<IFileSystem>();
        var moduleInfo = new Mock<ITestApplicationModuleInfo>();
        var environment = new Mock<IEnvironment>();
        var commandLineOptions = new Mock<ICommandLineOptions>();
        var configuration = new Mock<IConfiguration>();
        var clock = new Mock<IClock>();
        var testFramework = new Mock<ITestFramework>();
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        moduleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("My.Test.Module.dll");
        configuration.SetupGet(x => x[It.IsAny<string>()]).Returns("/results");
        testFramework.SetupGet(x => x.DisplayName).Returns("Fake");

        var engine = new JUnitReportEngine(new(
            fileSystem.Object,
            moduleInfo.Object,
            environment.Object,
            commandLineOptions.Object,
            configuration.Object,
            clock.Object,
            testFramework.Object,
            DateTimeOffset.UtcNow,
            137,
            CancellationToken.None,
            IsIncomplete: true));

        await engine.GenerateReportAsync([], new Dictionary<string, TestResultCapture.ParentChainEntry>());

        string xml = Encoding.UTF8.GetString(stream.Stream.ToArray());
        XElement root = XDocument.Parse(xml).Root!;
        Assert.IsNull(root.Attribute("exit-code"));
        Assert.IsNull(root.Attribute("status"));
        Assert.IsNull(root.Attribute("incomplete"));
        XElement suite = Assert.ContainsSingle(root.Elements("testsuite"));
        Assert.AreEqual("0", suite.Attribute("tests")!.Value);
        Assert.AreEqual("137", ReadProperty(suite, "exit-code"));
        Assert.AreEqual("aborted", ReadProperty(suite, "run-status"));
        Assert.AreEqual("true", ReadProperty(suite, "incomplete"));
    }

    private static string? ReadProperty(XElement suite, string name)
        => suite.Element("properties")?
            .Elements("property")
            .SingleOrDefault(property => property.Attribute("name")?.Value == name)?
            .Attribute("value")?
            .Value;

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
