// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers;

internal enum Category
{
    /// <summary>
    /// Rules that support designing test suites.
    /// </summary>
    Design,

    /// <summary>
    /// Rules that support high-performance testing.
    /// </summary>
    Performance,

    /// <summary>
    /// Rules that support proper usage of MSTest.
    /// </summary>
    Usage,
}
