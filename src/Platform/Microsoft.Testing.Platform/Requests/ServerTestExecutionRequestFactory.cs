// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ServerTestExecutionRequestFactory(Func<TestSessionContext, CancellationToken, Task<TestExecutionRequest>> factory) : ITestExecutionRequestFactory
{
    private readonly Func<TestSessionContext, CancellationToken, Task<TestExecutionRequest>> _factory = factory;

    public Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session, CancellationToken cancellationToken)
        => _factory(session, cancellationToken);
}
