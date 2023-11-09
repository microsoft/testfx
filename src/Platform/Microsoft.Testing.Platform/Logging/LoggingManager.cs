// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class LoggingManager : ILoggingManager
{
    private readonly List<Func<LogLevel, IServiceProvider, ILoggerProvider>> _loggerProviderFullFactories = [];

    public void AddProvider(Func<LogLevel, IServiceProvider, ILoggerProvider> loggerProviderFactory)
    {
        ArgumentGuard.IsNotNull(loggerProviderFactory);
        _loggerProviderFullFactories.Add(loggerProviderFactory);
    }

    /*
     The expected order for the final logs directory is (most to least important):
     1 Environment variable
     2 Command line
     3 TA settings(json)
     4 Default(TestResults in the current working folder)
    */
    public static ApplicationLoggingState CreateFileLoggerIfDiagnosticIsEnabled(CommandLineParseResult result, IRuntime runtime, IClock clock, IEnvironment environment)
    {
        LogLevel logLevel = LogLevel.Information;

        if (result.HasError)
        {
            return new(logLevel, result);
        }

        string? environmentVariable = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC);
        if (!result.IsOptionSet(PlatformCommandLineProvider.DiagnosticOptionKey))
        {
            // Environment variable is set, but the command line option is not set
            if (environmentVariable != "1")
            {
                return new(logLevel, result);
            }
        }

        // Environment variable is set to 0 and takes precedence over the command line option
        if (environmentVariable == "0")
        {
            return new(logLevel, result);
        }

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, out string[]? verbosity))
        {
            logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), verbosity[0], true);
        }

        // Override the log level if the environment variable is set
        string? environmentLogLevel = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);
        if (!TAString.IsNullOrEmpty(environmentLogLevel))
        {
            if (!Enum.TryParse(environmentLogLevel, out LogLevel parsedLogLevel))
            {
                throw new ArgumentException("Invalid environment value 'TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY', Expected Trace, Debug, Information, Warning, Error, Critical.");
            }

            logLevel = parsedLogLevel;
        }

        // Set the directory to the default test result directory
        ITestApplicationModuleInfo currentModuleInfo = runtime.GetCurrentModuleInfo();
        string directory = Path.Combine(Path.GetDirectoryName(currentModuleInfo.GetCurrentTestApplicationFullPath())!, AggregatedConfiguration.DefaultTestResultFolderName);
        bool customDirectory = false;

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.ResultDirectoryOptionKey, out string[]? resultDirectoryArg))
        {
            directory = resultDirectoryArg[0];
            customDirectory = true;
        }

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey, out string[]? directoryArg))
        {
            directory = directoryArg[0];
            customDirectory = true;
        }

        // Override the output directory
        string? environmentOutputDirectory = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY);
        if (!TAString.IsNullOrEmpty(environmentOutputDirectory))
        {
#if NETCOREAPP
            directory = environmentOutputDirectory;
#else
            directory = environmentOutputDirectory!;
#endif
            customDirectory = true;
        }

        // Finally create the directory
        Directory.CreateDirectory(directory);

        string prefixName = "log";
        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey, out string[]? prefixNameArg))
        {
            prefixName = prefixNameArg[0];
        }

        // Override the prefix name
        string? environmentFilePrefix = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX);
        if (!TAString.IsNullOrEmpty(environmentFilePrefix))
        {
#if NETCOREAPP
            prefixName = environmentFilePrefix;
#else
            prefixName = environmentFilePrefix!;
#endif
        }

        bool synchronousWrite = result.IsOptionSet(PlatformCommandLineProvider.DiagnosticFileLoggerSynchronousWriteOptionKey);

        // Override the synchronous write
        string? environmentSynchronousWrite = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE);
        if (!TAString.IsNullOrEmpty(environmentSynchronousWrite))
        {
            synchronousWrite = environmentSynchronousWrite == "1";
        }

        return new(logLevel, result, new(directory, clock, logLevel, prefixName, customDirectory, synchronousWrite), synchronousWrite);
    }

    internal async Task<ILoggerFactory> BuildAsync(IServiceProvider serviceProvider, LogLevel logLevel, IMonitor monitor)
    {
        List<ILoggerProvider> loggerProviders = [];

        foreach (Func<LogLevel, IServiceProvider, ILoggerProvider> factory in _loggerProviderFullFactories)
        {
            ILoggerProvider serviceInstance = factory(logLevel, serviceProvider);
            if (serviceInstance is IExtension extension && !await extension.IsEnabledAsync())
            {
                continue;
            }

            if (serviceInstance is IAsyncInitializableExtension async)
            {
                await async.InitializeAsync();
            }

            loggerProviders.Add(serviceInstance);
        }

        return new LoggerFactory(loggerProviders.ToArray(), logLevel, monitor);
    }
}
