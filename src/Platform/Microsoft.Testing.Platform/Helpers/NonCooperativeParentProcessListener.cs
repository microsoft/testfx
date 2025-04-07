﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class NonCooperativeParentProcessListener : IDisposable
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private Process? _parentProcess;

    public NonCooperativeParentProcessListener(ICommandLineOptions commandLineOptions, IEnvironment environment)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        SubscribeToParentProcess();
    }

    private void SubscribeToParentProcess()
    {
        _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, out string[]? pid);
        ApplicationStateGuard.Ensure(pid is not null);
        RoslynDebug.Assert(pid.Length == 1);

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            _parentProcess = Process.GetProcessById(int.Parse(pid[0], CultureInfo.InvariantCulture));
            _parentProcess.EnableRaisingEvents = true;
            _parentProcess.Exited += ParentProcess_Exited;
#pragma warning restore CA1416
        }
        catch (ArgumentException)
        {
            // If we fail the process is already gone, so we can just exit.
            // The first check is already done inside the command line parser.
            _environment.Exit(ExitCodes.DependentProcessExited);
        }
    }

    private void ParentProcess_Exited(object? sender, EventArgs e) => _environment.Exit(ExitCodes.DependentProcessExited);

    public void Dispose() => _parentProcess?.Dispose();
}
