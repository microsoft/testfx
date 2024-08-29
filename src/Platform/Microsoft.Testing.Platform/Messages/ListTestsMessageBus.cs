// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class ListTestsMessageBus(
    ITestFramework testFramework,
    ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
    ILoggerFactory loggerFactory,
    IOutputDevice outputDisplay,
    IAsyncMonitorFactory asyncMonitorFactory,
    IEnvironment environment,
    ITestApplicationProcessExitCode testApplicationProcessExitCode,
    DotnetTestConnection? dotnetTestConnection,
    DotnetTestDataConsumer? dotnetTestDataConsumer) : BaseMessageBus, IMessageBus, IDisposable, IOutputDeviceDataProducer
{
    private readonly ITestFramework _testFramework = testFramework;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    private readonly IOutputDevice _outputDisplay = outputDisplay;
    private readonly IEnvironment _environment = environment;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode = testApplicationProcessExitCode;
    private readonly DotnetTestConnection? _dotnetTestConnection = dotnetTestConnection;
    private readonly DotnetTestDataConsumer? _dotnetTestDataConsumer = dotnetTestDataConsumer;
    private readonly ILogger<ListTestsMessageBus> _logger = loggerFactory.CreateLogger<ListTestsMessageBus>();
    private readonly IAsyncMonitor _asyncMonitor = asyncMonitorFactory.Create();
    private bool _printTitle = true;

    public override IDataConsumer[] DataConsumerServices => [];

    public string Uid => nameof(ListTestsMessageBus);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public override Task DisableAsync() => Task.CompletedTask;

    public override void Dispose()
    {
    }

    public override Task DrainDataAsync() => Task.CompletedTask;

    public override Task InitAsync() => Task.CompletedTask;

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

        if (_dotnetTestConnection?.IsConnected == true)
        {
            RoslynDebug.Assert(_dotnetTestDataConsumer is not null);

            await _dotnetTestDataConsumer.ConsumeAsync(dataProducer, data, _testApplicationCancellationTokenSource.CancellationToken);
        }

        // Send the information to the ITestApplicationProcessExitCode to correctly handle the ZeroTest case.
        await _testApplicationProcessExitCode.ConsumeAsync(dataProducer, data, _testApplicationCancellationTokenSource.CancellationToken);

        using (await _asyncMonitor.LockAsync(_testApplicationCancellationTokenSource.CancellationToken))
        {
            if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (_printTitle)
            {
                await _outputDisplay.DisplayAsync(this, new TextOutputDeviceData("The following Tests are available:"));
                _printTitle = false;
            }

            await _outputDisplay.DisplayAsync(this, new TextOutputDeviceData(testNodeUpdatedMessage.TestNode.DisplayName));
        }
    }
}
