// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionRequestFactory(ICommandLineOptions commandLineService, ITestExecutionFilter testExecutionFilter) : ITestExecutionRequestFactory
{
    public Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session)
    {
        ApplicationStateGuard.Ensure(testExecutionFilter is not null);

        TestExecutionRequest testExecutionRequest = commandLineService.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            ? new DiscoverTestExecutionRequest(session, testExecutionFilter)
            : new RunTestExecutionRequest(session, testExecutionFilter);

        return Task.FromResult(testExecutionRequest);
    }
}
