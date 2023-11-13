// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Requests;

internal sealed class ConsoleTestExecutionFilterFactory : ITestExecutionFilterFactory
{
    private readonly ICommandLineOptions _commandLineService;

    public ConsoleTestExecutionFilterFactory(ICommandLineOptions commandLineService)
    {
        _commandLineService = commandLineService;
    }

    public string Uid => nameof(ConsoleTestExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ConsoleTestExecutionFilterFactory);

    public string Description => nameof(ConsoleTestExecutionFilterFactory);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync() =>
        _commandLineService.TryGetOptionArgumentList(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out string[]? filter)
            ? Task.FromResult((true, (ITestExecutionFilter?)new TreeNodeFilter(filter[0])))
            : Task.FromResult((true, (ITestExecutionFilter?)new NopFilter()));
}
