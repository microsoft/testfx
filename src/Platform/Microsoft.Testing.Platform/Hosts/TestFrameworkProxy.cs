﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Hosts;

// This is needed to avoid that methods will be called by extensions, they can only query the information.
internal sealed class TestFrameworkProxy(ITestFramework testFramework) : ITestFramework
{
    private readonly ITestFramework _testFramework = testFramework;

    /// <inheritdoc />
    public string Uid => _testFramework.Uid;

    /// <inheritdoc />
    public string Version => _testFramework.Version;

    /// <inheritdoc />
    public string DisplayName => _testFramework.DisplayName;

    /// <inheritdoc />
    public string Description => _testFramework.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestFrameworkProxyApiShouldNotBeCalled, "CreateTestSessionAsync"));

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
        => throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestFrameworkProxyApiShouldNotBeCalled, "ExecuteRequestAsync"));

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestFrameworkProxyApiShouldNotBeCalled, "CloseTestSessionAsync"));
}
