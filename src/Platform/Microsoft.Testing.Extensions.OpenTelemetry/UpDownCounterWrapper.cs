// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class UpDownCounterWrapper<T> : IUpDownCounter<T>
    where T : struct
{
    private readonly UpDownCounter<T> _counter;

    public UpDownCounterWrapper(UpDownCounter<T> counter)
        => _counter = counter
            ?? throw new ArgumentNullException(nameof(counter));

    public void Add(T delta, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        if (tags is null)
        {
            _counter.Add(delta);
            return;
        }

        _counter.Add(delta, MeasurementTags.ToArray(tags));
    }
}
