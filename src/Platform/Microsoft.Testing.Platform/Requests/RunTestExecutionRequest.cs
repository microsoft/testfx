// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

public class RunTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter) : TestExecutionRequest(session, executionFilter)
{
    public RunTestExecutionRequest(TestSessionContext session)
        : this(session, new NopFilter())
    {
    }
}
