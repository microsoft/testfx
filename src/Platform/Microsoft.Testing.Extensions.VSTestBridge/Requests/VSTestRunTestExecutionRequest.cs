// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// A specialized run test execution request for VSTest. It contains the VSTest specific properties.
/// </summary>
public sealed class VSTestRunTestExecutionRequest : RunTestExecutionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VSTestRunTestExecutionRequest"/> class.
    /// </summary>
    /// <param name="session">The test session context.</param>
    /// <param name="executionFilter">The test execution filter.</param>
    /// <param name="assemblyPaths">The assembly paths.</param>
    /// <param name="runContext">The VSTest run context.</param>
    /// <param name="frameworkHandle">The VSTest framework handle.</param>
    internal VSTestRunTestExecutionRequest(TestSessionContext session, ITestExecutionFilter executionFilter, string[] assemblyPaths,
        IRunContext runContext, IFrameworkHandle frameworkHandle)
        : base(session, executionFilter)
    {
        AssemblyPaths = assemblyPaths;
        RunContext = runContext;
        FrameworkHandle = frameworkHandle;
    }

    /// <summary>
    /// Gets the array of assembly paths.
    /// </summary>
    public string[] AssemblyPaths { get; }

    /// <summary>
    /// Gets the VSTest run context.
    /// </summary>
    public IRunContext RunContext { get; }

    /// <summary>
    /// Gets the VSTest framework handle.
    /// </summary>
    public IFrameworkHandle FrameworkHandle { get; }
}
