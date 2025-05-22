// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Special interface that, when returned from parameterized tests, is recognized specially.
/// </summary>
public interface ITestDataRow
{
    /// <summary>
    /// Gets the Value to pass to the test method.
    /// It can be a tuple to handle the case where the parameterized test method has multiple parameters.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Gets the ignore message. If non-null, the test case is considered ignored.
    /// </summary>
    string? IgnoreMessage { get; }

    /// <summary>
    /// Gets the display name for this specific test case. If null, the display name is calculated normally.
    /// </summary>
    string? DisplayName { get; }
}
