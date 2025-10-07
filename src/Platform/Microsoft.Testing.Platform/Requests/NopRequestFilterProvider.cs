// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides a no-op filter that allows all tests to execute.
/// This is the fallback provider when no other provider can handle the request.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public sealed class NopRequestFilterProvider : IRequestFilterProvider
{
    /// <inheritdoc />
    public string Uid => nameof(NopRequestFilterProvider);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => "No-Operation Filter Provider";

    /// <inheritdoc />
    public string Description => "Fallback provider that allows all tests to execute";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public bool CanHandle(IServiceProvider serviceProvider) => true;

    /// <inheritdoc />
    public Task<ITestExecutionFilter> CreateFilterAsync(IServiceProvider serviceProvider)
    {
        ITestExecutionFilter filter = new NopFilter();
        return Task.FromResult(filter);
    }
}
