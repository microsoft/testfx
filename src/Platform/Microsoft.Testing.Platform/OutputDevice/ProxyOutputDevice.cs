// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class ProxyOutputDevice : IOutputDevice, IDisposable
{
    private readonly ServerModePerCallOutputDevice? _serverModeOutputDevice;

    public ProxyOutputDevice(IPlatformOutputDevice originalOutputDevice, ServerModePerCallOutputDevice? serverModeOutputDevice)
    {
        OriginalOutputDevice = originalOutputDevice;
        _serverModeOutputDevice = serverModeOutputDevice;
    }

    internal IPlatformOutputDevice OriginalOutputDevice { get; }

    internal bool ConfigureRpcOnlyOutput(bool? requested)
        => _serverModeOutputDevice is not null && (_serverModeOutputDevice.RpcOnlyOutput = requested == true);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        if (_serverModeOutputDevice?.RpcOnlyOutput != true)
        {
            IOutputDeviceData originalData = data is ConnectionMessageOutputDeviceData connectionMessage
                ? new TextOutputDeviceData(connectionMessage.Text)
                : data;
            await OriginalOutputDevice.DisplayAsync(producer, originalData, cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayAsync(producer, data, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        if (_serverModeOutputDevice?.RpcOnlyOutput != true)
        {
            await OriginalOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        if (_serverModeOutputDevice?.RpcOnlyOutput != true)
        {
            await OriginalOutputDevice.DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        if (_serverModeOutputDevice?.RpcOnlyOutput != true)
        {
            await OriginalOutputDevice.DisplayAfterSessionEndRunAsync(cancellationToken).ConfigureAwait(false);
        }

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
        if (OriginalOutputDevice is IPlatformOutputDeviceWithRoleMessages roleMessagesDevice)
        {
            // Role callbacks are registered before initialize, so evaluate the negotiated route when they fire.
            await roleMessagesDevice.HandleProcessRoleAsync(
                processRole, () => _serverModeOutputDevice?.RpcOnlyOutput != true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await OriginalOutputDevice.HandleProcessRoleAsync(processRole, cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            await _serverModeOutputDevice.HandleProcessRoleAsync(processRole, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
        => _serverModeOutputDevice?.Dispose();
}
