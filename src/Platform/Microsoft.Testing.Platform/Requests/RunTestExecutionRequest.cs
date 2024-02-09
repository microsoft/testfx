// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a request to run test execution.
/// </summary>
public class RunTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter)
    : TestExecutionRequest(session, executionFilter)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunTestExecutionRequest"/> class.
    /// </summary>
    /// <param name="session">The test session context.</param>
    public RunTestExecutionRequest(TestSessionContext session)
        : this(session, new NopFilter())
    {
    }
}
