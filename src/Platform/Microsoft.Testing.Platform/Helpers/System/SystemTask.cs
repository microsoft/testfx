// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemTask : ITask
{
    public Task WhenAll(IEnumerable<Task> tasks)
        => Task.WhenAll(tasks);

    public Task WhenAll(params Task[] tasks)
        => Task.WhenAll(tasks);

    public Task<Task> WhenAny(params Task[] tasks)
        => Task.WhenAny(tasks);

    public Task Delay(TimeSpan timeSpan)
        => Task.Delay(timeSpan);

    public Task Delay(int millisecondDelay)
        => Task.Delay(millisecondDelay);

    public Task Delay(TimeSpan timeSpan, CancellationToken cancellation)
        => Task.Delay(timeSpan, cancellation);
}
