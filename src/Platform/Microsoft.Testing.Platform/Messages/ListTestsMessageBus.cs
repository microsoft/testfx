// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class ListTestsMessageBus(
    ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
    ILoggerFactory loggerFactory,
    IConsole console,
    IAsyncMonitorFactory asyncMonitorFactory,
    IEnvironment environment,
    ITestApplicationProcessExitCode testApplicationProcessExitCode,
    IPushOnlyProtocol? pushOnlyProtocol,
    IPushOnlyProtocolConsumer? pushOnlyProtocolConsumer,
    IClock clock,
    IRuntimeFeature runtimeFeature,
    ITestApplicationModuleInfo testApplicationModuleInfo,
    bool noAnsi) : BaseMessageBus, IMessageBus, IDisposable, IOutputDeviceDataProducer
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    private readonly IConsole _console = console;
    private readonly IEnvironment _environment = environment;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode = testApplicationProcessExitCode;
    private readonly IPushOnlyProtocol? _pushOnlyProtocol = pushOnlyProtocol;
    private readonly IPushOnlyProtocolConsumer? _pushOnlyProtocolConsumer = pushOnlyProtocolConsumer;
    private readonly IClock _clock = clock;
    private readonly bool _noAnsi = noAnsi;
    private readonly ILogger<ListTestsMessageBus> _logger = loggerFactory.CreateLogger<ListTestsMessageBus>();
    private readonly IAsyncMonitor _asyncMonitor = asyncMonitorFactory.Create();
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
    private TerminalTestReporter? _terminalTestReporter;
    private bool _writeSummary = true;
    private string? _shortArchitecture;
    private string? _runtimeFramework;
    private string? _targetFramework;
    private string? _assemblyName;

    public override IDataConsumer[] DataConsumerServices => [];

    public string Uid => nameof(ListTestsMessageBus);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public override Task DisableAsync() => Task.CompletedTask;

    public Guid Id { get; } = Guid.NewGuid();

    public override void Dispose()
    {
        if (_writeSummary)
        {
            _terminalTestReporter?.TestExecutionCompleted(_clock.UtcNow);
            _writeSummary = false;
        }
    }

    public override Task InitAsync()
    {
        // This is single exe run, don't show all the details of assemblies and their summaries.
        _terminalTestReporter = new TerminalTestReporter(_console, new()
        {
            BaseDirectory = null,
            ShowAssembly = false,
            ShowAssemblyStartAndComplete = false,
            ShowPassedTests = () => false,
            MinimumExpectedTests = 0,
            UseAnsi = !_noAnsi,
            ShowActiveTests = true,
            ShowProgress = () => false,
        });

        _terminalTestReporter.TestExecutionStarted(_clock.UtcNow, workerCount: 1, isDiscovery: true);

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#if !NETCOREAPP
            string longArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            _shortArchitecture = ArchitectureParser.GetShortArchitecture(longArchitecture);
#else
            // RID has the operating system, we want to see that in the banner, but not next to every dll.
            string longArchitecture = RuntimeInformation.RuntimeIdentifier;
            _shortArchitecture = ArchitectureParser.GetShortArchitecture(RuntimeInformation.RuntimeIdentifier);
#endif
            _runtimeFramework = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
            _targetFramework = TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName) ?? _runtimeFramework;
        }

        _assemblyName = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

        _terminalTestReporter.AssemblyRunStarted(_assemblyName, _targetFramework, _shortArchitecture, null);

        return Task.CompletedTask;
    }

    public override Task DrainDataAsync() => Task.CompletedTask;

    public override async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (data is not TestNodeUpdateMessage testNodeUpdatedMessage
            || testNodeUpdatedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is not DiscoveredTestNodeStateProperty)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                await _logger.LogTraceAsync($"Unexpected data received from producer {dataProducer.DisplayName}{_environment.NewLine}{data}");
            }

            return;
        }

        if (_pushOnlyProtocol?.IsServerMode == true)
        {
            RoslynDebug.Assert(_pushOnlyProtocolConsumer is not null);

            await _pushOnlyProtocolConsumer.ConsumeAsync(dataProducer, data, _testApplicationCancellationTokenSource.CancellationToken);
        }

        // Send the information to the ITestApplicationProcessExitCode to correctly handle the ZeroTest case.
        await _testApplicationProcessExitCode.ConsumeAsync(dataProducer, data, _testApplicationCancellationTokenSource.CancellationToken);

        using (await _asyncMonitor.LockAsync(_testApplicationCancellationTokenSource.CancellationToken))
        {
            if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            ApplicationStateGuard.Ensure(_terminalTestReporter != null);
            _terminalTestReporter.TestDiscovered(_assemblyName!, _targetFramework, _shortArchitecture, null, testNodeUpdatedMessage.DisplayName, testNodeUpdatedMessage.TestNode.Uid);
        }
    }
}
