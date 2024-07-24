// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
#if NETCOREAPP
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents a test application.
/// </summary>
public sealed class TestApplication : ITestApplication
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly ITestHost _testHost;
    private static int s_numberOfBuilders;
    private static UnhandledExceptionHandler? s_unhandledExceptionHandler;

    static TestApplication()
    {
        // Capture system console soon as possible to avoid any other code from changing it.
        // This is important for the console display system to work properly.
        _ = new SystemConsole();
    }

    internal TestApplication(ITestHost testHost)
    {
        _testHost = testHost;
    }

    internal IServiceProvider ServiceProvider => ((CommonTestHost)_testHost).ServiceProvider;

    internal static int MaxNumberOfBuilders { get; set; } = int.MaxValue;

    /// <summary>
    /// Creates a server mode builder asynchronously.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="testApplicationOptions">The test application options.</param>
    /// <returns>The task representing the asynchronous operation.</returns>
    public static Task<ITestApplicationBuilder> CreateServerModeBuilderAsync(string[] args, TestApplicationOptions? testApplicationOptions = null)
    {
        if (args.Contains($"--{PlatformCommandLineProvider.ServerOptionKey}") || args.Contains($"-{PlatformCommandLineProvider.ServerOptionKey}"))
        {
            // Remove the --server option from the args so that the builder can be created.
            args = args.Where(arg => arg.Trim('-') != PlatformCommandLineProvider.ServerOptionKey).ToArray();
        }

        return CreateBuilderAsync(args.Append($"--{PlatformCommandLineProvider.ServerOptionKey}").ToArray(), testApplicationOptions);
    }

    /// <summary>
    /// Creates a builder asynchronously.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="testApplicationOptions">The test application options.</param>
    /// <returns>The task representing the asynchronous operation.</returns>
    public static async Task<ITestApplicationBuilder> CreateBuilderAsync(string[] args, TestApplicationOptions? testApplicationOptions = null)
    {
        // We get the time to save it in the logs for testcontrollers troubleshooting.
        SystemClock systemClock = new();
        DateTimeOffset createBuilderStart = systemClock.UtcNow;
        string createBuilderEntryTime = createBuilderStart.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        testApplicationOptions ??= new TestApplicationOptions();

        SystemEnvironment systemEnvironment = new();
        LaunchAttachDebugger(systemEnvironment);

        // First step is to parse the command line from where we get the second input layer.
        // The first one should be the env vars handled autonomously by extensions and part of the test platform.
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, systemEnvironment);
        TestHostControllerInfo testHostControllerInfo = new(parseResult);
        SystemProcessHandler systemProcess = new();
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(systemEnvironment, systemProcess);

        // Create the UnhandledExceptionHandler that will be set inside the TestHostBuilder.
        LazyInitializer.EnsureInitialized(ref s_unhandledExceptionHandler, () => new UnhandledExceptionHandler(systemEnvironment, new SystemConsole(), parseResult.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey)));
        ApplicationStateGuard.Ensure(s_unhandledExceptionHandler is not null);

        // First task is to setup the logger if enabled and we take the info from the command line or env vars.
        ApplicationLoggingState loggingState = CreateFileLoggerIfDiagnosticIsEnabled(parseResult, testApplicationModuleInfo, systemClock, systemEnvironment, new SystemTask(), new SystemConsole());

        if (loggingState.FileLoggerProvider is not null)
        {
            ILogger logger = loggingState.FileLoggerProvider.CreateLogger(typeof(TestApplication).ToString());
            s_unhandledExceptionHandler.SetLogger(logger);
            await LogInformationAsync(logger, testApplicationModuleInfo, testHostControllerInfo, systemProcess, systemEnvironment, createBuilderEntryTime, loggingState.IsSynchronousWrite, loggingState.LogLevel, args);
        }

        // In VSTest mode bridge we need to ensure that we're using 1 test app per process, we cannot guarantee the correct working otherwise.
        if (loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey) &&
            Interlocked.Increment(ref s_numberOfBuilders) > MaxNumberOfBuilders &&
            !loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.SkipBuildersNumberCheckOptionKey))
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationVSTestModeTooManyBuilders);
        }

        // All checks are fine, create the TestApplication.
        return new TestApplicationBuilder(loggingState, createBuilderStart, testApplicationOptions, s_unhandledExceptionHandler);
    }

    private static async Task LogInformationAsync(
        ILogger logger,
        CurrentTestApplicationModuleInfo testApplicationModuleInfo,
        TestHostControllerInfo testHostControllerInfo,
        SystemProcessHandler processHandler,
        SystemEnvironment environment,
        string createBuilderEntryTime,
        bool syncWrite,
        LogLevel loggerLevel,
        string[] args)
    {
        // Log useful information
        AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (version is not null)
        {
            await logger.LogInformationAsync($"Version: {version.InformationalVersion}");
        }
        else
        {
            await logger.LogInformationAsync($"Version attribute not found");
        }

        await logger.LogInformationAsync("Logging mode: " + (syncWrite ? "synchronous" : "asynchronous"));
        await logger.LogInformationAsync($"Logging level: {loggerLevel}");
        await logger.LogInformationAsync($"CreateBuilderAsync entry time: {createBuilderEntryTime}");
        using IProcess currentProcess = processHandler.GetCurrentProcess();
        await logger.LogInformationAsync($"PID: {currentProcess.Id}");

#if NETCOREAPP
        string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#else
        string runtimeInformation = $"{RuntimeInformation.ProcessArchitecture} - {RuntimeInformation.FrameworkDescription}";
#endif
        await logger.LogInformationAsync($"Runtime information: {runtimeInformation}");

        SystemProcessHandler systemProcessHandler = new();
#if NETCOREAPP
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
            string? runtimeLocation = typeof(object).Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            if (runtimeLocation is not null)
            {
                await logger.LogInformationAsync($"Runtime location: {runtimeLocation}");
            }
            else
            {
                await logger.LogInformationAsync("Runtime location not found.");
            }
        }
#else
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file, this branch run only on .NET Framework
        string? runtimeLocation = typeof(object).Assembly?.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
        if (runtimeLocation is not null)
        {
            await logger.LogInformationAsync($"Runtime location: {runtimeLocation}");
        }
        else
        {
            await logger.LogInformationAsync($"Runtime location not found.");
        }
#endif

        bool isDynamicCodeSupported = false;
#if NETCOREAPP
        isDynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;
#endif
        await logger.LogInformationAsync($"IsDynamicCodeSupported: {isDynamicCodeSupported}");

        string moduleName = testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
        await logger.LogInformationAsync($"Test module: {moduleName}");
        await logger.LogInformationAsync($"Command line arguments: '{(args.Length == 0 ? string.Empty : args.Aggregate((a, b) => $"{a} {b}"))}'");

        StringBuilder machineInfo = new();
#pragma warning disable RS0030 // Do not use banned APIs
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"Machine name: {Environment.MachineName}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"OSVersion: {Environment.OSVersion}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"ProcessorCount: {Environment.ProcessorCount}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
#pragma warning restore RS0030 // Do not use banned APIs
#if NETCOREAPP
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"TotalAvailableMemoryBytes(GB): {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_000_000_000}");
#endif
        await logger.LogDebugAsync($"Machine info:\n{machineInfo}");

        if (testHostControllerInfo.HasTestHostController)
        {
            int? testHostControllerPID = testHostControllerInfo.GetTestHostControllerPID();

            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID);
            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID);
            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME);

            async Task LogVariableAsync(string key)
            {
                string? value;
                key = $"{key}_{testHostControllerPID}";
                if ((value = environment.GetEnvironmentVariable(key)) is not null)
                {
                    await logger.LogDebugAsync($"{key} '{value}'");
                }
            }
        }

        await logger.LogInformationAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT}: '{environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT)}'");
    }

    internal static void ReleaseBuilder()
        => Interlocked.Decrement(ref s_numberOfBuilders);

    /// <inheritdoc />
    public void Dispose()
        => (_testHost as IDisposable)?.Dispose();

#if NETCOREAPP
    public ValueTask DisposeAsync()
        => _testHost is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
#endif

    /// <inheritdoc />
    public async Task<int> RunAsync()
        => await _testHost.RunAsync();

    private static void LaunchAttachDebugger(SystemEnvironment environment)
    {
        if (environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER) == "1")
        {
            System.Diagnostics.Debugger.Launch();
        }
    }

    /*
     The expected order for the final logs directory is (most to least important):
     1 Environment variable
     2 Command line
     3 TA settings(json)
     4 Default(TestResults in the current working folder)
    */
    private static ApplicationLoggingState CreateFileLoggerIfDiagnosticIsEnabled(
        CommandLineParseResult result, CurrentTestApplicationModuleInfo testApplicationModuleInfo, SystemClock clock,
        SystemEnvironment environment, SystemTask task, SystemConsole console)
    {
        LogLevel logLevel = LogLevel.None;

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

        logLevel = LogLevel.Trace;

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, out string[]? verbosity))
        {
            logLevel = EnumPolyfill.Parse<LogLevel>(verbosity[0], true);
        }

        // Override the log level if the environment variable is set
        string? environmentLogLevel = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);
        if (!RoslynString.IsNullOrEmpty(environmentLogLevel))
        {
            if (!Enum.TryParse(environmentLogLevel, out LogLevel parsedLogLevel))
            {
                throw new NotSupportedException($"Invalid environment value '{nameof(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY)}', was expecting 'Trace', 'Debug', 'Information', 'Warning', 'Error', or 'Critical' but got '{environmentLogLevel}'.");
            }

            logLevel = parsedLogLevel;
        }

        // Set the directory to the default test result directory
        string directory = Path.Combine(Path.GetDirectoryName(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())!, AggregatedConfiguration.DefaultTestResultFolderName);
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
        if (!RoslynString.IsNullOrEmpty(environmentOutputDirectory))
        {
            directory = environmentOutputDirectory;
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
        if (!RoslynString.IsNullOrEmpty(environmentFilePrefix))
        {
            prefixName = environmentFilePrefix;
        }

        bool synchronousWrite = result.IsOptionSet(PlatformCommandLineProvider.DiagnosticFileLoggerSynchronousWriteOptionKey);

        // Override the synchronous write
        string? environmentSynchronousWrite = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE);
        if (!RoslynString.IsNullOrEmpty(environmentSynchronousWrite))
        {
            synchronousWrite = environmentSynchronousWrite == "1";
        }

        return new(
            logLevel,
            result,
            new(
                new FileLoggerOptions(
                    directory,
                    prefixName,
                    fileName: null,
                    synchronousWrite),
                logLevel,
                customDirectory,
                clock,
                task,
                console,
                new SystemFileSystem(),
                new SystemFileStreamFactory()),
            synchronousWrite);
    }
}
