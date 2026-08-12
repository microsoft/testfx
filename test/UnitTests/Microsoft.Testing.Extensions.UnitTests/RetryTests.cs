#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public class RetryTests
{
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

    [TestMethod]
    public async Task ProcessAsync_WithNoRegisteredProcessors_ReturnsEmptyDictionary()
    {
        var serviceProvider = new ServiceProvider();

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            new Mock<IOutputDevice>().Object,
            new Mock<ILogger>().Object,
            [new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null)],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoAttempts_ReturnsEmptyDictionaryWithoutInvokingProcessor()
    {
        var serviceProvider = new ServiceProvider();
        bool processorInvoked = false;
        serviceProvider.AddService(new TestArtifactPostProcessor(
            ["trx"],
            (_, _, _, _) =>
            {
                processorInvoked = true;
                return Task.FromResult<ProcessedArtifact?>(null);
            }));

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            new Mock<IOutputDevice>().Object,
            new Mock<ILogger>().Object,
            [new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null)],
            attemptCount: 1,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.IsFalse(processorInvoked);
    }

    [TestMethod]
    public async Task ProcessAsync_WithIncompleteAttemptCoverage_SkipsProcessorAndReturnsEmptyDictionary()
    {
        var serviceProvider = new ServiceProvider();
        bool processorInvoked = false;
        serviceProvider.AddService(new TestArtifactPostProcessor(
            ["trx"],
            (_, _, _, _) =>
            {
                processorInvoked = true;
                return Task.FromResult<ProcessedArtifact?>(null);
            }));

        // Only attempt 1 contributed a "trx" artifact even though there were 2 attempts overall: the merge is not
        // authoritative and must be skipped rather than produce a misleadingly incomplete merged report.
        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            new Mock<IOutputDevice>().Object,
            new Mock<ILogger>().Object,
            [new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null)],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.IsFalse(processorInvoked);
    }

    [TestMethod]
    public async Task ProcessAsync_WithProcessorReturningNull_SkipsWithoutRecordingReplacement()
    {
        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(new TestArtifactPostProcessor(
            ["trx"],
            (_, _, _, _) => Task.FromResult<ProcessedArtifact?>(null)));

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            new Mock<IOutputDevice>().Object,
            new Mock<ILogger>().Object,
            [
                new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null),
                new RetryAttemptArtifact("report2.trx", "trx", 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
    }

    [TestMethod]
    public async Task ProcessAsync_WithSuccessfulMerge_RecordsReplacementForFinalAttemptArtifact()
    {
        string outputDirectory = Path.GetTempPath();
        string mergedPath = Path.Combine(outputDirectory, "merged.trx");
        File.WriteAllText(mergedPath, "<merged/>");
        try
        {
            var serviceProvider = new ServiceProvider();
            serviceProvider.AddService(new TestArtifactPostProcessor(
                ["trx"],
                (inputs, _, _, _) =>
                {
                    Assert.HasCount(2, inputs);
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(mergedPath, "trx", "Merged", "Merged trx"));
                }));

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                new Mock<IOutputDeviceDataProducer>().Object,
                new Mock<IOutputDevice>().Object,
                new Mock<ILogger>().Object,
                [
                    new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null),
                    new RetryAttemptArtifact("report2.trx", "trx", 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(Path.GetFullPath(mergedPath), replacements["report2.trx"]);
        }
        finally
        {
            File.Delete(mergedPath);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenProcessorThrows_LogsWarningAndDisplaysMessageWithoutThrowing()
    {
        var serviceProvider = new ServiceProvider();
        var exception = new InvalidOperationException("boom");
        serviceProvider.AddService(new TestArtifactPostProcessor(
            ["trx"],
            (_, _, _, _) => throw exception));
        var logger = new Mock<ILogger>();
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null),
                new RetryAttemptArtifact("report2.trx", "trx", 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        outputDevice.Verify(
            device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.Is<IOutputDeviceData>(data => data is WarningMessageOutputDeviceData),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenProcessorThrowsOperationCanceledExceptionWithCancellationRequested_Rethrows()
    {
        var serviceProvider = new ServiceProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        serviceProvider.AddService(new TestArtifactPostProcessor(
            ["trx"],
            (_, _, _, _) => throw new OperationCanceledException(cts.Token)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            new Mock<IOutputDeviceDataProducer>().Object,
            new Mock<IOutputDevice>().Object,
            new Mock<ILogger>().Object,
            [
                new RetryAttemptArtifact("report1.trx", "trx", 1, destinationPath: null),
                new RetryAttemptArtifact("report2.trx", "trx", 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            cts.Token));
    }
}
