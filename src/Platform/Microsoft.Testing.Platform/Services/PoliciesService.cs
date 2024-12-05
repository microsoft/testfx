// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Services;

internal sealed class PoliciesService : IPoliciesService
{
    private BlockingCollection<Func<CancellationToken, Task>>? _onStopTestExecutionCallbacks;

    public void RegisterOnStopTestExecution(Func<CancellationToken, Task> callback)
        => (_onStopTestExecutionCallbacks ??= new()).Add(callback);

    internal async Task ExecuteOnStopTestExecutionCallbacks(CancellationToken cancellationToken)
    {
        if (_onStopTestExecutionCallbacks is null)
        {
            return;
        }

        foreach (Func<CancellationToken, Task> callback in _onStopTestExecutionCallbacks)
        {
            await callback.Invoke(cancellationToken);
        }
    }
}
