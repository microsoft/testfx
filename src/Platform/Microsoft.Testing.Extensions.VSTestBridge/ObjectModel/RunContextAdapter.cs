// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A Microsoft Testing Platform oriented implementation of the VSTest <see cref="IRunContext"/>.
/// </summary>
internal sealed class RunContextAdapter : ContextAdapterBase, IRunContext
{
    public RunContextAdapter(ICommandLineOptions commandLineOptions, IRunSettings runSettings)
        : base(commandLineOptions)
    {
        RoslynDebug.Assert(runSettings.SettingsXml is not null);

        RunSettings = runSettings;

        // Parse and take the results directory from the runsettings.
        TestRunDirectory = XElement.Parse(runSettings.SettingsXml).Descendants("ResultsDirectory").SingleOrDefault()?.Value;
    }

    public RunContextAdapter(ICommandLineOptions commandLineOptions, IRunSettings runSettings, TestNodeUid[] testNodeUids)
        : this(commandLineOptions, runSettings)
    {
        FilterExpressionWrapper = new(CreateFilter(testNodeUids));
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

    // We expect only GUID values in the testNodeUids.
    private static string CreateFilter(TestNodeUid[] testNodesUid)
    {
        StringBuilder filter = new();

        for (int i = 0; i < testNodesUid.Length; i++)
        {
            if (Guid.TryParse(testNodesUid[i].Value, out Guid guid))
            {
                filter.Append("Id=");
                filter.Append(guid.ToString());
            }
            else
            {
                throw new InvalidOperationException(ExtensionResources.InvalidFilterValue);
            }

            if (i != testNodesUid.Length - 1)
            {
                filter.Append('|');
            }
        }

        return filter.ToString();
    }
}
