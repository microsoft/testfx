// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides test execution filters based on TestNode UIDs from server-mode requests.
/// </summary>
internal sealed class TestNodeUidRequestFilterProvider : IRequestFilterProvider
{
    public string Uid => nameof(TestNodeUidRequestFilterProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => "TestNode UID Request Filter Provider";

    public string Description => "Creates filters for requests that specify test nodes by UID";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public bool CanHandle(RequestArgsBase args) => args.TestNodes is not null;

    public Task<ITestExecutionFilter> CreateFilterAsync(RequestArgsBase args)
    {
        Guard.NotNull(args.TestNodes);
        ITestExecutionFilter filter = new TestNodeUidListFilter(args.TestNodes.Select(node => node.Uid).ToArray());
        return Task.FromResult(filter);
    }
}
