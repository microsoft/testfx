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
#if NET9_0_OR_GREATER
    private readonly Lock _outputPolicyLock = new();
#else
    private readonly object _outputPolicyLock = new();
#endif
    private int _maxFailedTestsCallbackRegistered;
    private bool _connectionClosed;
    private bool _deferredShowMessage;

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

    internal bool ConfigureShowMessage(bool? requested, bool deferSuppression = false)
    {
        lock (_outputPolicyLock)
        {
            bool applied = !_connectionClosed && requested == true
                && _serverModeOutputDevice is not null
                && !OperatingSystem.IsBrowser()
                && OriginalOutputDevice is TerminalOutputDevice { SupportsShowMessage: true };
            if (!OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
            {
                _deferredShowMessage = applied && deferSuppression;
                terminal.SuppressConsoleOutput = applied && !deferSuppression;
            }

            return applied;
        }
    }

    internal void EndConnection()
    {
        lock (_outputPolicyLock)
        {
            _connectionClosed = true;
            if (!OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
            {
                terminal.SuppressConsoleOutput = false;
            }
        }
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        bool suppressed = false;
        if (!OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
        {
            suppressed = await terminal.DisplayWithSuppressionAsync(producer, data, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await OriginalOutputDevice.DisplayAsync(producer, data, cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            bool forwarded;
            try
            {
                forwarded = await _serverModeOutputDevice.ForwardAsync(producer, data, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                if (suppressed && !OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice cancelledTerminal)
                {
                    try
                    {
                        await cancelledTerminal.RenderAsync(producer, data, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception renderException)
                    {
                        // Preserve the forwarding cancellation, but make a secondary renderer failure observable.
                        exception.Data["OutputDeviceRenderException"] = renderException;
                        try
                        {
                            await cancelledTerminal.LogRenderFailureAsync(renderException).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // The original cancellation still owns this operation; the render failure is attached above.
                        }
                    }
                }

                throw;
            }

            if (suppressed && !forwarded && !OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice fallbackTerminal)
            {
                await fallbackTerminal.RenderAsync(producer, data, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        bool suppressed = false;
        if (!OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
        {
            suppressed = await terminal.DisplayBannerWithSuppressionAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await OriginalOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }

        if (_serverModeOutputDevice is not null)
        {
            bool forwarded = await _serverModeOutputDevice.ForwardBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
            if (suppressed && !forwarded && !OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice fallbackTerminal)
            {
                await fallbackTerminal.RenderBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
            }
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
            // InitializeAsync attaches synchronously before draining output already shown locally.
            Task initialization = _serverModeOutputDevice.InitializeAsync(serverTestHost);
            lock (_outputPolicyLock)
            {
                if (_deferredShowMessage && !_connectionClosed
                    && !OperatingSystem.IsBrowser() && OriginalOutputDevice is TerminalOutputDevice terminal)
                {
                    terminal.SuppressConsoleOutput = true;
                }

                _deferredShowMessage = false;
            }

            await initialization.ConfigureAwait(false);
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
                async (maxFailedTests, callbackCancellationToken) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), callbackCancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        EndConnection();
        _serverModeOutputDevice?.Dispose();
    }
}
