// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class ProxyPlatformOutputDevice : IPlatformOutputDevice
{
    private readonly IPlatformOutputDevice _originalOutputDevice;

    public ProxyPlatformOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice)
    {
        _originalOutputDevice = originalOutputDevice;
        ServerModeOutputDevice = serverModeOutputDevice;
    }

    internal ServerModePerCallOutputDevice? ServerModeOutputDevice { get; }

    public string Uid => nameof(ProxyPlatformOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ProxyPlatformOutputDevice);

    public string Description => nameof(ProxyPlatformOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync()
    {
        await _originalOutputDevice.DisplayAfterSessionEndRunAsync();
        if (ServerModeOutputDevice is not null)
        {
            await ServerModeOutputDevice.DisplayAfterSessionEndRunAsync();
        }
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        await _originalOutputDevice.DisplayAsync(producer, data);
        if (ServerModeOutputDevice is not null)
        {
            await ServerModeOutputDevice.DisplayAsync(producer, data);
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage)
    {
        await _originalOutputDevice.DisplayBannerAsync(bannerMessage);
        if (ServerModeOutputDevice is not null)
        {
            await ServerModeOutputDevice.DisplayBannerAsync(bannerMessage);
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        await _originalOutputDevice.DisplayBeforeSessionStartAsync();
        if (ServerModeOutputDevice is not null)
        {
            await ServerModeOutputDevice.DisplayBeforeSessionStartAsync();
        }
    }

    public async Task<bool> IsEnabledAsync()
        => (ServerModeOutputDevice is not null && await ServerModeOutputDevice.IsEnabledAsync()) || await _originalOutputDevice.IsEnabledAsync();
}
