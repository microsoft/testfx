// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// WASI-specific output device that uses standard console output.
/// </summary>
[SupportedOSPlatform("wasi")]
internal sealed class WasiOutputDevice : SimplifiedConsoleOutputDeviceBase
{
    private readonly IConsole _console;

    public WasiOutputDevice(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature,
        IEnvironment environment,
        IPlatformInformation platformInformation,
        IStopPoliciesService policiesService)
        : base(console, testApplicationModuleInfo, asyncMonitor, runtimeFeature, environment, platformInformation, policiesService)
    {
        _console = console;
    }

    /// <inheritdoc />
    public override string DisplayName => "Test Platform WASI Console Service";

    /// <inheritdoc />
    public override string Description => "Test Platform WASI console service";

    protected override void ConsoleWarn(string? message) => _console.WriteLine(message);

    protected override void ConsoleError(string? message) => _console.WriteLine(message);

    protected override void ConsoleLog(string? message) => _console.WriteLine(message);
}
