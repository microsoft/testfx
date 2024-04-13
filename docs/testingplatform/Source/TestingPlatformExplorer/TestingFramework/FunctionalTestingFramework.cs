// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

using TestingPlatformExplorer.UnitTests;

namespace TestingPlatformExplorer.FunctionalTestingFramework;

internal class FunctionalTestingFramework : ITestFramework
{
    private readonly FunctionalTestingFrameworkCapabilities _capabilities;
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<(TestOutCome, string)>[] _actions;

    public FunctionalTestingFramework(
        ITestFrameworkCapabilities capabilities,
        IServiceProvider serviceProvider,
        Func<(TestOutCome, string)>[] actions)
    {
        _capabilities = (FunctionalTestingFrameworkCapabilities)capabilities;
        _serviceProvider = serviceProvider;
        _actions = actions;
    }

    public string Uid => nameof(FunctionalTestingFramework);

    public string Version => "1.0.0";

    public string DisplayName => "TestingFramework";

    public string Description => "Testing framework sample";

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    }

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }
}
