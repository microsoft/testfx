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
    internal VSTestDiscoverTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter, string[] assemblyPaths,
        IDiscoveryContext discoveryContext, IMessageLogger messageLogger, ITestCaseDiscoverySink discoverySink)
        : base(session, executionFilter)
    {
        AssemblyPaths = assemblyPaths;
        DiscoveryContext = discoveryContext;
        MessageLogger = messageLogger;
        DiscoverySink = discoverySink;
    }

    /// <summary>
    /// Gets the paths of the assemblies to discover tests from.
    /// </summary>
    public string[] AssemblyPaths { get; }

    /// <summary>
    /// Gets the discovery context for the test discovery.
    /// </summary>
    public IDiscoveryContext DiscoveryContext { get; }

    /// <summary>
    /// Gets the message logger to log messages.
    /// </summary>
    public IMessageLogger MessageLogger { get; }

    /// <summary>
    /// Gets the discovery sink to send discovered test cases to.
    /// </summary>
    public ITestCaseDiscoverySink DiscoverySink { get; }
}
