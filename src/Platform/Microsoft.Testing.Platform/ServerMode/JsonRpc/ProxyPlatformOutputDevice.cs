// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class ProxyPlatformOutputDevice : IPlatformOutputDevice
{
    private readonly IPlatformOutputDevice _originalOutputDevice;
    private readonly ServerModePerCallOutputDevice? _serverModeOutputDevice;

    public ProxyPlatformOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice)
    {
        _originalOutputDevice = originalOutputDevice;
        _serverModeOutputDevice = serverModeOutputDevice;
    }

    public string Uid => nameof(ProxyPlatformOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ProxyPlatformOutputDevice);

    public string Description => nameof(ProxyPlatformOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync()
    {
        await _originalOutputDevice.DisplayAfterSessionEndRunAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAfterSessionEndRunAsync();
        }
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        await _originalOutputDevice.DisplayAsync(producer, data);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data);
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage)
    {
        await _originalOutputDevice.DisplayBannerAsync(bannerMessage);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage);
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        await _originalOutputDevice.DisplayBeforeSessionStartAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync();
        }
    }

    public async Task<bool> IsEnabledAsync()
        => (_serverModeOutputDevice is not null && await _serverModeOutputDevice.IsEnabledAsync()) || await _originalOutputDevice.IsEnabledAsync();

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost);
        }
    }
}
