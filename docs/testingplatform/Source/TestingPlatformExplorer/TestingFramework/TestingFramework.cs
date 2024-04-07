// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace TestingPlatformExplorer.TestingFramework;

internal class TestingFramework : ITestFramework
{
    private TestingFrameworkCapabilities _capabilities;
    private readonly IServiceProvider _serviceProvider;

    public TestingFramework(ITestFrameworkCapabilities capabilities, IServiceProvider serviceProvider)
    {
        _capabilities = (TestingFrameworkCapabilities)capabilities;
        _serviceProvider = serviceProvider;
    }

    public string Uid => nameof(TestingFramework);

    public string Version => "1.0.0";

    public string DisplayName => "TestingFramework";

    public string Description => "Testing framework sample";

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        throw new NotImplementedException();
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        throw new NotImplementedException();
    }

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsEnabledAsync()
    {
        throw new NotImplementedException();
    }
}
