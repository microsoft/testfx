﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a request to discover test execution.
/// </summary>
public class DiscoverTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter) : TestExecutionRequest(session, executionFilter)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverTestExecutionRequest"/> class.
    /// </summary>
    /// <param name="session">The test session context.</param>
    public DiscoverTestExecutionRequest(TestSessionContext session)
        : this(session, new NopFilter())
    {
    }
}
