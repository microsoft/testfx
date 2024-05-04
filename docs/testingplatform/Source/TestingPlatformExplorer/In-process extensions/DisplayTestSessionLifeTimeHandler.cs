// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

namespace TestingPlatformExplorer.In_process_extensions;
internal class DisplayTestSessionLifeTimeHandler : ITestSessionLifetimeHandler, IOutputDeviceDataProducer
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

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
        => _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("OnTestSessionStartingAsync()")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        });

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
        => _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("OnTestSessionFinishingAsync()")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        });
}
