// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxMergeTool(ICommandLineOptions commandLineOptions, IExtension extension) : ITool
{
    public const string ToolName = "merge-trx";

    private readonly ICommandLineOptions _commandLineOptions = commandLineOptions;
    private readonly IExtension _extension = extension;

    public string Name => ToolName;

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_commandLineOptions.TryGetOptionArgumentList(TrxMergeToolCommandLine.InputOptionName, out string[]? inputPaths));
        ApplicationStateGuard.Ensure(_commandLineOptions.TryGetOptionArgumentList(TrxMergeToolCommandLine.OutputOptionName, out string[]? outputPaths));
        ApplicationStateGuard.Ensure(outputPaths.Length == 1);

        await TrxReportEngine.MergeToFileAsync(
            inputPaths,
            outputPaths[0],
            TrxReportEngine.CreateMergeRunId(inputPaths),
            TrxReport.Resources.ExtensionResources.TrxMergedRunName,
            cancellationToken).ConfigureAwait(false);

        return (int)ExitCode.Success;
    }
}
