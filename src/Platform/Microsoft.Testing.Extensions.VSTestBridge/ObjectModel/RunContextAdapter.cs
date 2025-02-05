﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A Microsoft Testing Platform oriented implementation of the VSTest <see cref="IRunContext"/>.
/// </summary>
internal sealed class RunContextAdapter : ContextAdapterBase, IRunContext
{
    public RunContextAdapter(ICommandLineOptions commandLineOptions, IRunSettings runSettings, ITestExecutionFilter filter)
        : base(commandLineOptions, filter)
    {
        RoslynDebug.Assert(runSettings.SettingsXml is not null);

        RunSettings = runSettings;

        // Parse and take the results directory from the runsettings.
        TestRunDirectory = XElement.Parse(runSettings.SettingsXml).Descendants("ResultsDirectory").SingleOrDefault()?.Value;
    }

    // NOTE: Always false as it's TPv2 oriented and so not applicable to TA.

    /// <inheritdoc />
    public bool KeepAlive { get; }

    // NOTE:  Always false as it's TPv2 oriented and so not applicable to TA.

    /// <inheritdoc />
    public bool InIsolation { get; }

    // NOTE:  Always false as it's TPv2 oriented and so not applicable to TA.

    /// <inheritdoc />
    public bool IsDataCollectionEnabled { get; }

    /// <inheritdoc />
    public bool IsBeingDebugged => Debugger.IsAttached;

    /// <inheritdoc />
    public string? TestRunDirectory { get; }

    /// <inheritdoc />
    public string? SolutionDirectory { get; }

    /// <inheritdoc />
    public IRunSettings? RunSettings { get; }
}
