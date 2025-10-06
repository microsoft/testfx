// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a filter for test execution.
/// </summary>
public interface ITestExecutionFilter
{
    /// <summary>
    /// Determines whether the specified test node matches the filter criteria.
    /// </summary>
    /// <param name="testNode">The test node to evaluate.</param>
    /// <returns>true if the test node matches the filter; otherwise, false.</returns>
    bool Matches(TestNode testNode);
}
