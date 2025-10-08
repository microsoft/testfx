// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace TestingPlatformExplorer.InProcess;

internal sealed class DisplayTestSessionLifeTimeHandler : ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer,
    IAsyncInitializableExtension,
    IAsyncCleanableExtension,
    IAsyncDisposable
{
    private readonly IOutputDevice _outputDevice;

    public string Uid => "This extension display in console the session start/end";

    public string Version => "1.0.0";

    public string DisplayName => nameof(DisplayTestSessionLifeTimeHandler);

    public string Description => "This extension display in console the session start/end";

    public DisplayTestSessionLifeTimeHandler(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from OnTestSessionStartingAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, testSessionContext.CancellationToken);

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from OnTestSessionFinishingAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, testSessionContext.CancellationToken);

    public async Task InitializeAsync()
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from InitializeAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, CancellationToken.None);

    public async Task CleanupAsync()
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from CleanupAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, CancellationToken.None);

    public async ValueTask DisposeAsync()
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("Hello from DisposeAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, CancellationToken.None);
}
