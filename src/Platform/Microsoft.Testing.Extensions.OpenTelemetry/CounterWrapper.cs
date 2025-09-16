// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class CounterWrapper<T> : ICounter<T>
    where T : struct
{
    private readonly Counter<T> _counter;

    public CounterWrapper(Counter<T> counter)
        => _counter = counter
            ?? throw new ArgumentNullException(nameof(counter));

    public void Add(T delta) => _counter.Add(delta);
}
