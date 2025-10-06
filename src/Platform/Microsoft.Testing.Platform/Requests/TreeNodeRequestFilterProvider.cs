// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides test execution filters based on tree-based graph filters from server-mode requests.
/// </summary>
internal sealed class TreeNodeRequestFilterProvider : IRequestFilterProvider
{
    public string Uid => nameof(TreeNodeRequestFilterProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => "TreeNode Graph Filter Provider";

    public string Description => "Creates filters for requests that specify a graph filter expression";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public bool CanHandle(RequestArgsBase args) => args.GraphFilter is not null;

    public Task<ITestExecutionFilter> CreateFilterAsync(RequestArgsBase args)
    {
        Guard.NotNull(args.GraphFilter);
        ITestExecutionFilter filter = new TreeNodeFilter(args.GraphFilter);
        return Task.FromResult(filter);
    }
}
