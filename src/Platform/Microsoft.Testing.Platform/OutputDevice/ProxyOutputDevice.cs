// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class ProxyOutputDevice : IOutputDevice, IOutputDeviceDataProducer, IDisposable
{
    private readonly ServerModePerCallOutputDevice? _serverModeOutputDevice;
    private readonly IStopPoliciesService? _policiesService;
    private int _maxFailedTestsCallbackRegistered;

    public ProxyOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice, IStopPoliciesService? policiesService)
    {
        OriginalOutputDevice = originalOutputDevice;
        _serverModeOutputDevice = serverModeOutputDevice;
        _policiesService = policiesService;
    }

    public string Uid => nameof(ProxyOutputDevice);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(ProxyOutputDevice);

    public string Description => nameof(ProxyOutputDevice);

    internal IPlatformOutputDevice OriginalOutputDevice { get; }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    internal bool ConfigureRpcOnlyOutput(bool? requested)
    {
        bool applied = requested == true
            && _serverModeOutputDevice is not null
            && !OperatingSystem.IsBrowser()
            && OriginalOutputDevice is TerminalOutputDevice { SupportsRpcOnlyOutput: true };
        if (!OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
        {
            terminal.SuppressConsoleOutput = applied;
        }

        return applied;
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        await OriginalOutputDevice.DisplayAsync(producer, data, cancellationToken).ConfigureAwait(false);

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        await OriginalOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        await OriginalOutputDevice.DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        await OriginalOutputDevice.DisplayAfterSessionEndRunAsync(cancellationToken).ConfigureAwait(false);

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAfterSessionEndRunAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.InitializeAsync(serverTestHost).ConfigureAwait(false);
        }
    }

    internal async Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
    {
        await OriginalOutputDevice.HandleProcessRoleAsync(processRole, cancellationToken).ConfigureAwait(false);

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.HandleProcessRoleAsync(processRole, cancellationToken).ConfigureAwait(false);
        }

        if (processRole == TestProcessRole.TestHost
            && _policiesService is not null
            && Interlocked.Exchange(ref _maxFailedTestsCallbackRegistered, 1) == 0)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    public void Dispose()
        => _serverModeOutputDevice?.Dispose();
}
