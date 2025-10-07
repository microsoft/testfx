// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides test execution filters based on tree-based graph filters from command line options.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public sealed class TreeNodeRequestFilterProvider : IRequestFilterProvider
{
    /// <inheritdoc />
    public string Uid => nameof(TreeNodeRequestFilterProvider);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => "TreeNode Graph Filter Provider";

    /// <inheritdoc />
    public string Description => "Creates filters for requests that specify a graph filter expression";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public bool CanHandle(IServiceProvider serviceProvider)
    {
        ICommandLineOptions commandLineOptions = serviceProvider.GetRequiredService<ICommandLineOptions>();
        return commandLineOptions.TryGetOptionArgumentList(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out _);
    }

    /// <inheritdoc />
    public Task<ITestExecutionFilter> CreateFilterAsync(IServiceProvider serviceProvider)
    {
        ICommandLineOptions commandLineOptions = serviceProvider.GetRequiredService<ICommandLineOptions>();
        bool hasFilter = commandLineOptions.TryGetOptionArgumentList(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out string[]? treenodeFilter);
        ApplicationStateGuard.Ensure(hasFilter);
        Guard.NotNull(treenodeFilter);

        ITestExecutionFilter filter = new TreeNodeFilter(treenodeFilter[0]);
        return Task.FromResult(filter);
    }
}
