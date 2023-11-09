// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ServerTestExecutionFilterFactory : ITestExecutionFilterFactory
{
    public string Uid => nameof(ServerTestExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ServerTestExecutionFilterFactory);

    public string Description => nameof(ServerTestExecutionFilterFactory);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync()
        => Task.FromResult((true, (ITestExecutionFilter?)new NopFilter()));
}
