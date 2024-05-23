// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ServerTestExecutionRequestFactory(Func<TestSessionContext, TestExecutionRequest> factory) : ITestExecutionRequestFactory
{
    public Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session)
        => Task.FromResult(factory(session));
}
