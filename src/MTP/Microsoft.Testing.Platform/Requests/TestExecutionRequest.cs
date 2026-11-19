// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a request for test execution.
/// </summary>
public abstract class TestExecutionRequest : IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionRequest"/> class.
    /// </summary>
    /// <param name="session">The test session context.</param>
    /// <param name="filter">The test execution filter.</param>
    protected TestExecutionRequest(TestSessionContext session, ITestExecutionFilter filter)
    {
        Session = session;
        Filter = filter;
    }

    /// <summary>
    /// Gets the test execution filter.
    /// </summary>
    public ITestExecutionFilter Filter { get; }

    /// <summary>
    /// Gets the test session context.
    /// </summary>
    public TestSessionContext Session { get; }
}
