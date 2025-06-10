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
        await OriginalOutputDevice.DisplayAsync(producer, data).ConfigureAwait(false);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBannerAsync(string? bannerMessage)
    {
        await OriginalOutputDevice.DisplayBannerAsync(bannerMessage).ConfigureAwait(false);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBeforeSessionStartAsync()
    {
        await OriginalOutputDevice.DisplayBeforeSessionStartAsync().ConfigureAwait(false);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync().ConfigureAwait(false);
        }
    }

    internal async Task DisplayAfterSessionEndRunAsync()
    {
        await OriginalOutputDevice.DisplayAfterSessionEndRunAsync().ConfigureAwait(false);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAfterSessionEndRunAsync().ConfigureAwait(false);
        }
    }

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost).ConfigureAwait(false);
        }
    }

    internal async Task HandleProcessRoleAsync(TestProcessRole processRole)
    {
        await OriginalOutputDevice.HandleProcessRoleAsync(processRole).ConfigureAwait(false);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.HandleProcessRoleAsync(processRole).ConfigureAwait(false);
        }
    }
}
