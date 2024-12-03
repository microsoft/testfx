// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.OutputDevice;

// Any interfaces that can have special treatment for any output device should be implemented by
// this proxy class and be forwarded properly.
// This is not so good. How can we make sure we are not missing any interfaces that may be implemented by external output devices?
internal sealed class ProxyPlatformOutputDevice : IHotReloadPlatformOutputDevice, IDisposable
{
    private readonly ServerModePerCallOutputDevice? _serverModeOutputDevice;

    public ProxyPlatformOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice)
    {
        OriginalOutputDevice = originalOutputDevice;
        _serverModeOutputDevice = serverModeOutputDevice;
    }

    internal IPlatformOutputDevice OriginalOutputDevice { get; }

    public string Uid => nameof(ProxyPlatformOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ProxyPlatformOutputDevice);

    public string Description => nameof(ProxyPlatformOutputDevice);

    public async Task DisplayBeforeHotReloadSessionStartAsync()
    {
        if (OriginalOutputDevice is IHotReloadPlatformOutputDevice hotReloadOutputDevice)
        {
            await hotReloadOutputDevice.DisplayBeforeHotReloadSessionStartAsync();
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync()
    {
        if (OriginalOutputDevice is IHotReloadPlatformOutputDevice hotReloadOutputDevice)
        {
            await hotReloadOutputDevice.DisplayAfterHotReloadSessionEndAsync();
        }
    }

    public async Task DisplayAfterSessionEndRunAsync()
    {
        await OriginalOutputDevice.DisplayAfterSessionEndRunAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAfterSessionEndRunAsync();
        }
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        await OriginalOutputDevice.DisplayAsync(producer, data);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data);
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage)
    {
        await OriginalOutputDevice.DisplayBannerAsync(bannerMessage);
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage);
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        await OriginalOutputDevice.DisplayBeforeSessionStartAsync();
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync();
        }
    }

    public async Task<bool> IsEnabledAsync()
        => (_serverModeOutputDevice is not null && await _serverModeOutputDevice.IsEnabledAsync()) || await OriginalOutputDevice.IsEnabledAsync();

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost);
        }
    }

    public void Dispose()
        => (OriginalOutputDevice as IDisposable)?.Dispose();
}
