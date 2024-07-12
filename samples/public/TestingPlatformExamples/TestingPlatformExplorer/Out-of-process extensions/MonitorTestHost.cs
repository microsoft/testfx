// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.OutputDevice;

namespace TestingPlatformExplorer.OutOfProcess;

internal class MonitorTestHost : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public MonitorTestHost(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public string Uid => nameof(MonitorTestHost);

    public string Version => "1.0.0";

    public string DisplayName => nameof(MonitorTestHost);

    public string Description => "Example of monitoring the test host process.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("BeforeTestHostProcessStartAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
        });

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"OnTestHostProcessExitedAsync, test host exited with exit code {testHostProcessInformation.ExitCode}")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
        });

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"OnTestHostProcessStartedAsync, test host started with PID {testHostProcessInformation.PID}")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
        });
}
