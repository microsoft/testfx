// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class ToolTrxCompareFactory : IExtension
{
    /// <inheritdoc />
    public string Uid { get; } = nameof(TrxCompareTool);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxComparerToolDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxComparerToolDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public TrxCompareTool CreateTrxCompareTool(ICommandLineOptions commandLineOptions, IOutputDevice outputDisplay, ITask task)
        => new(commandLineOptions, this, outputDisplay, task);

    public TrxCompareToolCommandLine CreateTrxCompareToolCommandLine()
        => new(this);
}
