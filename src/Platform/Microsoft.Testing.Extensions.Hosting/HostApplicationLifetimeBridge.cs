// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

internal sealed class HostApplicationLifetimeBridge(IHostApplicationLifetime hostApplicationLifetime) : ITestApplicationHostLifetimeBridge
{
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private CancellationTokenRegistration _hostStoppingRegistration;
    private CancellationTokenRegistration _testApplicationStoppingRegistration;
    private int _stopPropagationStarted;
    private bool _isConnected;

    public void Connect(Action requestTestApplicationStop, CancellationToken testApplicationStopping)
    {
        lock (_sync)
        {
            if (_isConnected)
            {
                throw new InvalidOperationException("The host application lifetime bridge is already connected.");
            }

            _isConnected = true;
            _hostStoppingRegistration = hostApplicationLifetime.ApplicationStopping.Register(
                static state => ((HostApplicationLifetimeBridgeState)state!).RequestTestApplicationStop(),
                new HostApplicationLifetimeBridgeState(this, requestTestApplicationStop));
            _testApplicationStoppingRegistration = testApplicationStopping.Register(
                static state => ((HostApplicationLifetimeBridge)state!).RequestHostStop(),
                this);
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            if (!_isConnected)
            {
                return;
            }

            _isConnected = false;
            _hostStoppingRegistration.Dispose();
            _testApplicationStoppingRegistration.Dispose();
        }
    }

    private void RequestHostStop()
    {
        if (Interlocked.Exchange(ref _stopPropagationStarted, 1) == 0)
        {
            hostApplicationLifetime.StopApplication();
        }
    }

    private sealed class HostApplicationLifetimeBridgeState(HostApplicationLifetimeBridge owner, Action requestTestApplicationStop)
    {
        public void RequestTestApplicationStop()
        {
            if (Interlocked.Exchange(ref owner._stopPropagationStarted, 1) == 0)
            {
                requestTestApplicationStop();
            }
        }
    }
}
