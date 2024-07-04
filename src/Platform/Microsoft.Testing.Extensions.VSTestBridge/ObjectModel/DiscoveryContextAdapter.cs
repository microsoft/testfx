// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A Microsoft Testing Platform oriented implementation of the VSTest <see cref="IDiscoveryContext"/>.
/// </summary>
internal sealed class DiscoveryContextAdapter : ContextAdapterBase, IDiscoveryContext
{
    public DiscoveryContextAdapter(ICommandLineOptions commandLineOptions, IRunSettings? runSettings = null)
        : base(commandLineOptions)
    {
        RunSettings = runSettings;
    }

    public IRunSettings? RunSettings { get; }
}
