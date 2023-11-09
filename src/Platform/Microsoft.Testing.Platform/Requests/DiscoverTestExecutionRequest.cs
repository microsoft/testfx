// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

public class DiscoverTestExecutionRequest : TestExecutionRequest
{
    public DiscoverTestExecutionRequest(TestSessionContext session)
        : this(session, new NopFilter())
    {
    }

    public DiscoverTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter)
        : base(session, executionFilter)
    {
    }
}
