// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemTask : ITask
{
    public Task Delay(int millisecondDelay)
        => Task.Delay(millisecondDelay);

    public Task Delay(TimeSpan timeSpan, CancellationToken cancellation)
        => Task.Delay(timeSpan, cancellation);
}
