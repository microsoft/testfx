// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionRequestFactory(ICommandLineOptions commandLineService, ITestExecutionFilterFactory testExecutionFilterFactory) : ITestExecutionRequestFactory
{
    private readonly ICommandLineOptions _commandLineService = commandLineService;
    private readonly ITestExecutionFilterFactory _testExecutionFilterFactory = testExecutionFilterFactory;

    public async Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session)
    {
        (bool created, ITestExecutionFilter? testExecutionFilter) = await _testExecutionFilterFactory.TryCreateAsync();
        if (!created)
        {
            throw new InvalidOperationException(PlatformResources.CannotCreateTestExecutionFilterErrorMessage);
        }

        ApplicationStateGuard.Ensure(testExecutionFilter is not null);

        TestExecutionRequest testExecutionRequest = _commandLineService.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            ? new DiscoverTestExecutionRequest(session, testExecutionFilter)
            : new RunTestExecutionRequest(session, testExecutionFilter);

        return testExecutionRequest;
    }
}
