﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class UnhandledExceptionHandler(IEnvironment environment, IConsole console, bool isTestHost = false) : IUnhandledExceptionsHandler
{
    private readonly IEnvironment _environment = environment;
    private readonly IConsole _console = console;
    private readonly bool _isTestController = !isTestHost;
    private ILogger? _logger;

    public void Subscribe()
    {
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    public void SetLogger(ILogger logger)
        => _logger = logger;

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string error = $"{e.ExceptionObject}{_environment.NewLine}IsTerminating: {e.IsTerminating}";
        LogErrorAndExit(error, !e.IsTerminating);
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        string error = $"Unhandled exception: {e.Exception}";
        LogErrorAndExit(error, true);
    }

    private void LogErrorAndExit(string error, bool forceClose)
    {
        string errorPrefix = $"[UnhandledExceptionHandler.OnCurrentDomainUnhandledException{(_isTestController ? "(testhost controllers)" : "(testhost)")}] ";
        _console.WriteLine(errorPrefix + error);
        _logger?.LogCritical(errorPrefix + error);
        if (forceClose)
        {
            _environment.FailFast(errorPrefix + error);
        }
    }
}
