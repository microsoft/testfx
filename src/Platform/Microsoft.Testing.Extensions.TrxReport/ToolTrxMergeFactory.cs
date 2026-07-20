// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class ToolTrxMergeFactory : IExtension
{
    public string Uid => "Microsoft.Testing.Extensions.TrxReport.MergeTool";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.TrxMergeToolDisplayName;

    public string Description => ExtensionResources.TrxMergeToolDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public TrxMergeTool CreateTool(ICommandLineOptions commandLineOptions)
        => new(commandLineOptions, this);

    public TrxMergeToolCommandLine CreateCommandLine()
        => new(this);
}
