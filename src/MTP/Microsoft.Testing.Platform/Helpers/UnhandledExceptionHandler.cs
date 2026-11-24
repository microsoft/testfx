// Copyright (c) Microsoft Corporation. All rights reserved.
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
        string prefix = $"[UnhandledExceptionHandler.OnCurrentDomainUnhandledException{(_isTestController ? "(testhost controller workflow)" : "(testhost workflow)")}]";
        var exception = e.ExceptionObject as Exception;
        string consoleMessage = $"{prefix} {e.ExceptionObject}{_environment.NewLine}IsTerminating: {e.IsTerminating}";

        // The structured log keeps the prefix + IsTerminating in the message and routes the typed
        // exception through the exception parameter so the existing formatter (and structured sinks)
        // can capture stack/inner exceptions independently of the message text.
        string logMessage = exception is not null
            ? $"{prefix} IsTerminating: {e.IsTerminating}"
            : consoleMessage;

        LogErrorAndExit(consoleMessage, logMessage, exception, !e.IsTerminating);
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        string prefix = $"[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException{(_isTestController ? "(testhost controller workflow)" : "(testhost workflow)")}]";
        string consoleMessage = $"{prefix} Unhandled exception: {e.Exception}";
        string logMessage = $"{prefix} Unhandled task exception";
        LogErrorAndExit(consoleMessage, logMessage, e.Exception, true);
    }

    private void LogErrorAndExit(string consoleMessage, string logMessage, Exception? exception, bool forceClose)
    {
        _console.WriteLine(consoleMessage);

        if (_logger is not null)
        {
            if (exception is not null)
            {
                _logger.Log(LogLevel.Critical, logMessage, exception, LoggingExtensions.Formatter);
            }
            else
            {
                _logger.LogCritical(logMessage);
            }
        }

        if (forceClose)
        {
            if (exception is not null)
            {
                _environment.FailFast(consoleMessage, exception);
            }
            else
            {
                _environment.FailFast(consoleMessage);
            }
        }
    }
}
