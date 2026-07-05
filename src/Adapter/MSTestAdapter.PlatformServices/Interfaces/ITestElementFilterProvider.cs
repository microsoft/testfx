// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Platform-agnostic factory that produces the <see cref="ITestElementFilter"/> in effect for the current
/// run or discovery.
/// </summary>
/// <remarks>
/// This abstraction lets the platform services discovery and execution pipelines obtain the active filter
/// without taking a dependency on a specific test platform's filter object model (for example the VSTest
/// <c>ITestCaseFilterExpression</c> parsed from an <c>IRunContext</c> / <c>IDiscoveryContext</c>). The concrete
/// provider is created at the adapter boundary, closing over the host filter context; the engine invokes it at
/// the exact point it needs the filter so parse-error reporting keeps the same timing and per-source semantics.
/// </remarks>
internal interface ITestElementFilterProvider
{
    /// <summary>
    /// Builds the <see cref="ITestElementFilter"/> for the current run/discovery.
    /// </summary>
    /// <param name="logger">Logger used to report a filter parsing error.</param>
    /// <param name="filterHasError">
    /// Set to <see langword="true"/> when the filter is unsupported or failed to parse; in that case the caller
    /// should report no tests for the affected source.
    /// </param>
    /// <returns>
    /// The filter to apply, or <see langword="null"/> when no filter applies (every element is included).
    /// </returns>
    ITestElementFilter? GetTestElementFilter(IAdapterMessageLogger logger, out bool filterHasError);
}
