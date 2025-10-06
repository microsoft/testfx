// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides test execution filters based on TestNode UIDs from server-mode requests.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public sealed class TestNodeUidRequestFilterProvider : IRequestFilterProvider
{
    /// <inheritdoc />
    public string Uid => nameof(TestNodeUidRequestFilterProvider);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => "TestNode UID Request Filter Provider";

    /// <inheritdoc />
    public string Description => "Creates filters for requests that specify test nodes by UID";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public bool CanHandle(IServiceProvider serviceProvider)
    {
        ITestExecutionRequestContext? context = serviceProvider.GetServiceInternal<ITestExecutionRequestContext>();
        return context?.TestNodes is not null;
    }

    /// <inheritdoc />
    public Task<ITestExecutionFilter> CreateFilterAsync(IServiceProvider serviceProvider)
    {
        ITestExecutionRequestContext context = serviceProvider.GetRequiredService<ITestExecutionRequestContext>();
        Guard.NotNull(context.TestNodes);

        ITestExecutionFilter filter = new TestNodeUidListFilter([.. context.TestNodes.Select(node => node.Uid)]);
        return Task.FromResult(filter);
    }
}
