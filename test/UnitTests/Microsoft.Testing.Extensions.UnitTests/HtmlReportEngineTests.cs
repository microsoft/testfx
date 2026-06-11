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
    public void TestResultCapture_DoesNotSplitSurrogatePair_AtTruncationBoundary()
    {
        // Build a string whose (maxLength-1)-th char is the high surrogate of a pair.
        // After truncation the high surrogate must be dropped so the result is valid UTF-16.
        string prefix = new('a', TestResultCapture.MaxStandardStreamLength - 1);
        const string surrogatePair = "\uD83D\uDE00"; // 😀 — high surrogate at index maxLength-1
        string input = prefix + surrogatePair + new string('z', 10);

        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new StandardOutputProperty(input));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        CapturedTestResult result = TestResultCapture.TryCapture(node)!;

        Assert.IsNotNull(result.StandardOutput);

        // Verify truncation happened (also establishes that '\n' is present in the output).
        Assert.Contains("[truncated, original length:", result.StandardOutput);

        // Now safely index back from the truncation marker '\n'.
        int newlineIdx = result.StandardOutput!.IndexOf('\n');
        Assert.IsGreaterThan(0, newlineIdx, "Newline marker must not be at position 0 or absent.");

        // The truncated prefix must not end with a lone high surrogate.
        Assert.IsFalse(
            char.IsHighSurrogate(result.StandardOutput[newlineIdx - 1]),
            "Truncate must not leave a lone high surrogate at the cut boundary.");

        // Confirm we backed off exactly one char over the high surrogate.
        Assert.AreEqual(
            TestResultCapture.MaxStandardStreamLength - 1,
            newlineIdx,
            "Prefix should be maxLength-1 chars (backed off over the high surrogate).");
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
    public void TestResultCapture_DoesNotWalkTerminalProperties_ForNonTerminalStates()
    {
        var bag = new PropertyBag(
            DiscoveredTestNodeStateProperty.CachedInstance,
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)),
            new TimingProperty(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero)));
        TestNode node = new() { Uid = "a", DisplayName = "x", Properties = bag };

        Assert.IsNull(TestResultCapture.TryCapture(node));
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
    public async Task GenerateReportAsync_DefaultFileName_IsAsmTfmArchShape()
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

        // <asm>_<tfm>_<arch>.html — deterministic, discoverable across reruns and matrices.
        // The arch token is derived from RuntimeInformation.ProcessArchitecture so the regex stays in
        // sync with new Architecture enum values without manual maintenance.
        string archToken = Regex.Escape(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        string expectedFileNamePattern = $"^My\\.Test\\.Module_net[0-9]+(\\.[0-9]+)?_{archToken}\\.html$";
        Assert.AreEqual(pathSeen, finalPath);
        Assert.IsTrue(
            Regex.IsMatch(Path.GetFileName(finalPath), expectedFileNamePattern),
            $"File name '{Path.GetFileName(finalPath)}' does not match expected default pattern '{expectedFileNamePattern}'.");
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitRelativePath_IsResolvedUnderResultsDirectory()
    {
        string[]? htmlFileName = [Path.Combine("nested", "custom.html")];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName, out htmlFileName)).Returns(true);

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

        HtmlReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        string expectedPath = Path.Combine("out", "nested", "custom.html");
        Assert.AreEqual(expectedPath, finalPath);
        Assert.AreEqual(expectedPath, pathSeen);
        Assert.Contains(Path.Combine("out", "nested"), directories);
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitAbsolutePath_OverridesResultsDirectory()
    {
        string absolutePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".html");
        string[]? htmlFileName = [absolutePath];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName, out htmlFileName)).Returns(true);

        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns<string>(path => path);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        HtmlReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(absolutePath, finalPath);
        Assert.AreEqual(absolutePath, pathSeen);
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitFileName_ResolvesPlaceholdersAndSanitizesLeafName()
    {
        string[]? htmlFileName = [Path.Combine("nested", "report*_{pid}_{tfm}.html")];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName, out htmlFileName)).Returns(true);

        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns<string>(path => path);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        HtmlReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");
        _ = _environmentMock.SetupGet(_ => _.ProcessId).Returns(1234);

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(pathSeen, finalPath);
        string finalFileName = Path.GetFileName(finalPath);
        Assert.StartsWith("report__", finalFileName);
        Assert.Contains("1234", finalFileName);
        Assert.Contains("_net", finalFileName);
        Assert.EndsWith(".html", finalPath);
    }

    [TestMethod]
    public async Task GenerateReportAsync_ExplicitReservedFileName_SanitizesLeafName()
    {
        string[]? htmlFileName = [Path.Combine("nested", "CON.html")];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName, out htmlFileName)).Returns(true);

        string? pathSeen = null;
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns<string>(path => path);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                pathSeen = path;
                return new MemoryFileStream();
            });

        HtmlReportEngine engine = CreateEngine();
        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns("out");

        (string finalPath, _) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(pathSeen, finalPath);
        Assert.AreEqual("_CON.html", Path.GetFileName(finalPath));
    }

    [TestMethod]
    public async Task GenerateReportAsync_OverwritesAndWarns_When_DefaultFileExists()
    {
        // Default-name path uses the same overwrite-and-warn semantics as the explicit-name
        // path: a single, predictable rule. When the file already exists, the engine
        // overwrites it (FileMode.Create) and surfaces the HtmlReportFileExistsAndWillBeOverwritten
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

        (string finalPath, string? warning) = await engine.GenerateReportAsync([Captured("a", "A", "passed")]);

        Assert.AreEqual(pathSeen, finalPath);
        Assert.DoesNotContain("_1.html", finalPath);
        Assert.IsNotNull(warning);
        Assert.Contains(finalPath, warning!);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PropagatesIOException_When_WriteFails()
    {
        // An IOException during the write (e.g. disk full, permission denied, path too
        // long) must propagate to the caller — there is no longer any disambiguation
        // loop that could mask such failures behind a 5-second retry budget.
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

        return CreateEngine();
    }

    private HtmlReportEngine CreateEngine()
    {
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
