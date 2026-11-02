// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed class SingleFlightTask
{
    private readonly object _lock = new();
    private Task? _task;

    public Task StartAsync(Action action)
        => StartCoreAsync(() => Task.Run(action));

    public Task StartAsync(Func<Task> action)
        => StartCoreAsync(() => Task.Run(action));

    private Task StartCoreAsync(Func<Task> taskFactory)
    {
        lock (_lock)
        {
            return _task ??= taskFactory();
        }
    }
}
