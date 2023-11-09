// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ServerTestExecutionRequestFactory : ITestExecutionRequestFactory
{
    private readonly Func<TestSessionContext, TestExecutionRequest> _factory;

    public ServerTestExecutionRequestFactory(Func<TestSessionContext, TestExecutionRequest> factory)
    {
        _factory = factory;
    }

    public Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session)
        => Task.FromResult(_factory(session));
}
