// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// A specialized discover test execution request for VSTest. It contains the VSTest specific properties.
/// </summary>
public sealed class VSTestDiscoverTestExecutionRequest : DiscoverTestExecutionRequest
{
    internal VSTestDiscoverTestExecutionRequest(TestSessionContext session, VSTestTestExecutionFilter executionFilter, string[] assemblyPaths,
        IDiscoveryContext discoveryContext, IMessageLogger messageLogger, ITestCaseDiscoverySink discoverySink)
        : base(session, executionFilter)
    {
        AssemblyPaths = assemblyPaths;
        DiscoveryContext = discoveryContext;
        MessageLogger = messageLogger;
        DiscoverySink = discoverySink;
    }

    public VSTestTestExecutionFilter VSTestFilter
        => (VSTestTestExecutionFilter)Filter;

    public string[] AssemblyPaths { get; }

    public IDiscoveryContext DiscoveryContext { get; }

    public IMessageLogger MessageLogger { get; }

    public ITestCaseDiscoverySink DiscoverySink { get; }
}
