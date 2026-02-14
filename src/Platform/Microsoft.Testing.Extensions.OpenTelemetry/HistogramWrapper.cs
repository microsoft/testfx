// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class HistogramWrapper<T> : IHistogram<T>
    where T : struct
{
    private readonly Histogram<T> _histogram;

    public HistogramWrapper(Histogram<T> histogram)
        => _histogram = histogram
            ?? throw new ArgumentNullException(nameof(histogram));

    public void Record(T value)
        => _histogram.Record(value);
}
