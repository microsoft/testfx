// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides a no-op filter that allows all tests to execute.
/// This is the fallback provider when no other provider can handle the request.
/// </summary>
internal sealed class NopRequestFilterProvider : IRequestFilterProvider
{
    public string Uid => nameof(NopRequestFilterProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => "No-Operation Filter Provider";

    public string Description => "Fallback provider that allows all tests to execute";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public bool CanHandle(RequestArgsBase args) => true;

    public Task<ITestExecutionFilter> CreateFilterAsync(RequestArgsBase args)
    {
        ITestExecutionFilter filter = new NopFilter();
        return Task.FromResult(filter);
    }
}
