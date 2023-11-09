// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

public abstract class TestExecutionRequest : IRequest
{
    protected TestExecutionRequest(TestSessionContext session, ITestExecutionFilter filter)
    {
        Session = session;
        Filter = filter;
    }

    public ITestExecutionFilter Filter { get; }

    public TestSessionContext Session { get; }
}
