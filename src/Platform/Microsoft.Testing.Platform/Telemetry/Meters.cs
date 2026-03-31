// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Defines a generic interface for a counter that operates on value types.
/// </summary>
/// <typeparam name="T">The value type that the counter tracks or manipulates.</typeparam>
internal interface ICounter<T>
    where T : struct
{
    /// <summary>
    /// Adds the specified value to the current total or state represented by the instance.
    /// </summary>
    /// <param name="delta">The value to add. The meaning of this value depends on the implementation and type parameter <typeparamref
    /// name="T"/>.</param>
    void Add(T delta);
}

/// <summary>
/// Defines the contract for a histogram that collects and analyzes values of a specified value type.
/// </summary>
/// <remarks>Implementations of this interface typically provide methods for recording values, retrieving
/// statistical summaries, and analyzing distributions. Histograms are commonly used for tracking metrics such as
/// latencies, frequencies, or other numeric measurements in performance monitoring and data analysis
/// scenarios.</remarks>
/// <typeparam name="T">The value type of the data points to be recorded in the histogram. Must be a struct.</typeparam>
internal interface IHistogram<T>
    where T : struct
{
    /// <summary>
    /// Records the specified value for later processing or analysis.
    /// </summary>
    /// <param name="value">The value to record. May represent data to be stored, logged, or tracked depending on the implementation.</param>
    void Record(T value);
}
