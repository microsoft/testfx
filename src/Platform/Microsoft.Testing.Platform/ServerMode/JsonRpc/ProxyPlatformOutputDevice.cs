// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.ServerMode;

// Any interfaces that can have special treatment for any output device should be implemented by
// this proxy class and be forwarded properly.
// This is not so good. How can we make sure we are not missing any interfaces that may be implemented by external output devices?
internal sealed class ProxyPlatformOutputDevice : IHotReloadPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IDisposable,
    IAsyncInitializableExtension
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

    public Type[] DataTypesConsumed => (_originalOutputDevice as IDataConsumer)?.DataTypesConsumed ?? Array.Empty<Type>();

    public async Task DisplayBeforeHotReloadSessionStartAsync()
    {
        if (_originalOutputDevice is IHotReloadPlatformOutputDevice hotReloadOutputDevice)
        {
            await hotReloadOutputDevice.DisplayBeforeHotReloadSessionStartAsync();
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync()
    {
        if (_originalOutputDevice is IHotReloadPlatformOutputDevice hotReloadOutputDevice)
        {
            await hotReloadOutputDevice.DisplayAfterHotReloadSessionEndAsync();
        }
    }

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

    public async Task InitializeAsync()
        => await _originalOutputDevice.TryInitializeAsync();

    public async Task<bool> IsEnabledAsync()
        => (_serverModeOutputDevice is not null && await _serverModeOutputDevice.IsEnabledAsync()) || await _originalOutputDevice.IsEnabledAsync();

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (_originalOutputDevice is ITestSessionLifetimeHandler originalLifetimeHandler)
        {
            await originalLifetimeHandler.OnTestSessionFinishingAsync(sessionUid, cancellationToken);
        }
    }

    public async Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (_originalOutputDevice is ITestSessionLifetimeHandler originalLifetimeHandler)
        {
            await originalLifetimeHandler.OnTestSessionStartingAsync(sessionUid, cancellationToken);
        }
    }

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost);
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (_originalOutputDevice is IDataConsumer dataConsumer)
        {
            await dataConsumer.ConsumeAsync(dataProducer, value, cancellationToken);
        }
    }

    public void Dispose()
        => (_originalOutputDevice as IDisposable)?.Dispose();
}
