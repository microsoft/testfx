// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemRuntime : IRuntime
{
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IProcessHandler _process;
    private readonly CommandLineParseResult? _parseResult;

    public SystemRuntime(IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler process, CommandLineParseResult parseResult)
    {
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _process = process;
        _parseResult = parseResult;
    }

    public SystemRuntime(IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler process)
    {
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _process = process;
    }

    public ITestApplicationModuleInfo GetCurrentModuleInfo() => new CurrentTestApplicationModuleInfo(_runtimeFeature, _environment, _process);

    public ITestHostControllerInfo GetTestHostControllerInfo()
        => _parseResult is null ? throw new InvalidOperationException("Unexpected usage of GetTestHostControllerInfo()") : new TestHostControllerInfo(_parseResult);
}
