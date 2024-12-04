// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class ProxyOutputDevice : IOutputDevice
{
    private readonly ServerModePerCallOutputDevice? _serverModeOutputDevice;

    public ProxyOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice)
    {
        OriginalOutputDevice = originalOutputDevice;
        _serverModeOutputDevice = serverModeOutputDevice;
    }

    internal IPlatformOutputDevice OriginalOutputDevice { get; }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        await OriginalOutputDevice.DisplayAsync(producer, data);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data);
        }
    }

    internal async Task DisplayBannerAsync(string? bannerMessage)
    {
        await OriginalOutputDevice.DisplayBannerAsync(bannerMessage);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage);
        }
    }

    internal async Task DisplayBeforeSessionStartAsync()
    {
        await OriginalOutputDevice.DisplayBeforeSessionStartAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync();
        }
    }

    internal async Task DisplayAfterSessionEndRunAsync()
    {
        await OriginalOutputDevice.DisplayAfterSessionEndRunAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAfterSessionEndRunAsync();
        }
    }

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        // Server mode output device is basically used to send messages to Test Explorer.
        // For that, it needs the ServerTestHost.
        // However, the ServerTestHost is available later than the time we create the output device.
        // So, the server mode output device is initially created early without the ServerTestHost, and
        // it keeps any messages in a list.
        // Later when ServerTestHost is created and is available, we initialize the server mode output device.
        // The initialization will setup the right state for pushing to Test Explorer, and will push any existing
        // messages to Test Explorer as well.
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost);
        }
    }
}
