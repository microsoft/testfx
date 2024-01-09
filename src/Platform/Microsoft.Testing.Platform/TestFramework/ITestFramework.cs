// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.TestFramework;

public interface ITestFramework : IExtension
{
    /// <summary>
    /// Ask to the test framework to create a test session.
    /// </summary>
    /// <param name="context">The test session creation context.</param>
    Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context);

    /// <summary>
    /// Ask to the test framework to execute a test execution request.
    /// </summary>
    /// <param name="context">The test execution request context.</param>
    Task ExecuteRequestAsync(ExecuteRequestContext context);

    /// <summary>
    /// Ask to the test framework to destroy the test session and release all the resources.
    /// </summary>
    /// <param name="context">The test session destruction context.</param>
    Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context);
}
