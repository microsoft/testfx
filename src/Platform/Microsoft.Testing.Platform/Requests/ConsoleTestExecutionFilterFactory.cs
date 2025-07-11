// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionFilterFactory(ICommandLineOptions commandLineService) : ITestExecutionFilterFactory
{
    private readonly ICommandLineOptions _commandLineService = commandLineService;

    public string Uid => nameof(ConsoleTestExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => PlatformResources.ConsoleTestExecutionFilterFactoryDisplayName;

    public string Description => PlatformResources.ConsoleTestExecutionFilterFactoryDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync() =>
        _commandLineService.TryGetOptionArgument(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out string? filter)
            ? Task.FromResult((true, (ITestExecutionFilter?)new TreeNodeFilter(filter)))
            : Task.FromResult((true, (ITestExecutionFilter?)new NopFilter()));
}
