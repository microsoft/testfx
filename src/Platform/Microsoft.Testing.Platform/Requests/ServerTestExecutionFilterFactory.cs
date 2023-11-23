// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ServerTestExecutionFilterFactory : ITestExecutionFilterFactory
{
    public string Uid => nameof(ServerTestExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => PlatformResources.ServerTestExecutionFilterFactoryDisplayName;

    public string Description => PlatformResources.ServerTestExecutionFilterFactoryDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync()
        => Task.FromResult((true, (ITestExecutionFilter?)new NopFilter()));
}
