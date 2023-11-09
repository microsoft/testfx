// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemMonitor : IMonitor
{
    public IDisposable Lock(object obj)
        => new DisposableMonitor(obj);

    private readonly struct DisposableMonitor : IDisposable
    {
        private readonly object _obj;
        private readonly bool _lockTaken;

        public DisposableMonitor(object obj)
        {
            _obj = obj;
            Monitor.Enter(obj, ref _lockTaken);
        }

        public void Dispose()
        {
            if (_lockTaken)
            {
                Monitor.Exit(_obj);
            }
        }
    }
}
