// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;

namespace TestingPlatformExplorer.InProcess;
internal sealed class DisplayTestApplicationLifecycleCallbacks : ITestApplicationLifecycleCallbacks, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public string Uid => nameof(DisplayTestApplicationLifecycleCallbacks);

    public string Version => "1.0.0";

    public string DisplayName => nameof(DisplayTestApplicationLifecycleCallbacks);

    public string Description => "This extension display in console the before/after run";

    public DisplayTestApplicationLifecycleCallbacks(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public async Task AfterRunAsync(int exitCode, CancellationToken cancellation)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"Hello from AfterRunAsync, exit code: {exitCode}")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        });

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from BeforeRunAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        });

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
