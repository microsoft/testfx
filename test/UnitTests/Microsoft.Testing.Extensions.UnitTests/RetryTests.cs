#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using System.IO.Pipes;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public class RetryTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public void RetryPipeServer_UsesControllerAuthorizedSecurityIdentities()
    {
        ServiceProvider serviceProvider = new()
        {
            TestHostControllerAuthorizedSecurityIdentities = [ContosoPackageSid],
        };
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        serviceProvider.AddService(new TestCommandLineOptions([]));
        serviceProvider.AddService(new Mock<IFileSystem>().Object);

        var orchestrator = new RetryOrchestrator(serviceProvider);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);

        Assert.IsInstanceOfType<ITestHostControllerConnectionAuthorizationConsumer>(orchestrator);
        Assert.IsTrue(server.PipeName.StartsWith(@"LOCAL\", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task RunAttemptAsync_UsesRegisteredTestHostLauncher()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        Mock<IProcessHandler> processHandler = new(MockBehavior.Strict);
        serviceProvider.AddService(processHandler.Object);
        var launcher = new ConnectingTestHostLauncher();
        serviceProvider.AddService(launcher);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);
        List<string> arguments =
        [
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
            server.PipeName,
        ];

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            arguments,
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsFalse(result.ExitedBeforeConnect);
        Assert.AreEqual("testhost.exe", launcher.Context!.FileName);
        Assert.AreSequenceEqual(arguments, launcher.Context.Arguments);
        processHandler.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAttemptAsync_AlreadyExitedCustomHandle_DoesNotWaitForPipeTimeout()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        serviceProvider.AddService(new Mock<IProcessHandler>(MockBehavior.Strict).Object);
        serviceProvider.AddService(new AlreadyExitedTestHostLauncher(exitCode: 7));
        Mock<IOutputDevice> outputDevice = new();
        outputDevice.Setup(x => x.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            [],
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None);

        Assert.AreEqual(7, result.ExitCode);
        Assert.IsTrue(result.ExitedBeforeConnect);
        outputDevice.Verify(
            x => x.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void SnapshotAttemptArtifacts_CopiesExternalArtifactAndPreservesDestination()
    {
        string retryRoot = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde"));
        string attemptDirectory = Path.Combine(retryRoot, "1");
        string externalPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        string expectedSnapshot = Path.Combine(retryRoot, "Artifacts", "1", "0000-report.ctrf.json");
        var fileSystem = new Mock<IFileSystem>();

        IReadOnlyList<RetryAttemptArtifact> captured = RetryArtifactProcessor.SnapshotAttemptArtifacts(
            fileSystem.Object,
            [new ArtifactRequest(externalPath, "microsoft.testing.ctrf")],
            attempt: 1,
            attemptDirectory,
            retryRoot);

        RetryAttemptArtifact artifact = Assert.ContainsSingle(captured);
        Assert.AreEqual(expectedSnapshot, artifact.Path);
        Assert.AreEqual(externalPath, artifact.DestinationPath);
        Assert.AreEqual(1, artifact.Attempt);
        fileSystem.Verify(fs => fs.CreateDirectory(Path.Combine(retryRoot, "Artifacts", "1")), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(externalPath, expectedSnapshot, overwrite: true), Times.Once);
    }

    [TestMethod]
    public void SnapshotAttemptArtifacts_LeavesAttemptArtifactInPlace()
    {
        string retryRoot = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde"));
        string attemptDirectory = Path.Combine(retryRoot, "1");
        string artifactPath = Path.Combine(attemptDirectory, "report.ctrf.json");
        var fileSystem = new Mock<IFileSystem>();

        RetryAttemptArtifact captured = Assert.ContainsSingle(RetryArtifactProcessor.SnapshotAttemptArtifacts(
            fileSystem.Object,
            [new ArtifactRequest(artifactPath, "microsoft.testing.ctrf")],
            attempt: 1,
            attemptDirectory,
            retryRoot));

        Assert.AreEqual(Path.GetFullPath(artifactPath), captured.Path);
        Assert.IsNull(captured.DestinationPath);
        fileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.Never);
        fileSystem.Verify(fs => fs.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void PublishExternalArtifacts_UsesMergedReplacement()
    {
        string snapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "2", "report.ctrf.json"));
        string destinationPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        string replacementPath = Path.GetFullPath(Path.Combine("temp", "merged.ctrf.json"));
        var fileSystem = new Mock<IFileSystem>();
        var artifact = new RetryAttemptArtifact(snapshotPath, "microsoft.testing.ctrf", attempt: 2, destinationPath);

        RetryArtifactProcessor.PublishExternalArtifacts(
            fileSystem.Object,
            [artifact],
            finalAttempt: 2,
            new Dictionary<string, string> { [snapshotPath] = replacementPath });

        fileSystem.Verify(fs => fs.CreateDirectory(Path.GetDirectoryName(destinationPath)!), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(replacementPath, destinationPath, overwrite: true), Times.Once);
    }

    [TestMethod]
    public void PublishExternalArtifacts_UsesFinalSnapshotWhenNoReplacementExists()
    {
        string finalSnapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "2", "report.ctrf.json"));
        string previousSnapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "1", "report.ctrf.json"));
        string destinationPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        var fileSystem = new Mock<IFileSystem>();

        RetryArtifactProcessor.PublishExternalArtifacts(
            fileSystem.Object,
            [
                new RetryAttemptArtifact(previousSnapshotPath, "microsoft.testing.ctrf", attempt: 1, destinationPath),
                new RetryAttemptArtifact(finalSnapshotPath, "microsoft.testing.ctrf", attempt: 2, destinationPath),
                new RetryAttemptArtifact("internal.ctrf.json", "microsoft.testing.ctrf", attempt: 2, destinationPath: null),
            ],
            finalAttempt: 2,
            new Dictionary<string, string>());

        fileSystem.Verify(fs => fs.CopyFile(finalSnapshotPath, destinationPath, overwrite: true), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(previousSnapshotPath, It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        fileSystem.Verify(fs => fs.CopyFile("internal.ctrf.json", It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task MoveArtifactsAsync_NormalizesRelativePathsBeforeApplyingReplacement()
    {
        string currentAttemptDirectory = Path.Combine("TR", "Retries", "abcde", "2");
        string relativeAttemptFile = Path.Combine(currentAttemptDirectory, "report.xml");
        string replacementFile = Path.GetFullPath(Path.Combine("merged", "report.xml"));
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(fs => fs.GetFiles(currentAttemptDirectory, "*.*", SearchOption.AllDirectories))
            .Returns([relativeAttemptFile]);
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await RetrySummaryReporter.MoveArtifactsAsync(
            new Mock<IOutputDeviceDataProducer>().Object,
            outputDevice.Object,
            fileSystem.Object,
            new Mock<ILogger>().Object,
            currentAttemptDirectory,
            "TR",
            new Dictionary<string, string>
            {
                [Path.GetFullPath(relativeAttemptFile)] = replacementFile,
            },
            CancellationToken.None);

        fileSystem.Verify(
            fs => fs.CopyFile(replacementFile, Path.Combine("TR", "report.xml"), overwrite: true),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessAsync_ProcessorWithoutRetryMode_IsNotCalled()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.TestModules],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("first.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("second.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_SingleAttempt_IsNotProcessed()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [new RetryAttemptArtifact("first.report", "report", attempt: 1, destinationPath: null)],
            attemptCount: 1,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_CompleteArtifacts_UsesAttemptOrderAndMapsFinalArtifact()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(outputDirectory, "attempt-1.report");
            string finalPath = Path.Combine(outputDirectory, "attempt-2.report");
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            IReadOnlyList<InputArtifact>? capturedInputs = null;
            ArtifactPostProcessingContext? capturedContext = null;
            CancellationToken capturedCancellationToken = default;
            string? capturedOutputDirectory = null;
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                (inputs, directory, context, cancellationToken) =>
                {
                    capturedInputs = inputs;
                    capturedOutputDirectory = directory;
                    capturedContext = context;
                    capturedCancellationToken = cancellationToken;
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(processor);
            var cancellationToken = new CancellationToken(canceled: false);
            var runSummary = new ArtifactPostProcessingRunSummary(
                totalTests: 3,
                passedTests: 3,
                failedTests: 0,
                skippedTests: 0,
                duration: TimeSpan.FromSeconds(1),
                exitCode: 0,
                testModuleCount: 1);

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(finalPath, "report", attempt: 2, destinationPath: null),
                    new RetryAttemptArtifact(firstPath, "report", attempt: 1, destinationPath: null),
                ],
                attemptCount: 2,
                runSummary,
                outputDirectory,
                cancellationToken);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(Path.GetFullPath(mergedPath), replacements[finalPath]);
            Assert.IsNotNull(capturedInputs);
            Assert.AreSequenceEqual([firstPath, finalPath], capturedInputs.Select(input => input.Path));
            Assert.AreSequenceEqual(["report", "report"], capturedInputs.Select(input => input.Kind));
            Assert.AreSequenceEqual(["1", "2"], capturedInputs.Select(input => input.ExecutionId));
            Assert.IsTrue(capturedInputs.All(input =>
                input.ProducingTestModule is null
                && input.TargetFramework is null
                && input.Architecture is null));
            Assert.AreEqual(outputDirectory, capturedOutputDirectory);
            Assert.IsNotNull(capturedContext);
            Assert.AreEqual(ArtifactPostProcessingMode.RetryAttempts, capturedContext.Mode);
            Assert.AreEqual(ArtifactPostProcessingTruncationReason.None, capturedContext.TruncationReason);
            Assert.AreSame(runSummary, capturedContext.RunSummary);
            Assert.AreEqual(cancellationToken, capturedCancellationToken);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [DataRow("report", "attempt.report", true)]
    [DataRow(null, "attempt.REPORT", true)]
    [DataRow(null, "attempt.txt", false)]
    [DataRow("other", "attempt.report", false)]
    [DataRow("REPORT", "attempt.report", false)]
    [TestMethod]
    public async Task ProcessAsync_MatchesProducerKindOrUntaggedExtension(
        string? artifactKind,
        string fileName,
        bool shouldProcess)
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [".report"],
                (_, _, _, _) =>
                {
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(processor);
            string firstPath = Path.Combine(outputDirectory, "1", fileName);
            string finalPath = Path.Combine(outputDirectory, "2", fileName);

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(firstPath, artifactKind, attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(finalPath, artifactKind, attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.AreEqual(shouldProcess ? 1 : 0, processor.ProcessCallCount);
            Assert.AreEqual(shouldProcess, replacements.ContainsKey(finalPath));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ReplacementLookupUsesPlatformPathComparison()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string finalPath = Path.Combine(outputDirectory, "Attempt-2.report");
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                (_, _, _, _) =>
                {
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                CreateServiceProvider(processor),
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(Path.Combine(outputDirectory, "attempt-1.report"), "report", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(finalPath, "report", attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.AreEqual(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                replacements.ContainsKey(finalPath.ToUpperInvariant()));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_IncompleteOrDuplicateAttemptArtifacts_AreNotProcessed()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> missingAttemptReplacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null)],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);
        IReadOnlyDictionary<string, string> duplicateAttemptReplacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("attempt-1-a.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-1-b.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(missingAttemptReplacements);
        Assert.IsEmpty(duplicateAttemptReplacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_ProcessorDeclinesMerge_ReturnsNoReplacement()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(1, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_InvalidOutput_WarnsAndContinuesWithOtherKinds()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string validMergedPath = Path.Combine(outputDirectory, "valid-merged.report");
            var invalidProcessor = new TestArtifactPostProcessor(
                uid: "invalid-processor",
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["invalid"],
                supportedExtensions: [],
                processAsync: (_, _, _, _) => Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                    Path.Combine(outputDirectory, "missing.report"),
                    "invalid",
                    "Missing report",
                    description: null)));
            var validProcessor = new TestArtifactPostProcessor(
                uid: "valid-processor",
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["valid"],
                supportedExtensions: [],
                processAsync: (_, _, _, _) =>
                {
                    File.WriteAllText(validMergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        validMergedPath,
                        "valid",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(invalidProcessor, validProcessor);
            var logger = new Mock<ILogger>();
            var outputDevice = new Mock<IOutputDevice>();
            var displayed = new List<IOutputDeviceData>();
            outputDevice
                .Setup(device => device.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.IsAny<IOutputDeviceData>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                    (_, data, _) => displayed.Add(data))
                .Returns(Task.CompletedTask);
            string validFinalPath = Path.Combine(outputDirectory, "valid-2.report");

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                outputDevice.Object,
                logger.Object,
                [
                    new RetryAttemptArtifact("invalid-1.report", "invalid", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact("invalid-2.report", "invalid", attempt: 2, destinationPath: null),
                    new RetryAttemptArtifact("valid-1.report", "valid", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(validFinalPath, "valid", attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(Path.GetFullPath(validMergedPath), replacements[validFinalPath]);
            WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(
                Assert.ContainsSingle(displayed));
            Assert.Contains("invalid-processor", warning.Message);
            logger.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.Is<string>(message =>
                        message.Contains("invalid-processor", StringComparison.Ordinal)
                        && message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)),
                    null,
                    It.IsAny<Func<string, Exception?, string>>()),
                Times.Once);
            Assert.AreEqual(1, invalidProcessor.ProcessCallCount);
            Assert.AreEqual(1, validProcessor.ProcessCallCount);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_CanceledProcessor_PropagatesWithoutWarning()
    {
        var cancellationToken = new CancellationToken(canceled: true);
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: [],
            (_, _, _, _) => throw new OperationCanceledException(cancellationToken));
        ServiceProvider serviceProvider = CreateServiceProvider(processor);
        var logger = new Mock<ILogger>();
        var outputDevice = new Mock<IOutputDevice>();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            cancellationToken));

        logger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Never);
        outputDevice.Verify(
            device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProcessAsync_UnrelatedCancellation_WarnsWithoutPropagating()
    {
        const string ProcessorUid = "canceling-processor";
        const string ExceptionMessage = "The processor canceled its own operation.";
        var processor = new TestArtifactPostProcessor(
            uid: ProcessorUid,
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: [],
            processAsync: (_, _, _, _) => throw new OperationCanceledException(ExceptionMessage));
        var logger = new Mock<ILogger>();
        var outputDevice = new Mock<IOutputDevice>();
        var producer = new Mock<IOutputDeviceDataProducer>();
        WarningMessageOutputDeviceData? displayedWarning = null;
        outputDevice
            .Setup(device => device.DisplayAsync(
                producer.Object,
                It.IsAny<WarningMessageOutputDeviceData>(),
                CancellationToken.None))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayedWarning = (WarningMessageOutputDeviceData)data)
            .Returns(Task.CompletedTask);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            CreateServiceProvider(processor),
            producer.Object,
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.IsNotNull(displayedWarning);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                Policy.Resources.ExtensionResources.RetryArtifactPostProcessorFailed,
                ProcessorUid,
                ExceptionMessage),
            displayedWarning.Message);
        logger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.Is<string>(message =>
                    message.Contains(ProcessorUid, StringComparison.Ordinal)
                    && message.Contains(nameof(OperationCanceledException), StringComparison.Ordinal)
                    && message.Contains(ExceptionMessage, StringComparison.Ordinal)),
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
        outputDevice.Verify(
            device => device.DisplayAsync(
                producer.Object,
                It.IsAny<WarningMessageOutputDeviceData>(),
                CancellationToken.None),
            Times.Once);
    }

    [TestMethod]
    public void GetCommandLineOptions_PublicRetryOptions_AreExtensionOptions()
    {
        var provider = new RetryCommandLineOptionsProvider();

        foreach (CommandLineOption option in provider.GetCommandLineOptions().Where(option => !option.IsHidden))
        {
            Assert.IsFalse(option.IsBuiltIn, option.Name);
        }
    }

    [TestMethod]
    public void GetCommandLineOptions_InternalRetryOption_RemainsBuiltIn()
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().Single(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName);

        Assert.IsTrue(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "100")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "0")]
    [TestMethod]
    public async Task IsValid_If_CorrectInteger_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "-1")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "-1")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_NegativeValue_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionNonNegativeIntegerArgumentErrorMessage, optionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow("invalid")]
    [DataRow("32.32")]
    [DataRow("-1")]
    [DataRow("101")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_OutOfRangeValue_Is_Provided_For_MaxPercentageOption(string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsMaxPercentageOptionIntegerBetween0And100ArgumentErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_MaxTests_BothProvided()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsPercentageAndCountCannotBeMixedErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxTests_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow(true, false)]
    [DataRow(false, true)]
    [TestMethod]
    public async Task IsValid_When_TestOption_Provided_With_Either_MaxPercentage_MaxTests_Provided(bool isMaxPercentageSet, bool isMaxTestsSet)
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, [] },
        };
        if (isMaxPercentageSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, []);
        }

        if (isMaxTestsSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("0")]
    [DataRow("0s")]
    [DataRow("200")]
    [DataRow("1s")]
    [DataRow("2.5m")]
    [DataRow("1h")]
    [TestMethod]
    public async Task IsValid_If_CorrectTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("invalid")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("25d")]
    [TestMethod]
    public async Task IsInvalid_If_InvalidTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(Policy.Resources.ExtensionResources.RetryFailedTestsDelayOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_DelayOption_Provided_But_RetryOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_DelayOption_Provided_With_RetryOption()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, ["3"] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    private static ServiceProvider CreateServiceProvider(params IArtifactPostProcessor[] processors)
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddServices(processors);
        return serviceProvider;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"retry-artifact-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestArtifactPostProcessor : IArtifactPostProcessor
    {
        private readonly Func<
            IReadOnlyList<InputArtifact>,
            string,
            ArtifactPostProcessingContext,
            CancellationToken,
            Task<ProcessedArtifact?>> _processAsync;

        public TestArtifactPostProcessor(
            IReadOnlyList<ArtifactPostProcessingMode> supportedModes,
            IReadOnlyList<string> supportedKinds,
            IReadOnlyList<string> supportedExtensions,
            Func<
                IReadOnlyList<InputArtifact>,
                string,
                ArtifactPostProcessingContext,
                CancellationToken,
                Task<ProcessedArtifact?>>? processAsync = null,
            string uid = "test-processor")
        {
            SupportedModes = supportedModes;
            SupportedKinds = supportedKinds;
            SupportedFileExtensionsFallback = supportedExtensions;
            Uid = uid;
            _processAsync = processAsync ?? ((_, _, _, _) => Task.FromResult<ProcessedArtifact?>(null));
        }

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; }

        public bool SupportsTruncatedRuns => false;

        public IReadOnlyList<string> SupportedKinds { get; }

        public IReadOnlyList<string> SupportedFileExtensionsFallback { get; }

        public int ProcessCallCount { get; private set; }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ProcessedArtifact?> ProcessAsync(
            IReadOnlyList<InputArtifact> inputs,
            string outputDirectory,
            ArtifactPostProcessingContext context,
            CancellationToken cancellationToken)
        {
            ProcessCallCount++;
            return _processAsync(inputs, outputDirectory, context, cancellationToken);
        }
    }

    private sealed class ConnectingTestHostLauncher : ITestHostLauncher
    {
        public TestHostLaunchContext? Context { get; private set; }

        public string Uid => nameof(ConnectingTestHostLauncher);

        public string Version => "1.0.0";

        public string DisplayName => nameof(ConnectingTestHostLauncher);

        public string Description => nameof(ConnectingTestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
        {
            Context = context;
            int pipeNameIndex = context.Arguments.ToList().IndexOf($"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}") + 1;
            var pipeClient = new NamedPipeClientStream(".", context.Arguments[pipeNameIndex], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(5_000, cancellationToken);
            return new ConnectedTestHostHandle(pipeClient);
        }
    }

    private sealed class ConnectedTestHostHandle(NamedPipeClientStream pipeClient) : ITestHostHandle
    {
        public string Identifier => nameof(ConnectedTestHostHandle);

        public int ExitCode => 0;

        public bool HasExited => true;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Terminate()
        {
        }

        public void Dispose() => pipeClient.Dispose();
    }

    private sealed class AlreadyExitedTestHostLauncher(int exitCode) : ITestHostLauncher
    {
        public string Uid => nameof(AlreadyExitedTestHostLauncher);

        public string Version => "1.0.0";

        public string DisplayName => nameof(AlreadyExitedTestHostLauncher);

        public string Description => nameof(AlreadyExitedTestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
            => Task.FromResult<ITestHostHandle>(new AlreadyExitedTestHostHandle(exitCode));
    }

    private sealed class AlreadyExitedTestHostHandle(int exitCode) : ITestHostHandle
    {
        public string Identifier => nameof(AlreadyExitedTestHostHandle);

        public int ExitCode => exitCode;

        public bool HasExited => true;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }
}
