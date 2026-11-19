// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Enumeration for how we access data rows in data driven testing.
/// </summary>
public enum DataAccessMethod
{
    /// <summary>
    /// Rows are returned in sequential order.
    /// </summary>
    Sequential,

    /// <summary>
    /// Rows are returned in random order.
    /// </summary>
    Random,
}
