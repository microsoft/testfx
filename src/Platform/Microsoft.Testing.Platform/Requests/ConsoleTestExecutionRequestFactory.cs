// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionRequestFactory(ServiceProvider serviceProvider) : ITestExecutionRequestFactory
{
    public Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session)
    {
        ApplicationStateGuard.Ensure(serviceProvider is not null);

        ITestExecutionFilter testExecutionFilter = serviceProvider.GetTestExecutionFilter();

        TestExecutionRequest testExecutionRequest = serviceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            ? new DiscoverTestExecutionRequest(session, testExecutionFilter)
            : new RunTestExecutionRequest(session, testExecutionFilter);

        return Task.FromResult(testExecutionRequest);
    }
}
