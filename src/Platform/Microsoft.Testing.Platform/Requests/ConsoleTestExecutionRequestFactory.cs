// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionRequestFactory(
    ICommandLineOptions commandLineService,
    ITestExecutionFilterFactory testExecutionFilterFactory,
    IReadOnlyList<ITestExecutionFilterProvider> testExecutionFilterProviders) : ITestExecutionRequestFactory
{
    private readonly ICommandLineOptions _commandLineService = commandLineService;
    private readonly ITestExecutionFilterFactory _testExecutionFilterFactory = testExecutionFilterFactory;
    private readonly IReadOnlyList<ITestExecutionFilterProvider> _testExecutionFilterProviders = testExecutionFilterProviders;

    public async Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session, CancellationToken cancellationToken)
    {
        (bool created, ITestExecutionFilter? testExecutionFilter) = await _testExecutionFilterFactory.TryCreateAsync().ConfigureAwait(false);
        if (!created)
        {
            throw new InvalidOperationException(PlatformResources.CannotCreateTestExecutionFilterErrorMessage);
        }

        ApplicationStateGuard.Ensure(testExecutionFilter is not null);

        bool isDiscovery = _commandLineService.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
        var context = new TestExecutionFilterContext(
            isDiscovery ? TestExecutionRequestKind.Discovery : TestExecutionRequestKind.Run,
            TestExecutionRequestOrigin.Console);
        testExecutionFilter = await TestExecutionFilterComposer.ComposeAsync(
            testExecutionFilter,
            _testExecutionFilterProviders,
            context,
            allowProviderContributions: true,
            cancellationToken).ConfigureAwait(false);

        TestExecutionRequest testExecutionRequest = isDiscovery
            ? new DiscoverTestExecutionRequest(session, testExecutionFilter)
            : new RunTestExecutionRequest(session, testExecutionFilter);

        return testExecutionRequest;
    }
}
