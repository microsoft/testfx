// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class TestExecutionRequestContext : ITestExecutionRequestContext
{
    public TestExecutionRequestContext(RequestArgsBase requestArgs)
    {
        TestNodes = requestArgs.TestNodes;
    }

    public ICollection<TestNode>? TestNodes { get; }
}
