// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System.Runtime.InteropServices.JavaScript;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Browser-specific output device that uses JavaScript console APIs.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class BrowserOutputDevice : SimplifiedConsoleOutputDeviceBase
{
    private readonly bool _displayActiveTestProgress;

    public BrowserOutputDevice(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature,
        IEnvironment environment,
        IPlatformInformation platformInformation,
        ICommandLineOptions commandLineOptions,
        IStopPoliciesService policiesService)
        : base(console, testApplicationModuleInfo, asyncMonitor, runtimeFeature, environment, platformInformation, policiesService)
    {
        _displayActiveTestProgress = IsProgressEnabled(commandLineOptions);
    }

    /// <inheritdoc />
    public override string DisplayName => "Test Platform Browser Console Service";

    /// <inheritdoc />
    public override string Description => "Test Platform browser console service using JavaScript interop";

    protected override bool DisplayActiveTestProgress => _displayActiveTestProgress;

    [JSImport("globalThis.console.warn")]
    private static partial void JSConsoleWarn(string? message);

    [JSImport("globalThis.console.error")]
    private static partial void JSConsoleError(string? message);

    [JSImport("globalThis.console.log")]
    private static partial void JSConsoleLog(string? message);

    protected override void ConsoleWarn(string? message) => JSConsoleWarn(message);

    protected override void ConsoleError(string? message) => JSConsoleError(message);

    protected override void ConsoleLog(string? message) => JSConsoleLog(message);

    private static bool IsProgressEnabled(ICommandLineOptions commandLineOptions)
        => TerminalTestReporterCommandLineOptionsProvider.IsProgressEnabled(commandLineOptions);
}

#endif
