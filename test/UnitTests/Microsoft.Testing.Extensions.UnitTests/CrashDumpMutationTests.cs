// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
#if NET
using System.Reflection.Emit;
using System.Runtime.Versioning;
#endif

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpEnvironmentVariableProviderMutationTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 6, 11, 7, 8, 9, TimeSpan.Zero);

    [TestMethod]
    [DataRow(false, false, true, false)]
    [DataRow(true, false, true, true)]
    [DataRow(false, true, true, true)]
    [DataRow(true, false, false, false)]
    public async Task IsEnabledAsync_UsesRequestedFeaturesAndConfiguration(
        bool crashDump,
        bool crashReport,
        bool configurationEnabled,
        bool expected)
    {
        var options = new Dictionary<string, string[]>();
        if (crashDump)
        {
            options.Add(CrashDumpCommandLineOptions.CrashDumpOptionName, []);
        }

        if (crashReport)
        {
            options.Add(CrashDumpCommandLineOptions.CrashReportOptionName, []);
        }

        CrashDumpEnvironmentVariableProvider provider = CreateProvider(
            new TestCommandLineOptions(options),
            new CrashDumpConfiguration { Enable = configurationEnabled },
            Path.GetTempPath());

        Assert.AreEqual(expected, await provider.IsEnabledAsync());
    }

    [TestMethod]
    public async Task UpdateAsync_CrashDumpDefaults_SetEveryLockedRuntimeVariable()
    {
        string resultsDirectory = Path.Combine(Path.GetTempPath(), "crashdump-env-" + Guid.NewGuid().ToString("N"));
        var crashDumpConfiguration = new CrashDumpConfiguration();
        var options = new TestCommandLineOptions(new()
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
        });
        CrashDumpEnvironmentVariableProvider provider = CreateProvider(options, crashDumpConfiguration, resultsDirectory);
        List<EnvironmentVariable> variables = [];
        var environmentVariables = new Mock<IEnvironmentVariables>();
        environmentVariables
            .Setup(x => x.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(variables.Add);

        await provider.UpdateAsync(environmentVariables.Object);

        Assert.HasCount(7, variables);
        Assert.IsTrue(variables.All(static variable => !variable.IsSecret));
        Assert.IsTrue(variables.All(static variable => variable.IsLocked));
        AssertVariable(variables, "DOTNET_DbgEnableMiniDump", "1");
        AssertVariable(variables, "COMPlus_DbgEnableMiniDump", "1");
        AssertVariable(variables, "DOTNET_DbgMiniDumpType", "4");
        AssertVariable(variables, "COMPlus_DbgMiniDumpType", "4");

        string testApplicationName = Assembly.GetEntryAssembly()!.GetName().Name!;
        string expectedDumpName = Path.Combine(resultsDirectory, $"{testApplicationName}_%p_crash.dmp");
        AssertVariable(variables, "DOTNET_DbgMiniDumpName", expectedDumpName);
        AssertVariable(variables, "COMPlus_DbgMiniDumpName", expectedDumpName);
        Assert.AreEqual(expectedDumpName, crashDumpConfiguration.DumpFileNamePattern);

        EnvironmentVariable sequenceVariable = AssertVariable(
            variables,
            CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName);
        Assert.AreEqual(sequenceVariable.Value, crashDumpConfiguration.SequenceFileName);
        Assert.IsNotNull(sequenceVariable.Value);
        Assert.AreEqual(resultsDirectory, Path.GetDirectoryName(sequenceVariable.Value));
        Assert.IsTrue(
            Regex.IsMatch(
                Path.GetFileName(sequenceVariable.Value),
                $"^{Regex.Escape(testApplicationName)}_[0-9a-f]{{8}}_crash\\.sequence\\.log$",
                RegexOptions.CultureInvariant),
            sequenceVariable.Value);
        Assert.DoesNotContain(
            static variable => variable.Variable.EndsWith("EnableCrashReport", StringComparison.Ordinal),
            variables);
        Assert.DoesNotContain(
            static variable => variable.Variable.EndsWith("EnableCrashReportOnly", StringComparison.Ordinal),
            variables);
    }

    [TestMethod]
    [DataRow(false, "EnableCrashReportOnly", "EnableCrashReport")]
    [DataRow(true, "EnableCrashReport", "EnableCrashReportOnly")]
    public async Task UpdateAsync_CrashReportMode_SetsOnlyTheExpectedReportVariables(
        bool includeCrashDump,
        string expectedSuffix,
        string unexpectedSuffix)
    {
        var options = new Dictionary<string, string[]>
        {
            [CrashDumpCommandLineOptions.CrashReportOptionName] = [],
        };
        if (includeCrashDump)
        {
            options.Add(CrashDumpCommandLineOptions.CrashDumpOptionName, []);
        }

        CrashDumpEnvironmentVariableProvider provider = CreateProvider(
            new TestCommandLineOptions(options),
            new CrashDumpConfiguration(),
            Path.GetTempPath());
        List<EnvironmentVariable> variables = [];
        var environmentVariables = new Mock<IEnvironmentVariables>();
        environmentVariables
            .Setup(x => x.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(variables.Add);

        await provider.UpdateAsync(environmentVariables.Object);

        AssertVariable(variables, $"DOTNET_{expectedSuffix}", "1");
        AssertVariable(variables, $"COMPlus_{expectedSuffix}", "1");
        Assert.DoesNotContain(
            variable => variable.Variable.EndsWith(unexpectedSuffix, StringComparison.Ordinal),
            variables);
    }

    [TestMethod]
    public async Task UpdateAsync_CrashSequenceOff_DoesNotConfigureASequenceFile()
    {
        var options = new TestCommandLineOptions(new()
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
            [CrashDumpCommandLineOptions.CrashSequenceOptionName] = ["off"],
        });
        var crashDumpConfiguration = new CrashDumpConfiguration();
        CrashDumpEnvironmentVariableProvider provider = CreateProvider(options, crashDumpConfiguration, Path.GetTempPath());
        List<EnvironmentVariable> variables = [];
        var environmentVariables = new Mock<IEnvironmentVariables>();
        environmentVariables
            .Setup(x => x.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(variables.Add);

        await provider.UpdateAsync(environmentVariables.Object);

        Assert.IsNull(crashDumpConfiguration.SequenceFileName);
        Assert.DoesNotContain(
            static variable => variable.Variable == CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName,
            variables);
    }

    [TestMethod]
    [DataRow("plain.dmp", @"^plain\.dmp_[0-9a-f]{8}\.sequence\.log$")]
    [DataRow("%p%e%h%t", @"^_[0-9a-f]{8}\.sequence\.log$")]
    [DataRow("prefix_%%_%p_%e_trailing%.dmp", @"^prefix_%___trailingdmp_[0-9a-f]{8}\.sequence\.log$")]
    [DataRow("%%%p", @"^%_[0-9a-f]{8}\.sequence\.log$")]
    [DataRow("trailing%", @"^trailing%_[0-9a-f]{8}\.sequence\.log$")]
    public async Task UpdateAsync_CustomDumpPattern_DerivesConcreteSequenceFileName(string userPattern, string expectedFileNamePattern)
    {
        var options = new TestCommandLineOptions(new()
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
            [CrashDumpCommandLineOptions.CrashDumpFileNameOptionName] = [userPattern],
        });
        var crashDumpConfiguration = new CrashDumpConfiguration();
        CrashDumpEnvironmentVariableProvider provider = CreateProvider(options, crashDumpConfiguration, Path.GetTempPath());
        var environmentVariables = new Mock<IEnvironmentVariables>();

        await provider.UpdateAsync(environmentVariables.Object);

        Assert.IsNotNull(crashDumpConfiguration.SequenceFileName);
        Assert.IsTrue(
            Regex.IsMatch(
                Path.GetFileName(crashDumpConfiguration.SequenceFileName),
                expectedFileNamePattern,
                RegexOptions.CultureInvariant),
            crashDumpConfiguration.SequenceFileName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UpdateAsync_TraceLoggingHonorsLoggerLevel(bool traceEnabled)
    {
        var options = new TestCommandLineOptions(new()
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
        });
        var crashDumpConfiguration = new CrashDumpConfiguration();
        Mock<ILogger> logger = new();
        logger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(traceEnabled);
        logger
            .Setup(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        CrashDumpEnvironmentVariableProvider provider = CreateProvider(
            options,
            crashDumpConfiguration,
            Path.GetTempPath(),
            logger);

        await provider.UpdateAsync(Mock.Of<IEnvironmentVariables>());

        string[] loggedMessages = logger.Invocations
            .Where(static invocation => invocation.Method.Name == nameof(ILogger.LogAsync))
            .Select(static invocation => (string)invocation.Arguments[1])
            .ToArray();
        if (traceEnabled)
        {
            Assert.AreSequenceEqual(
                [
                    $"DbgMiniDumpName: {crashDumpConfiguration.DumpFileNamePattern}",
                    "DbgMiniDumpType: 4",
                    $"{CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName}: {crashDumpConfiguration.SequenceFileName}",
                ],
                loggedMessages);
        }
        else
        {
            Assert.IsEmpty(loggedMessages);
        }
    }

    private static CrashDumpEnvironmentVariableProvider CreateProvider(
        TestCommandLineOptions options,
        CrashDumpConfiguration crashDumpConfiguration,
        string resultsDirectory,
        Mock<ILogger>? logger = null)
    {
        var configuration = new Mock<IConfiguration>();
        configuration
            .Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns(resultsDirectory);
        logger ??= new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.UtcNow).Returns(FixedTime);

        return new CrashDumpEnvironmentVariableProvider(
            configuration.Object,
            options,
            crashDumpConfiguration,
            loggerFactory.Object,
            clock.Object);
    }

    private static EnvironmentVariable AssertVariable(
        IEnumerable<EnvironmentVariable> variables,
        string name,
        string? expectedValue = null)
    {
        EnvironmentVariable variable = variables.Single(variable => variable.Variable == name);
        if (expectedValue is not null)
        {
            Assert.AreEqual(expectedValue, variable.Value);
        }

        Assert.IsFalse(variable.IsSecret);
        Assert.IsTrue(variable.IsLocked);
        return variable;
    }
}

[TestClass]
[ResourceLock(CrashDumpArtifactPublisherMutationTests.ProcessKilledByHangDumpAppDomainDataResource, Mode = ResourceAccessMode.Read)]
public sealed class CrashDumpArtifactPublisherMutationTests
{
    internal const string ProcessKilledByHangDumpAppDomainDataResource = "Microsoft.Testing.Extensions.CrashDump.ProcessKilledByHangDumpAppDomainData";

    [TestMethod]
    public async Task OnTestHostProcessStartedAsync_MissingDumpPattern_ThrowsInvariantViolation()
    {
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration(),
            new RecordingMessageBus(),
            new RecordingOutputDevice());

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.OnTestHostProcessStartedAsync(new ProcessInformation(123), CancellationToken.None));

        Assert.Contains("Unexpected state", exception.Message);
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_MissingDumpPattern_ThrowsInvariantViolation()
    {
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration(),
            new RecordingMessageBus(),
            new RecordingOutputDevice());

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None));

        Assert.Contains("Unexpected state", exception.Message);
    }

    [TestMethod]
    public async Task OnTestHostProcessStartedAsync_PreCanceledToken_StopsSnapshot()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "dump_1.dmp"), "existing");
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = Path.Combine(directory.Path, "dump_%p.dmp") },
            new RecordingMessageBus(),
            new RecordingOutputDevice());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.OnTestHostProcessStartedAsync(new ProcessInformation(123), cancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task BeforeTestHostProcessStartAsync_FirstUnsupportedRequestDisplaysMessage()
    {
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName,
            new CrashDumpConfiguration(),
            new RecordingMessageBus(),
            outputDevice);

        await handler.BeforeTestHostProcessStartAsync(CancellationToken.None);

#if NETCOREAPP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.AreSequenceEqual([CrashDumpResources.CrashReportIfSupportedIgnoredOnWindowsInfoMessage], outputDevice.Messages);
        }
        else
        {
            Assert.IsEmpty(outputDevice.Messages);
        }
#else
        Assert.AreSequenceEqual([CrashDumpResources.CrashReportIfSupportedIgnoredOnNetFrameworkInfoMessage], outputDevice.Messages);
#endif
    }

    [TestMethod]
    public async Task CrashReportOnly_PublishesOnlyExpectedReportAndReportsReportOnly()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%p.dmp");
        string dump = Path.Combine(directory.Path, "dump_123.dmp");
        string expectedReport = dump + ".crashreport.json";
        string unrelatedReport = Path.Combine(directory.Path, "other.dmp.crashreport.json");
        File.WriteAllText(dump, "dump");
        File.WriteAllText(expectedReport, "{}");
        File.WriteAllText(unrelatedReport, "{}");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashReportOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([expectedReport], GetPublishedPaths(messageBus));
        Assert.AreSequenceEqual(
            [string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, 123)],
            outputDevice.Messages);
    }

    [TestMethod]
    public async Task CrashDumpOnly_IgnoresCrashReportFilesWhenComputingTheCrashMessage()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%e_%p.dmp");
        string dump = Path.Combine(directory.Path, "dump_testhost_123.dmp");
        File.WriteAllText(dump, "dump");
        File.WriteAllText(dump + ".crashreport.json", "{}");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([dump], GetPublishedPaths(messageBus));
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, 123),
            outputDevice.Messages[0]);
    }

    [TestMethod]
    public async Task NoCrashArtifacts_ReportsGenericCrashAndMissingExpectedDump()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%p.dmp");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.IsEmpty(messageBus.Published);
        Assert.HasCount(2, outputDevice.Messages);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, 123),
            outputDevice.Messages[0]);
        Assert.Contains(Path.Combine(directory.Path, "dump_123.dmp"), outputDevice.Messages[1]);
    }

    [TestMethod]
    public async Task ChildDumpOnly_IsPublishedAndStillReportsTheMissingTesthostDump()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%p.dmp");
        string childDump = Path.Combine(directory.Path, "dump_456.dmp");
        File.WriteAllText(childDump, "dump");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([childDump], GetPublishedPaths(messageBus));
        Assert.HasCount(2, outputDevice.Messages);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, 123),
            outputDevice.Messages[0]);
        Assert.Contains(Path.Combine(directory.Path, "dump_123.dmp"), outputDevice.Messages[1]);
    }

    [TestMethod]
    public async Task ExtensionlessPattern_PublishesMatchingFilesThatHaveExtensions()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "%p");
        string childDump = Path.Combine(directory.Path, "child.dmp");
        File.WriteAllText(childDump, "dump");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([childDump], GetPublishedPaths(messageBus));
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, 123),
            outputDevice.Messages[0]);
    }

    [TestMethod]
    public async Task MissingDumpDirectory_StopsFallbackEnumerationAfterWarning()
    {
        using var directory = new TemporaryDirectory();
        string missingDirectory = Path.Combine(directory.Path, "missing");
        string pattern = Path.Combine(missingDirectory, "dump_%p.dmp");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.IsEmpty(messageBus.Published);
        Assert.HasCount(2, outputDevice.Messages);
        Assert.Contains(Path.Combine(missingDirectory, "dump_123.dmp"), outputDevice.Messages[1]);
    }

    [TestMethod]
    public async Task FallbackEnumeration_PublishesDmpWhenConfiguredPatternUsesAnotherExtension()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%p.custom");
        string fallbackDump = Path.Combine(directory.Path, "fallback.dmp");
        File.WriteAllText(fallbackDump, "dump");
        var messageBus = new RecordingMessageBus();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            new RecordingOutputDevice());

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([fallbackDump], GetPublishedPaths(messageBus));
    }

    [TestMethod]
    public async Task MultipleTesthostMatchingDumps_DoNotToggleTesthostDetectionOff()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%e_%p.dmp");
        string firstDump = Path.Combine(directory.Path, "dump_first_123.dmp");
        string secondDump = Path.Combine(directory.Path, "dump_second_123.dmp");
        File.WriteAllText(firstDump, "dump");
        File.WriteAllText(secondDump, "dump");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual(
            new[] { firstDump, secondDump }.OrderBy(static path => path, StringComparer.Ordinal).ToArray(),
            GetPublishedPaths(messageBus).OrderBy(static path => path, StringComparer.Ordinal).ToArray());
        Assert.AreSequenceEqual(
            [string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, 123)],
            outputDevice.Messages);
    }

    [TestMethod]
    public async Task RuntimePlaceholderCrashReport_IsMatchedAndDoesNotEmitMissingReportWarning()
    {
        using var directory = new TemporaryDirectory();
        string pattern = Path.Combine(directory.Path, "dump_%e_%p.dmp");
        string report = Path.Combine(directory.Path, "dump_testhost_123.dmp.crashreport.json");
        File.WriteAllText(report, "{}");
        var messageBus = new RecordingMessageBus();
        var outputDevice = new RecordingOutputDevice();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashReportOptionName,
            new CrashDumpConfiguration { DumpFileNamePattern = pattern },
            messageBus,
            outputDevice);

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.AreSequenceEqual([report], GetPublishedPaths(messageBus));
        Assert.AreSequenceEqual(
            [string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, 123)],
            outputDevice.Messages);
    }

    [TestMethod]
    public async Task CrashingProcess_PublishesConfiguredSequenceFile()
    {
        using var directory = new TemporaryDirectory();
        string sequenceFile = Path.Combine(directory.Path, "crash.sequence.log");
        File.WriteAllText(sequenceFile, "# sequence");
        var messageBus = new RecordingMessageBus();
        CrashDumpProcessLifetimeHandler handler = CreateHandler(
            CrashDumpCommandLineOptions.CrashDumpOptionName,
            new CrashDumpConfiguration
            {
                DumpFileNamePattern = Path.Combine(directory.Path, "dump_%p.dmp"),
                SequenceFileName = sequenceFile,
            },
            messageBus,
            new RecordingOutputDevice());

        await handler.OnTestHostProcessExitedAsync(new ProcessInformation(123), CancellationToken.None);

        Assert.Contains(
            artifact => artifact.FileInfo.FullName == sequenceFile
                && artifact.DisplayName == CrashDumpResources.CrashDumpSequenceArtifactDisplayName
                && artifact.Description == CrashDumpResources.CrashDumpSequenceArtifactDescription,
            messageBus.Published.OfType<FileArtifact>());
    }

    private static CrashDumpProcessLifetimeHandler CreateHandler(
        string option,
        CrashDumpConfiguration configuration,
        RecordingMessageBus messageBus,
        RecordingOutputDevice outputDevice)
        => new(
            new TestCommandLineOptions(new()
            {
                [option] = [],
            }),
            messageBus,
            outputDevice,
            configuration);

    private static string[] GetPublishedPaths(RecordingMessageBus messageBus)
        => messageBus.Published
            .OfType<FileArtifact>()
            .Select(static artifact => artifact.FileInfo.FullName)
            .ToArray();

    private sealed class RecordingMessageBus : IMessageBus
    {
        public List<IData> Published { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            Published.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutputDevice : IOutputDevice
    {
        public List<string> Messages { get; } = [];

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            if (data is ErrorMessageOutputDeviceData error)
            {
                Messages.Add(error.Message);
            }
            else if (data is FormattedTextOutputDeviceData formatted)
            {
                Messages.Add(formatted.Text);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ProcessInformation(int pid) : ITestHostProcessInformation
    {
        public int PID { get; } = pid;

        public int ExitCode => 1;

        public bool HasExitedGracefully => false;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateDirectory(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "crashdump-mutation-" + Guid.NewGuid().ToString("N"))).FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort cleanup.
            }
        }
    }
}

[TestClass]
public sealed class CrashDumpRegistrationMutationTests
{
    [TestMethod]
    public void CrashDumpConfiguration_EnableDefaultsToTrue()
        => Assert.IsTrue(new CrashDumpConfiguration().Enable);

    [TestMethod]
    public void CrashDumpCommandLineProvider_MetadataHasStableIdentity()
    {
        var provider = new CrashDumpCommandLineProvider();

        Assert.AreEqual("CrashDumpCommandLineProvider", provider.Uid);
        Assert.AreEqual(CrashDumpResources.CrashDumpDisplayName, provider.DisplayName);
        Assert.AreEqual(CrashDumpResources.CrashDumpDescription, provider.Description);
    }

    [TestMethod]
    public void CrashDumpCommandLineProvider_OptionsAreVisible()
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption[] options = provider.GetCommandLineOptions().ToArray();

        Assert.HasCount(6, options);
        Assert.DoesNotContain(static option => option.IsHidden, options);
    }

    [TestMethod]
    public void BuildDumpFileNameRegexPattern_CollapsesAdjacentRuntimePlaceholders()
        => Assert.AreEqual("^.*\\.dmp$", CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegexPattern("%p%e%h%t.dmp"));

    [TestMethod]
    public async Task AddCrashDumpProvider_RegistersEveryControllerAndTesthostRole()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        IList environmentVariableFactories = GetPrivateList(builder.TestHostControllers, "_environmentVariableProviderFactories");
        IList lifetimeHandlerFactories = GetPrivateList(builder.TestHostControllers, "_lifetimeHandlerFactories");
        IList commandLineFactories = GetPrivateList(builder.CommandLine, "_commandLineProviderFactory");
        IList dataConsumerFactories = GetPrivateList(builder.TestHost, "_dataConsumersCompositeServiceFactories");
        IList sessionHandlerFactories = GetPrivateList(builder.TestHost, "_testSessionLifetimeHandlerCompositeFactories");
        int environmentVariableCount = environmentVariableFactories.Count;
        int lifetimeHandlerCount = lifetimeHandlerFactories.Count;
        int commandLineCount = commandLineFactories.Count;
        int dataConsumerCount = dataConsumerFactories.Count;
        int sessionHandlerCount = sessionHandlerFactories.Count;

        builder.AddCrashDumpProvider();

        Assert.HasCount(environmentVariableCount + 1, environmentVariableFactories);
        Assert.HasCount(lifetimeHandlerCount + 1, lifetimeHandlerFactories);
        Assert.HasCount(commandLineCount + 1, commandLineFactories);
        Assert.HasCount(dataConsumerCount + 1, dataConsumerFactories);
        Assert.HasCount(sessionHandlerCount + 1, sessionHandlerFactories);
        Assert.AreSame(dataConsumerFactories[dataConsumerCount], sessionHandlerFactories[sessionHandlerCount]);

        var commandLineFactory = (Delegate)commandLineFactories[commandLineCount]!;
        object? provider = commandLineFactory.DynamicInvoke(new ServiceProvider());
        Assert.IsInstanceOfType<CrashDumpCommandLineProvider>(provider);
    }

    private static IList GetPrivateList(object owner, string fieldName)
        => (IList)(owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!);
}

[TestClass]
public sealed class CrashDumpSharedHelperMutationTests
{
    private const BindingFlags StaticMethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly CrashDumpAssembly = typeof(CrashDumpEnvironmentVariableProvider).Assembly;
    private static readonly Type ArtifactNamingHelperType = GetEmbeddedType("Microsoft.Testing.Platform.Services.ArtifactNamingHelper");
    private static readonly Type TargetFrameworkParserType = GetEmbeddedType("Microsoft.Testing.Platform.OutputDevice.TargetFrameworkParser");

    [TestMethod]
    public void GetStandardReplacements_ReturnsExactRuntimeAndTimestampValues()
    {
        DateTimeOffset timestamp = new DateTimeOffset(2026, 6, 11, 7, 8, 9, 123, TimeSpan.Zero).AddTicks(4567);

        Dictionary<string, string> replacements = Invoke<Dictionary<string, string>>(
            ArtifactNamingHelperType,
            "GetStandardReplacements",
            "process",
            "42",
            timestamp);

        Assert.AreEqual("process", replacements["pname"]);
        Assert.AreEqual("42", replacements["pid"]);
        Assert.AreEqual("2026-06-11_07-08-09.1234567", replacements["time"]);
        Assert.AreEqual(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), replacements["arch"]);
        Assert.AreEqual(Assembly.GetEntryAssembly()!.GetName().Name, replacements["asm"]);
        Assert.AreEqual(GetShortTargetFrameworkIncludingPlatform(Assembly.GetEntryAssembly()), replacements["tfm"]);
        Assert.AreNotEqual("unknown", replacements["asm"]);
        Assert.AreNotEqual("unknown", replacements["tfm"]);
    }

    [TestMethod]
    public void ResolveTemplate_NullReplacements_ReturnsTemplate()
        => Assert.AreEqual(
            "{pname}.dmp",
            Invoke<string>(ArtifactNamingHelperType, "ResolveTemplate", "{pname}.dmp", null));

    [TestMethod]
    public void ResolveTemplate_CaseInsensitiveDictionaryStillUsesOrdinalLookup()
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PNAME"] = "process",
        };

        Assert.AreEqual(
            "{pname}",
            Invoke<string>(ArtifactNamingHelperType, "ResolveTemplate", "{pname}", replacements));
    }

    [TestMethod]
    public void GetShortTargetFramework_NullReturnsNull()
        => Assert.IsNull(GetShortTargetFramework(null));

    [TestMethod]
    [DataRow(".NET Framework", ".NET Framework")]
    [DataRow(".NET Framework 4.6.2", "net462")]
    [DataRow(".NET Framework 4.7.1", "net471")]
    [DataRow(".NET Framework 4.7.2", "net472")]
    [DataRow(".NET Framework 4.8.1", "net481")]
    [DataRow(".NET Framework 5.6.2", "net56")]
    [DataRow(".NET Framework 5.7.1", "net57")]
    [DataRow(".NET Framework 5.7.2", "net57")]
    [DataRow(".NET Framework 5.8.2", "net58")]
    [DataRow(".NET Core", ".NET Core")]
    [DataRow(".NET Core 3.1", "netcoreapp3.1")]
    [DataRow(".NET Core 3.1.0", "netcoreapp3.1")]
    [DataRow(".NET 10.0.0", "net10.0")]
    [DataRow(".NET 12.3.0", "net12.3")]
    [DataRow(".NET", ".NET")]
    [DataRow(".NET ", ".NET ")]
    [DataRow(".NET .", ".NET .")]
    [DataRow(".NET .0", ".NET .0")]
    [DataRow(".NET 8.", ".NET 8.")]
    [DataRow("custom-runtime", "custom-runtime")]
    [DataRow(".net Framework 4.7.2", ".net Framework 4.7.2")]
    [DataRow(".net Core 3.1.0", ".net Core 3.1.0")]
    [DataRow(".net 8.0.0", ".net 8.0.0")]
    public void GetShortTargetFramework_ParsesOnlyTheExactKnownPrefixes(string frameworkDescription, string expected)
        => Assert.AreEqual(expected, GetShortTargetFramework(frameworkDescription));

    [TestMethod]
    public void BuildTargetFrameworkMoniker_RequiresBothFrameworkAndPlatform()
    {
        Assert.IsNull(BuildTargetFrameworkMoniker(null, "Windows10.0"));
        Assert.AreEqual(string.Empty, BuildTargetFrameworkMoniker(string.Empty, "Windows10.0"));
        Assert.AreEqual("net8.0", BuildTargetFrameworkMoniker("net8.0", null));
        Assert.AreEqual("net8.0-windows10.0", BuildTargetFrameworkMoniker("net8.0", "Windows10.0"));
    }

    [TestMethod]
    public void GetTargetPlatformName_NullAssemblyReturnsNull()
        => Assert.IsNull(GetTargetPlatformName(null));

#if NET
    [TestMethod]
    public void GetTargetPlatformName_RejectsOtherSingleArgumentAttributes()
    {
        Assembly assembly = CreateAssemblyWithFrameworkAttribute();

        Assert.IsNull(GetTargetPlatformName(assembly));
    }

    [TestMethod]
    public void GetTargetPlatformName_ReturnsExactTargetPlatformAttributeValue()
    {
        Assembly assembly = CreateAssemblyWithTargetPlatform("Windows10.0.22621.0");

        Assert.AreEqual("Windows10.0.22621.0", GetTargetPlatformName(assembly));
    }

    [TestMethod]
    public void GetShortTargetFrameworkIncludingPlatform_EmptyDisplayNameFallsBackToRuntimeFramework()
    {
        Assembly assembly = CreateAssemblyWithEmptyFrameworkDisplayNameAndTargetPlatform("BrowserWasm1.0");

        string? result = GetShortTargetFrameworkIncludingPlatform(assembly);

        Assert.IsNotNull(result);
        Assert.StartsWith("net", result);
        Assert.EndsWith("-browserwasm1.0", result);
    }

    private static Assembly CreateAssemblyWithFrameworkAttribute()
    {
        AssemblyBuilder builder = CreateAssemblyBuilder();
        ConstructorInfo constructor = typeof(TargetFrameworkAttribute).GetConstructor([typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [".NETCoreApp,Version=v8.0"]));
        return builder;
    }

    private static Assembly CreateAssemblyWithTargetPlatform(string platform)
    {
        AssemblyBuilder builder = CreateAssemblyBuilder();
        ConstructorInfo constructor = typeof(System.Runtime.Versioning.TargetPlatformAttribute).GetConstructor([typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [platform]));
        return builder;
    }

    private static Assembly CreateAssemblyWithEmptyFrameworkDisplayNameAndTargetPlatform(string platform)
    {
        AssemblyBuilder builder = CreateAssemblyBuilder();
        ConstructorInfo frameworkConstructor = typeof(TargetFrameworkAttribute).GetConstructor([typeof(string)])!;
        PropertyInfo displayNameProperty = typeof(TargetFrameworkAttribute).GetProperty(nameof(TargetFrameworkAttribute.FrameworkDisplayName))!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(
            frameworkConstructor,
            [".NETCoreApp,Version=v8.0"],
            [displayNameProperty],
            [string.Empty]));

        ConstructorInfo platformConstructor = typeof(System.Runtime.Versioning.TargetPlatformAttribute).GetConstructor([typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(platformConstructor, [platform]));
        return builder;
    }

    private static AssemblyBuilder CreateAssemblyBuilder()
        => AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CrashDumpMutationTests_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
#endif

    private static string? GetShortTargetFramework(string? frameworkDescription)
        => Invoke<string?>(TargetFrameworkParserType, "GetShortTargetFramework", frameworkDescription);

    private static string? GetShortTargetFrameworkIncludingPlatform(Assembly? assembly)
        => Invoke<string?>(TargetFrameworkParserType, "GetShortTargetFrameworkIncludingPlatform", assembly);

    private static string? BuildTargetFrameworkMoniker(string? framework, string? platform)
        => Invoke<string?>(TargetFrameworkParserType, "BuildTargetFrameworkMoniker", framework, platform);

    private static string? GetTargetPlatformName(Assembly? assembly)
        => Invoke<string?>(TargetFrameworkParserType, "GetTargetPlatformName", assembly);

    private static T Invoke<T>(Type type, string methodName, params object?[] arguments)
        => (T)type.GetMethod(methodName, StaticMethodBindingFlags)!.Invoke(null, arguments)!;

    private static Type GetEmbeddedType(string fullName)
        => CrashDumpAssembly.GetType(fullName, throwOnError: true)!;
}
