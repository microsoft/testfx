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
    public VSTestRunTestExecutionRequest(TestSessionContext session, VSTestTestExecutionFilter executionFilter, string[] assemblyPaths,
        IRunContext runContext, IFrameworkHandle frameworkHandle)
        : base(session, executionFilter)
    {
        AssemblyPaths = assemblyPaths;
        RunContext = runContext;
        FrameworkHandle = frameworkHandle;
    }

    public VSTestTestExecutionFilter VSTestFilter
        => (VSTestTestExecutionFilter)Filter;

    public string[] AssemblyPaths { get; }

    public IRunContext RunContext { get; }

    public IFrameworkHandle FrameworkHandle { get; }
}
