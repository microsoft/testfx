// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
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
public class TrxTests
{
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ICommandLineOptions> _commandLineOptionsMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension = [];

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_TrxDoesContainClassName()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        var propertyBag = new PropertyBag(new PassedTestNodeStateProperty());
        propertyBag.Add(new TrxFullyQualifiedTypeNameProperty("FqnForClassNameTest"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.Contains(@"className=""FqnForClassNameTest""", trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithNotExecutedTests_TrxExecutedTestsCountHasIt()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(SkippedTestNodeStateProperty.CachedInstance);
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.Contains(@"notExecuted=""1""", trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithFailingThenSkippedTest_TrxOutcomeShouldBeFailed()
    {
        // Arrange
        using var memoryStream = new MemoryFileStream();

        TrxTestResult[] messages = [
            CreateTestNodeUpdate("1", "Test1", new PropertyBag(new FailedTestNodeStateProperty())),
            CreateTestNodeUpdate("2", "Test2", new PropertyBag(SkippedTestNodeStateProperty.CachedInstance))
        ];

        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync(messages);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithSkippedThenFailingTest_TrxOutcomeShouldBeFailed()
    {
        // Arrange
        using var memoryStream = new MemoryFileStream();

        TrxTestResult[] messages = [
            CreateTestNodeUpdate("1", "Test1", new PropertyBag(SkippedTestNodeStateProperty.CachedInstance)),
            CreateTestNodeUpdate("2", "Test2", new PropertyBag(new FailedTestNodeStateProperty())),
        ];

        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync(messages);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithTimeoutTests_TrxTimeoutTestsCountHasIt()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new TimeoutTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
        string trxContent = xml.ToString();
        Assert.Contains(@"timeout=""1""", trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArgumentTrxReportFileName_FileIsCorrectlyGenerated()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["argumentTrxReportFileName"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        Assert.IsTrue(fileName.Equals("argumentTrxReportFileName", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArgumentTrxReportFileName_FileIsCorrectlyOverwrittenWhenExists()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["argumentTrxReportFileName"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);
        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNotNull(warning);
        Assert.AreEqual("Warning: Trx file 'argumentTrxReportFileName' already exists and will be overwritten.", warning);
        Assert.IsTrue(fileName.Equals("argumentTrxReportFileName", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithInvalidArgumentValueForTrxReportFileName_FileIsGeneratedWithNormalizedName()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["NUL"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        Assert.IsTrue(fileName.Equals("_NUL", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithPlaceholderInTrxReportFileName_PlaceholdersAreResolved()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{pname}_{pid}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        _ = _environmentMock.SetupGet(_ => _.ProcessId).Returns(1234);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        // {pname} resolves to the test app name (without extension), {pid} resolves to the process id from IEnvironment.
        Assert.AreEqual("report_TestAppPath_1234.trx", fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithUnknownPlaceholder_PlaceholderIsPreserved()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{unknown}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        // Unknown placeholders are preserved as-is by ArtifactNamingHelper.
        Assert.AreEqual("report_{unknown}.trx", fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithoutPlaceholderInFileName_FileNameUnchanged()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["plain_report.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        Assert.AreEqual("plain_report.trx", fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTimePlaceholder_TimeIsResolvedFromClock()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{time}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        _ = _clockMock.SetupGet(_ => _.UtcNow).Returns(new DateTimeOffset(2025, 9, 22, 13, 49, 34, TimeSpan.Zero));
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        Assert.AreEqual("report_2025-09-22_13-49-34.0000000.trx", fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithPlaceholderResolvingToInvalidChars_InvalidCharsAreSanitized()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{pname}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);
        // Override the test app path setup from GenerateTrxReportEngine to one whose file name part contains characters
        // (parentheses and a space) that are in InvalidFileNameChars and should be replaced with '_'.
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(Path.GetTempPath(), "bad (name).dll"));

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        // The resolved {pname} is "bad (name)" (Path.GetFileNameWithoutExtension), and the invalid characters '(', ')' and ' ' are replaced by '_'.
        Assert.AreEqual("report_bad__name_.trx", fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithAsmPlaceholder_PlaceholderIsResolved()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{asm}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        // {asm} resolves to the entry assembly name (or "unknown" if there is no entry assembly).
        // We do not assert the exact value because the entry assembly differs across runners and TFMs;
        // we only verify that the placeholder was replaced with a non-empty token and is sanitized as a valid file name.
        Assert.IsTrue(fileName.StartsWith("report_", StringComparison.Ordinal), $"Expected fileName to start with 'report_' but was '{fileName}'.");
        Assert.IsTrue(fileName.EndsWith(".trx", StringComparison.Ordinal), $"Expected fileName to end with '.trx' but was '{fileName}'.");
        Assert.DoesNotContain("{asm}", fileName, $"Expected '{{asm}}' to be resolved but was still present in '{fileName}'.");
        Assert.AreNotEqual("report_.trx", fileName, "Expected {asm} to resolve to a non-empty value.");
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTfmPlaceholder_PlaceholderIsResolved()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["report_{tfm}.trx"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        // {tfm} resolves via TargetFrameworkAttribute or RuntimeInformation.FrameworkDescription
        // (e.g. "net462", "net8.0", "net9.0"). We only assert the placeholder was replaced with a non-empty
        // token rather than the exact value, because the test runs on multiple TFMs.
        Assert.IsTrue(fileName.StartsWith("report_", StringComparison.Ordinal), $"Expected fileName to start with 'report_' but was '{fileName}'.");
        Assert.IsTrue(fileName.EndsWith(".trx", StringComparison.Ordinal), $"Expected fileName to end with '.trx' but was '{fileName}'.");
        Assert.DoesNotContain("{tfm}", fileName, $"Expected '{{tfm}}' to be resolved but was still present in '{fileName}'.");
        Assert.AreNotEqual("report_.trx", fileName, "Expected {tfm} to resolve to a non-empty value.");
        Assert.IsNotNull(memoryStream.TrxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestHostCrash_ResultSummaryOutcomeIsFailed()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)], isTestHostCrashed: true);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestSkipped_ResultSummaryOutcomeIsCompleted()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new SkippedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestFailed_WithStandardErrorTrxMessage_TrxContainsStdErr()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxMessagesProperty([new StandardErrorTrxMessage("error message")]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");

        XElement? testRun = xml.Root;
        Assert.IsNotNull(testRun);
        var nodes = testRun.Nodes().ToList();

        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Failed"" .*>
      <Output>
        <StdErr>error message</StdErr>
      </Output>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestFailed_WithoutStandardErrorTrxMessage_TrxContainsStdOut()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxMessagesProperty([new StandardOutputTrxMessage("error message")]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Failed"" .*>
      <Output>
        <StdOut>error message</StdOut>
      </Output>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithSupplementaryUnicode_TrxPreservesCharacter()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        const string emojiGrinningFace = "\U0001F600";
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxMessagesProperty([new StandardOutputTrxMessage($"stdout {emojiGrinningFace}")]),
            new TrxExceptionProperty($"message {emojiGrinningFace}", $"stack {emojiGrinningFace}"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", $"TestMethod {emojiGrinningFace}", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        XNamespace xmlNamespace = xml.Root!.Name.Namespace;
        string trxContent = xml.ToString();

        XElement unitTestResult = xml.Descendants(xmlNamespace + "UnitTestResult").Single();
        Assert.AreEqual($"TestMethod {emojiGrinningFace}", unitTestResult.Attribute("testName")?.Value);
        Assert.AreEqual($"stdout {emojiGrinningFace}", xml.Descendants(xmlNamespace + "StdOut").Single().Value);
        Assert.AreEqual($"message {emojiGrinningFace}", xml.Descendants(xmlNamespace + "Message").Single().Value);
        Assert.AreEqual($"stack {emojiGrinningFace}", xml.Descendants(xmlNamespace + "StackTrace").Single().Value);
        Assert.Contains(emojiGrinningFace, trxContent, "TRX content should preserve supplementary Unicode characters.");
        Assert.DoesNotContain(@"\ud83d\ude00", trxContent, "TRX content should not contain escaped surrogate pair.");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithUnpairedSurrogates_TrxEscapesInvalidCharacters()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string stdoutWithUnpairedSurrogates = "stdout \uD800 \uDC00";
        PropertyBag propertyBag = new(
            new PassedTestNodeStateProperty(),
            new TrxMessagesProperty([new StandardOutputTrxMessage(stdoutWithUnpairedSurrogates)]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        XNamespace xmlNamespace = xml.Root!.Name.Namespace;
        Assert.AreEqual(@"stdout \ud800 \udc00", xml.Descendants(xmlNamespace + "StdOut").Single().Value);
        Assert.Contains(@"\ud800 \udc00", xml.ToString(), "TRX content should escape unpaired surrogate characters.");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestFailed_TrxContainsDebugTrace()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxMessagesProperty([new StandardErrorTrxMessage("stderr trx message"), new StandardOutputTrxMessage("stdout trx message"), new DebugOrTraceTrxMessage("debug trx message")]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Failed"" .*>
      <Output>
        <StdOut>stdout trx message</StdOut>
        <StdErr>stderr trx message</StdErr>
        <DebugTrace>debug trx message</DebugTrace>
      </Output>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestFailed_WithoutStandardErrorTrxMessage_TrxContainsErrorInfo()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxExceptionProperty("trx exception message", "trx stack trace"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Failed"" .*>
      <Output>
        <ErrorInfo>
          <Message>trx exception message</Message>
          <StackTrace>trx stack trace</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_PassedTestWithTestCategory_TrxContainsTestCategory()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new PassedTestNodeStateProperty(),
            new TrxCategoriesProperty(["category1"]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTest name=""TestMethod"" .*>
      <TestCategory>
        <TestCategoryItem TestCategory=""category1"" />
      </TestCategory>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_FailedTestWithTestCategory_TrxContainsTestCategory()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty(),
            new TrxCategoriesProperty(["category1"]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Failed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTest name=""TestMethod"" .*>
      <TestCategory>
        <TestCategoryItem TestCategory=""category1"" />
      </TestCategory>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithAdapterSupportTrxCapability_TrxContainsClassName()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new PassedTestNodeStateProperty(),
            new TrxFullyQualifiedTypeNameProperty("TrxFullyQualifiedTypeName"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.Contains(@"className=""TrxFullyQualifiedTypeName", trxContent, trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArtifactsByTestNode_TrxContainsResultFile()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        var propertyBag = new PropertyBag(new PassedTestNodeStateProperty(), new FileArtifactProperty(new FileInfo("fileName"), "TestMethod", "description"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string relativeResultsDirectory = xml.Descendants().Single(x => x.Name.LocalName == "UnitTestResult").Attribute("relativeResultsDirectory")!.Value;
        string expectedDestinationSuffix = Path.Combine("_MachineName_0001-01-01_00_00_00.0000000", "In", relativeResultsDirectory, "MachineName", "fileName");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Passed"" .*>
      <ResultFiles>
        <ResultFile path=.*fileName"" />
      </ResultFiles>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
        _fileSystem.Verify(
            x => x.CopyFile(
                It.Is<string>(source => source.EndsWith("fileName", StringComparison.Ordinal)),
                It.Is<string>(destination => destination.EndsWith(
                    expectedDestinationSuffix,
                    StringComparison.Ordinal))),
            Times.Once);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArtifactsByExtension_TrxContainsCollectorDataEntries()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        _artifactsByExtension.Add(
            new ToolTrxCompareFactory(),
            [new(new SessionUid("1"), new FileInfo("fileName"), "TestMethod", "description")]);

        var propertyBag = new PropertyBag(new PassedTestNodeStateProperty());

        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <CollectorDataEntries>
      <Collector agentName=.*>
        <UriAttachments>
          <UriAttachment>
            <A href=.*fileName"" />
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_FileAlreadyExists_WillRetry()
    {
        // Arrange
        int retryCount = 0;
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.CreateNew))
            .Returns(() =>
            {
                if (retryCount > 3)
                {
                    return new MemoryFileStream();
                }

                retryCount++;
                throw new IOException("The process cannot access the file because it is being used by another process.");
            });

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        var trxReportEngine = new TrxReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _artifactsByExtension,
            _testFrameworkMock.Object,
            DateTime.UtcNow,
#if NETCOREAPP
            0,
            CancellationToken.None);
#else
            0);
#endif

        // Act
        _ = await trxReportEngine.GenerateReportAsync([]);

        // Assert
        Assert.AreEqual(4, retryCount);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithMetadataProperties_TrxHandlesProperties()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();

        var propertyBag = new PropertyBag(
                new PassedTestNodeStateProperty(),
                new TestMetadataProperty("Owner", "ValueOfOwner"),
                new TestMetadataProperty("Description", "Description of my test"),
                new TestMetadataProperty("Priority", "5"),
                new TestMetadataProperty("MyProperty1", "MyValue1"),
                new TestMetadataProperty("MyProperty2", "MyValue2"));

        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestMethod", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTest name=""TestMethod"" storage=""testapppath"" id=""39697f97-07bd-1f42-c69a-32f372f41ef4"" priority=""5"">
      <Execution id="".+?"" />
      <Owners>
        <Owner name=""ValueOfOwner"" />
      </Owners>
      <Description>Description of my test</Description>
      <Properties>
        <Property>
          <Key>MyProperty2</Key>
          <Value>MyValue2</Value>
        </Property>
        <Property>
          <Key>MyProperty1</Key>
          <Value>MyValue1</Value>
        </Property>
      </Properties>
      <TestMethod codeBase=""TestAppPath"" adapterTypeName=""executor:///"" className=""MyNamespace.MyClass"" name=""TestMethod"" />
    </UnitTest>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern), trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTrxTestDefinitionName_UnitTestNameIsFromTrxTestDefinitionName()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        var propertyBag = new PropertyBag(
            new PassedTestNodeStateProperty(),
            new TrxTestDefinitionName("ExplicitTestDefinitionName"));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync([CreateTestNodeUpdate("test()", "TestResultDisplayName", propertyBag)]);

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        // UnitTest/@name should use TrxTestDefinitionName, not the display name
        Assert.Contains(@"<UnitTest name=""ExplicitTestDefinitionName""", trxContent, trxContent);
        // UnitTestResult/@testName should use the display name
        Assert.Contains(@"testName=""TestResultDisplayName""", trxContent, trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithDuplicateTestIdAndDifferentExplicitTestDefinitionNames_ThrowsInvalidOperationException()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        TrxTestResult[] messages = [
            CreateTestNodeUpdate("same-uid", "DisplayName1", new PropertyBag(new PassedTestNodeStateProperty(), new TrxTestDefinitionName("ExplicitName1"))),
            CreateTestNodeUpdate("same-uid", "DisplayName2", new PropertyBag(new PassedTestNodeStateProperty(), new TrxTestDefinitionName("ExplicitName2"))),
        ];
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => trxReportEngine.GenerateReportAsync(messages));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithFallbackNameFirstThenMatchingExplicitTestDefinitionName_Succeeds()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();

        // First result has no TrxTestDefinitionName, so it falls back to the display name "MethodName".
        // Second result provides an explicit TrxTestDefinitionName "MethodName" that matches the fallback.
        TrxTestResult[] messages = [
            CreateTestNodeUpdate("same-uid", "MethodName", new PropertyBag(new PassedTestNodeStateProperty())),
            CreateTestNodeUpdate("same-uid", "MethodName", new PropertyBag(new PassedTestNodeStateProperty(), new TrxTestDefinitionName("MethodName"))),
        ];
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync(messages);

        // Assert: no exception, UnitTest/@name is "MethodName"
        Assert.IsNull(warning);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        string trxContent = xml.ToString();
        Assert.Contains(@"<UnitTest name=""MethodName""", trxContent, trxContent);
    }

    private static void AssertTrxOutcome(XDocument xml, string expectedOutcome)
    {
        Assert.IsNotNull(xml);
        XElement? testRun = xml.Root;
        Assert.IsNotNull(testRun);
        var resultSummary = (XElement?)testRun.LastNode;
        Assert.IsNotNull(resultSummary);
        XAttribute? outcome = resultSummary.FirstAttribute;
        Assert.IsNotNull(outcome);
        Assert.IsTrue(outcome.Value.Equals(expectedOutcome, StringComparison.Ordinal));
    }

    private static void AssertExpectedTrxFileName(string fileName)
           => Assert.IsTrue(fileName.Equals("_MachineName_0001-01-01_00_00_00.0000000.trx", StringComparison.Ordinal));

    private static TrxTestResult CreateTestNodeUpdate(string uid, string displayName, PropertyBag propertyBag)
    {
        if (!propertyBag.Any<TrxFullyQualifiedTypeNameProperty>())
        {
            propertyBag.Add(new TrxFullyQualifiedTypeNameProperty("MyNamespace.MyClass"));
        }

        var message = new TestNodeUpdateMessage(
                new SessionUid("1"),
                new TestNode { Uid = uid, DisplayName = displayName, Properties = propertyBag });
        return TrxTestResultExtractor.Extract(message).Result;
    }

    private TrxReportEngine GenerateTrxReportEngine(MemoryFileStream memoryStream, bool isExplicitFileName = false)
    {
        DateTime testStartTime = DateTime.Now;

        _ = _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), isExplicitFileName ? FileMode.Create : FileMode.CreateNew))
            .Returns(memoryStream);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");

        return new TrxReportEngine(
            _fileSystem.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _artifactsByExtension,
            _testFrameworkMock.Object,
            testStartTime,
#if NETCOREAPP
            0,
            CancellationToken.None);
#else
            0);
#endif
    }

    private sealed class MemoryFileStream : IFileStream
    {
        public MemoryFileStream() => Stream = new MemoryStream();

        public MemoryStream Stream { get; }

        public XDocument? TrxContent { get; private set; }

        Stream IFileStream.Stream => Stream;

        string IFileStream.Name => string.Empty;

        private void SetTrxContent()
        {
            if (TrxContent is null)
            {
                _ = Stream.Seek(0, SeekOrigin.Begin);
                if (Stream.Length != 0)
                {
                    TrxContent = XDocument.Load(Stream);
                }
            }
        }

        void IDisposable.Dispose()
        {
            SetTrxContent();
            Stream.Dispose();
        }

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            SetTrxContent();
            return Stream.DisposeAsync();
        }
#endif
    }
}
