// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

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
    public async Task TrxReportEngine_GenerateReportAsyncWithNullAdapterSupportTrxCapability_TrxDoesNotContainClassName()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.IsFalse(trxContent.Contains(@"className="));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithNotExecutedTests_TrxExecutedTestsCountHasIt()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream, notExecutedTestsCount: 1);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.IsTrue(trxContent.Contains(@"notExecuted=""1"""));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsyncWithTimeoutTests_TrxTimeoutTestsCountHasIt()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream, timeoutTestsCount: 1);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.IsTrue(trxContent.Contains(@"timeout=""1"""));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArgumentTrxReportFileName_FileIsCorrectlyGenerated()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        string[]? argumentTrxReportFileName = ["argumentTrxReportFileName"];
        _ = _commandLineOptionsMock.Setup(_ => _.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out argumentTrxReportFileName)).Returns(true);
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream, isExplicitFileName: true);
        _ = _fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream, isExplicitFileName: true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        Assert.IsTrue(fileName.Equals("_NUL", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithTestHostCrash_ResultSummaryOutcomeIsFailed()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(new PassedTestNodeStateProperty());
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync(isTestHostCrashed: true);

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 0, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 1, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
            new TrxMessagesProperty([new("error message")]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 1, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
    public async Task TrxReportEngine_GenerateReportAsync_WithTestFailed_TrxContainsDebugTrace()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        PropertyBag propertyBag = new(
            new FailedTestNodeStateProperty("test failed"),
            new TrxMessagesProperty([new("base trx message"), new StandardErrorTrxMessage("stderr trx message"), new StandardOutputTrxMessage("stdout trx message"), new DebugOrTraceTrxMessage("debug trx message")]));
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 1, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        <StdOut>base trx message
stdout trx message</StdOut>
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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 1, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(0, 1, propertyBag, memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0,
            propertyBag, memoryStream, true);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        Assert.IsTrue(trxContent.Contains(@"className=""TrxFullyQualifiedTypeName"), trxContent);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArtifactsByTestNode_TrxContainsResultFile()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0,
            new(new PassedTestNodeStateProperty(), new FileArtifactProperty(new FileInfo("fileName"), "TestMethod", "description")), memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTestResult .* testName=""TestMethod"" .* outcome=""Passed"" .*>
      <ResultFiles>
        <ResultFile path=.*fileName"" />
      </ResultFiles>
    </UnitTestResult>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithArtifactsByExtension_TrxContainsCollectorDataEntries()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        _artifactsByExtension.Add(
            new ToolTrxCompareFactory(),
            [new(new SessionUid("1"), new FileInfo("fileName"), "TestMethod", "description")]);
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0,
            new(new PassedTestNodeStateProperty()), memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

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
        TrxReportEngine trxReportEngine = new(_fileSystem.Object, _testApplicationModuleInfoMock.Object, _environmentMock.Object, _commandLineOptionsMock.Object,
            _configurationMock.Object, _clockMock.Object, [], 0, 0, 0, 0,
            _artifactsByExtension, true, _testFrameworkMock.Object, DateTime.UtcNow, 0, CancellationToken.None,
            isCopyingFileAllowed: false);

        // Act
        _ = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.AreEqual(4, retryCount);
    }

    [TestMethod]
    public async Task TrxReportEngine_GenerateReportAsync_WithMetadataProperties_TrxHandlesProperties()
    {
        // Arrange
        using MemoryFileStream memoryStream = new();
        TrxReportEngine trxReportEngine = GenerateTrxReportEngine(1, 0,
            new(
                new PassedTestNodeStateProperty(),
                new TestMetadataProperty("Owner", "ValueOfOwner"),
                new TestMetadataProperty("Priority", "5"),
                new TestMetadataProperty("MyProperty1", "MyValue1"),
                new TestMetadataProperty("MyProperty2", "MyValue2")), memoryStream);

        // Act
        (string fileName, string? warning) = await trxReportEngine.GenerateReportAsync();

        // Assert
        Assert.IsNull(warning);
        AssertExpectedTrxFileName(fileName);
        Assert.IsNotNull(memoryStream.TrxContent);
        XDocument xml = memoryStream.TrxContent;
        AssertTrxOutcome(xml, "Completed");
        string trxContent = xml.ToString();
        string trxContentsPattern = @"
    <UnitTest name=""TestMethod"" storage=""testapppath"" id=""b1e0b10f-442a-7875-e431-96fc1c27316b"" priority=""5"">
      <Execution id="".+?"" />
      <Owners>
        <Owner name=""ValueOfOwner"" />
      </Owners>
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
      <TestMethod codeBase=""TestAppPath"" adapterTypeName=""executor:///"" name=""TestMethod"" />
    </UnitTest>
 ";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
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

    private TrxReportEngine GenerateTrxReportEngine(int passedTestsCount, int failedTestsCount, PropertyBag propertyBag, MemoryFileStream memoryStream,
           bool? adapterSupportTrxCapability = null, int notExecutedTestsCount = 0, int timeoutTestsCount = 0,
           bool isExplicitFileName = false)
    {
        var testNode = new TestNodeUpdateMessage(
            new SessionUid("1"),
            new TestNode { Uid = new TestNodeUid("test()"), DisplayName = "TestMethod", Properties = propertyBag });

        TestNodeUpdateMessage[] testNodeUpdatedMessages = [testNode];

        DateTime testStartTime = DateTime.Now;
        CancellationToken cancellationToken = CancellationToken.None;

        _ = _fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        _ = _fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), isExplicitFileName ? FileMode.Create : FileMode.CreateNew))
            .Returns(memoryStream);

        _ = _configurationMock.SetupGet(_ => _[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(_ => _.MachineName).Returns("MachineName");
        _ = _testApplicationModuleInfoMock.Setup(_ => _.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");

        return new TrxReportEngine(_fileSystem.Object, _testApplicationModuleInfoMock.Object, _environmentMock.Object, _commandLineOptionsMock.Object,
                   _configurationMock.Object, _clockMock.Object, testNodeUpdatedMessages, failedTestsCount, passedTestsCount, notExecutedTestsCount, timeoutTestsCount,
                   _artifactsByExtension, adapterSupportTrxCapability, _testFrameworkMock.Object, testStartTime, 0, cancellationToken,
                   isCopyingFileAllowed: false);
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
                TrxContent = XDocument.Load(Stream);
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
