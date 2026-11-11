// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding TRX report generation to a test application.
/// </summary>
public static class TrxReportExtensions
{
    /// <summary>
    /// Adds TRX report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddTrxReportProvider(this ITestApplicationBuilder builder)
    {
        if (builder is not IArtifactPostProcessingApplicationBuilder artifactPostProcessingBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidTestApplicationBuilderType);
        }

        var commandLine = new TrxReportGeneratorCommandLine();

        var compositeTestSessionTrxService =
            new CompositeExtensionFactory<TrxReportGenerator>(serviceProvider =>
                new TrxReportGenerator(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestFramework(),
                serviceProvider.GetTestFrameworkCapabilities(),
                serviceProvider.GetTestApplicationProcessExitCode(),
                serviceProvider.GetTask(),
                serviceProvider.GetService<TrxTestApplicationLifecycleCallbacks>(),
                serviceProvider.GetLoggerFactory().CreateLogger<TrxReportGenerator>()));

        if (TrxModeHelpers.IsTestHostControllerSupported)
        {
            ControllerBackedRegistrations(builder);
        }

        builder.TestHost.AddDataConsumer(compositeTestSessionTrxService);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeTestSessionTrxService);

        builder.CommandLine.AddProvider(() => commandLine);

        ToolTrxCompareFactory toolTrxCompareFactory = new();
        TrxCompareToolCommandLine createTrxCompareToolCommandLine = toolTrxCompareFactory.CreateTrxCompareToolCommandLine();
        builder.CommandLine.AddProvider(() => createTrxCompareToolCommandLine);

        artifactPostProcessingBuilder.Tools.AddTool(serviceProvider => toolTrxCompareFactory.CreateTrxCompareTool(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetOutputDevice(),
            serviceProvider.GetRequiredService<ITask>()));

        artifactPostProcessingBuilder.ArtifactPostProcessing.AddArtifactPostProcessor(_ => new TrxArtifactPostProcessor());

        ToolTrxMergeFactory toolTrxMergeFactory = new();
        builder.CommandLine.AddProvider(toolTrxMergeFactory.CreateCommandLine);
        artifactPostProcessingBuilder.Tools.AddTool(serviceProvider => toolTrxMergeFactory.CreateTool(serviceProvider.GetCommandLineOptions()));
    }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private static void ControllerBackedRegistrations(ITestApplicationBuilder builder)
    {
        builder.TestHost.AddTestHostApplicationLifetime(serviceProvider =>
            new TrxTestApplicationLifecycleCallbacks(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment()));

        NamedPipeServerEndpoint endpoint = NamedPipeServerFactory.CreateEndpoint();
        var compositeLifeTimeHandler =
            new CompositeExtensionFactory<TrxProcessLifetimeHandler>(serviceProvider =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetLoggerFactory();
                loggerFactory.CreateLogger<TrxProcessLifetimeHandler>().LogTrace($"TRX pipe name: '{endpoint.PipeName}'");
                return new TrxProcessLifetimeHandler(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetEnvironment(),
                    loggerFactory,
                    serviceProvider.GetMessageBus(),
                    serviceProvider.GetRequiredService<IFileSystem>(),
                    serviceProvider.GetTestApplicationModuleInfo(),
                    serviceProvider.GetConfiguration(),
                    serviceProvider.GetSystemClock(),
                    serviceProvider.GetTask(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider,
                    endpoint);
            });
        ((TestHostControllersManager)builder.TestHostControllers).AddDataConsumer(compositeLifeTimeHandler);
        builder.TestHostControllers.AddProcessLifetimeHandler(compositeLifeTimeHandler);
        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider =>
        {
            serviceProvider.GetLoggerFactory().CreateLogger<TrxEnvironmentVariableProvider>().LogTrace($"TRX pipe name: '{endpoint.PipeName}'");
            return new TrxEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), endpoint);
        });
        ((TestHostControllersManager)builder.TestHostControllers).AddEnvironmentVariableProviderWhenControllerStarts(serviceProvider =>
            new TrxModeEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), serviceProvider.GetEnvironment()));

        var inProcessArtifacts = new CompositeExtensionFactory<TrxInProcessControllerArtifactHandler>(serviceProvider =>
            new TrxInProcessControllerArtifactHandler(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetConfiguration(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetOutputDevice()));
        ((TestHostControllersManager)builder.TestHostControllers).AddDataConsumerWhenControllerStarts(inProcessArtifacts);
        ((TestHostControllersManager)builder.TestHostControllers).AddRunCompletionHandler(serviceProvider =>
            (TrxInProcessControllerArtifactHandler)((ICompositeExtensionFactory)inProcessArtifacts).GetInstance(serviceProvider));
    }

    private sealed class TrxInProcessControllerArtifactHandler(
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        IEnvironment environment,
        IConfiguration configuration,
        IClock clock,
        IOutputDevice outputDevice)
        : IDataConsumer, ITestHostControllerRunCompletionHandler, IOutputDeviceDataProducer
    {
        private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifacts = [];

        public string Uid => nameof(TrxInProcessControllerArtifactHandler);

        public string Version => ExtensionVersion.DefaultSemVer;

        public string DisplayName => ExtensionResources.TrxReportGeneratorDisplayName;

        public string Description => ExtensionResources.TrxReportGeneratorDescription;

        public Type[] DataTypesConsumed => [typeof(FileArtifact)];

        public Task<bool> IsEnabledAsync()
            => Task.FromResult(commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)
                && !TrxModeHelpers.ShouldUseControllerBackedTrxGeneration(commandLineOptions));

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (!_artifacts.TryGetValue(dataProducer, out List<SessionFileArtifact>? artifacts))
            {
                artifacts = [];
                _artifacts.Add(dataProducer, artifacts);
            }

            var artifact = (FileArtifact)value;
            artifacts.Add(new SessionFileArtifact(
                new SessionUid(Guid.Empty.ToString()), artifact.FileInfo, artifact.DisplayName, artifact.Description, artifact.Kind));
            return Task.CompletedTask;
        }

        public async Task OnRunCompletedAsync(int exitCode, IReadOnlyList<SessionFileArtifact> artifacts, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_artifacts.Count == 0)
            {
                return;
            }

            foreach (SessionFileArtifact artifact in artifacts.Where(artifact => artifact.Kind == TrxReportEngine.TrxArtifactKind))
            {
                IReadOnlyList<string> warnings = await TrxReportEngine.AddArtifactsAsync(
                    artifact.FileInfo, _artifacts, fileSystem, environment, configuration, clock, cancellationToken).ConfigureAwait(false);
                foreach (string warning in warnings)
                {
                    await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private sealed class TrxModeEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, IEnvironment environment)
        : ITestHostEnvironmentVariableProvider
    {
        public string Uid => nameof(TrxModeEnvironmentVariableProvider);

        public string Version => ExtensionVersion.DefaultSemVer;

        public string DisplayName => ExtensionResources.TrxReportGeneratorDisplayName;

        public string Description => ExtensionResources.TrxReportGeneratorDescription;

        private string VariableName => TrxModeHelpers.GetControllerModeEnvironmentVariableName(environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        private string Mode => TrxModeHelpers.ShouldUseControllerBackedTrxGeneration(commandLineOptions)
            ? TrxReportGeneratorCommandLine.OutOfProcessMode
            : TrxReportGeneratorCommandLine.InProcessMode;

        public Task<bool> IsEnabledAsync() => Task.FromResult(commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName));

        public Task UpdateAsync(IEnvironmentVariables environmentVariables)
        {
            // Scope the choice to this controller so nested launchers cannot reuse an ancestor's mode.
            environmentVariables.SetVariable(new EnvironmentVariable(VariableName, Mode, isSecret: false, isLocked: true));
            return Task.CompletedTask;
        }

        public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
            => environmentVariables.TryGetVariable(VariableName, out OwnedEnvironmentVariable? variable)
                && variable.Value == Mode
                    ? ValidationResult.ValidTask
                    : ValidationResult.InvalidTask(ExtensionResources.TrxModeInvalidValue);
    }
}
