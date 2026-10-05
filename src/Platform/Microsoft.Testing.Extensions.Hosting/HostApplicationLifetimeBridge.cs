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
    private bool _isConnected;

    public void Connect(Action requestTestApplicationStop)
    {
        lock (_sync)
        {
            if (_isConnected)
            {
                throw new InvalidOperationException("The host application lifetime bridge is already connected.");
            }

            _isConnected = true;
            _hostStoppingRegistration = hostApplicationLifetime.ApplicationStopping.Register(
                static state => ((Action)state!).Invoke(),
                requestTestApplicationStop);
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            if (_isConnected)
            {
                _isConnected = false;
                _hostStoppingRegistration.Dispose();
            }
        }
    }
}
