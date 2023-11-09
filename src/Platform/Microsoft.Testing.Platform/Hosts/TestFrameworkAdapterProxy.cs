// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Microsoft.Testing.Platform.Hosts;

// This is needed to avoid that methods will be called by extensions, they can only query the information.
internal sealed class TestFrameworkAdapterProxy : ITestFramework
{
    private readonly ITestFramework _testFrameworkAdapter;

    public TestFrameworkAdapterProxy(ITestFramework testFrameworkAdapter)
    {
        _testFrameworkAdapter = testFrameworkAdapter;
    }

    /// <inheritdoc />
    public string Uid => _testFrameworkAdapter.Uid;

    /// <inheritdoc />
    public string Version => _testFrameworkAdapter.Version;

    /// <inheritdoc />
    public string DisplayName => _testFrameworkAdapter.DisplayName;

    /// <inheritdoc />
    public string Description => _testFrameworkAdapter.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => throw new InvalidOperationException("CreateTestSessionAsync is an operation allowed only to the test engine.");

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
        => throw new InvalidOperationException("ExecuteRequestAsync is an operation allowed only to the test engine.");

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => throw new InvalidOperationException("CloseTestSessionAsync is an operation allowed only to the test engine.");
}
