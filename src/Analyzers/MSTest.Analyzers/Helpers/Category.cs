// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Helpers;

internal enum Category
{
    /// <summary>
    /// Rules that support designing test suites — structural and organizational patterns such as how test
    /// classes, fixtures, and data sources are shaped. Use this for guidance that improves the maintainability
    /// or correctness of the test design rather than raw API usage or runtime cost.
    /// </summary>
    Design,

    /// <summary>
    /// Rules that support high-performance testing — patterns that measurably affect test-suite execution time
    /// or resource usage. Use this for blocking calls (e.g. <c>Thread.Sleep</c>/<c>Task.Wait</c>), missing
    /// parallelism, or non-cooperative timeouts that keep threads busy after a test has timed out.
    /// </summary>
    Performance,

    /// <summary>
    /// Rules that support proper usage of MSTest — correct API usage such as valid method signatures, required
    /// attributes, and avoiding misused APIs. Use this when the diagnostic is about using the framework
    /// correctly rather than test design or runtime performance.
    /// </summary>
    Usage,
}
