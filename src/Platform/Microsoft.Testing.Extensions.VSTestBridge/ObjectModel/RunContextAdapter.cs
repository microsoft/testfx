// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

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
        // We assume that the UIDs we receive are TestCase.FullyQualifiedName values.
        FilterExpressionWrapper = new(string.Join("|", testNodeUids.Select(ConvertToFullyQualifiedNameFilterString)));
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

    private static string ConvertToFullyQualifiedNameFilterString(TestNodeUid testNodeUid)
    {
        StringBuilder filterString = new("FullyQualifiedName=");

        for (int i = 0; i < testNodeUid.Value.Length; i++)
        {
            char currentChar = testNodeUid.Value[i];
            switch (currentChar)
            {
                case '\\':
                case '(':
                case ')':
                case '&':
                case '|':
                case '=':
                case '!':
                case '~':
                    // If the symbol is not escaped, add an escape character.
                    if (i - 1 < 0 || testNodeUid.Value[i - 1] != '\\')
                    {
                        filterString.Append('\\');
                    }

                    filterString.Append(currentChar);
                    break;

                default:
                    filterString.Append(currentChar);
                    break;
            }
        }

        return filterString.ToString();
    }
}
