// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Services;

internal sealed class PoliciesService : IPoliciesService
{
    internal sealed class Policy
    {
        private BlockingCollection<Func<CancellationToken, Task>>? _callbacks;

        public void RegisterCallback(Func<CancellationToken, Task> callback)
            => (_callbacks ??= new()).Add(callback);

        public async Task ExecuteCallbacksAsync(CancellationToken cancellationToken)
        {
            if (_callbacks is null)
            {
                return;
            }

            foreach (Func<CancellationToken, Task> callback in _callbacks)
            {
                await callback.Invoke(cancellationToken);
            }
        }
    }

    internal Policy MaxFailedTestsPolicy { get; } = new();

    internal Policy AbortPolicy { get; } = new();

    public void RegisterOnMaxFailedTestsCallback(Func<CancellationToken, Task> callback)
        => MaxFailedTestsPolicy.RegisterCallback(callback);

    public void RegisterOnAbortCallback(Func<CancellationToken, Task> callback)
        => AbortPolicy.RegisterCallback(callback);
}
