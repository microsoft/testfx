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
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Builder;

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

    public static Task<ITestApplicationBuilder> CreateServerModeBuilderAsync(string[] args, TestApplicationOptions? testApplicationOptions = null)
    {
        if (args.Contains($"--{PlatformCommandLineProvider.ServerOptionKey}") || args.Contains($"-{PlatformCommandLineProvider.ServerOptionKey}"))
        {
            // Remove the --server option from the args so that the builder can be created.
            args = args.Where(arg => arg.Trim('-') != PlatformCommandLineProvider.ServerOptionKey).ToArray();
        }

        return CreateBuilderAsync(args.Append($"--{PlatformCommandLineProvider.ServerOptionKey}").ToArray(), testApplicationOptions);
    }

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

        SystemProcessHandler systemProcess = new();
        var systemRuntime = new SystemRuntime(new SystemRuntimeFeature(), systemEnvironment, systemProcess, parseResult);

        // Create the UnhandledExceptionHandler that will be set inside the TestHostBuilder.
        LazyInitializer.EnsureInitialized(ref s_unhandledExceptionHandler, () => new UnhandledExceptionHandler(systemEnvironment, new SystemConsole(), parseResult.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey)));
        ArgumentGuard.IsNotNull(s_unhandledExceptionHandler);

        // First task is to setup the logger if enabled and we take the info from the command line or env vars.
        ApplicationLoggingState loggingState = LoggingManager.CreateFileLoggerIfDiagnosticIsEnabled(parseResult, systemRuntime, systemClock, systemEnvironment);

        if (loggingState.FileLoggerProvider is not null)
        {
            ILogger logger = loggingState.FileLoggerProvider.CreateLogger(typeof(TestApplication).ToString());
            s_unhandledExceptionHandler.SetLogger(logger);
            await LogInformationAsync(logger, systemRuntime, systemProcess, systemEnvironment, createBuilderEntryTime, loggingState.IsSynchronousWrite, loggingState.LogLevel, args);
        }

        // In VSTest mode bridge we need to ensure that we're using 1 test app per process, we cannot guarantee the correct working otherwise.
        if (loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey) &&
            Interlocked.Increment(ref s_numberOfBuilders) > MaxNumberOfBuilders &&
            !loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.SkipBuildersNumberCheckOptionKey))
        {
            throw new InvalidOperationException("More than one TestApplicationBuilder per process is not supported in VSTest mode");
        }

        // All checks are fine, create the TestApplication.
        return new TestApplicationBuilder(args, loggingState, createBuilderStart, systemRuntime, testApplicationOptions, s_unhandledExceptionHandler);
    }

    private static async Task LogInformationAsync(
        ILogger logger,
        SystemRuntime runtime,
        SystemProcessHandler processHandler,
        SystemEnvironment environment,
        string createBuilderEntryTime,
        bool syncWrite,
        LogLevel loggerLevel,
        string[] args)
    {
        // Log useful information
        var version = (AssemblyInformationalVersionAttribute?)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
        if (version is not null)
        {
            await logger.LogInformationAsync($"Version: {version.InformationalVersion} ({(syncWrite ? "synchronous log write" : "asynchronous log write")})");
        }
        else
        {
            await logger.LogInformationAsync($"Version attribute not found");
        }

        await logger.LogInformationAsync($"Logging level: {loggerLevel}");
        await logger.LogInformationAsync($"CreateBuilderAsync entry time: {createBuilderEntryTime}");
        await logger.LogInformationAsync($"PID: {processHandler.GetCurrentProcess().Id}");

#if !NETCOREAPP
        string runtimeInformation = $"{RuntimeInformation.FrameworkDescription}";
#else
        string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#endif
        await logger.LogInformationAsync($"Runtime information: {runtimeInformation}");

        SystemProcessHandler systemProcessHandler = new();
#if NETCOREAPP
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
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

        string? moduleName = runtime.GetCurrentModuleInfo().GetCurrentTestApplicationFullPath();
        moduleName = TAString.IsNullOrEmpty(moduleName)
#if !NETCOREAPP
            ? systemProcessHandler.GetCurrentProcess().MainModule.FileName
#else
            ? environment.ProcessPath
#endif
            : moduleName;
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
        await logger.LogDebugAsync($"Machine info:\n{machineInfo}");
#endif

        ITestHostControllerInfo testHostControllerInfo = runtime.GetTestHostControllerInfo();
        if (testHostControllerInfo.HasTestHostController)
        {
            string? processCorrelationId;
            if ((processCorrelationId = environment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{testHostControllerInfo.GetTestHostControllerPID()}")) is not null)
            {
                await logger.LogDebugAsync($"{$"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{testHostControllerInfo.GetTestHostControllerPID()}"} '{processCorrelationId}'");
            }

            string? parentPid;
            if ((parentPid = environment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID}_{testHostControllerInfo.GetTestHostControllerPID()}")) is not null)
            {
                await logger.LogDebugAsync($"{$"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID}_{testHostControllerInfo.GetTestHostControllerPID()}"} '{parentPid}'");
            }

            string? testHostProcessStartTime;
            if ((testHostProcessStartTime = environment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{testHostControllerInfo.GetTestHostControllerPID()}")) is not null)
            {
                await logger.LogDebugAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{testHostControllerInfo.GetTestHostControllerPID()} '{testHostProcessStartTime}'");
            }
        }

        await logger.LogInformationAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT}: '{environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT)}'");
    }

    internal static void ReleaseBuilder()
        => Interlocked.Decrement(ref s_numberOfBuilders);

    public void Dispose()
        => (_testHost as IDisposable)?.Dispose();

#if NETCOREAPP
    public ValueTask DisposeAsync()
        => _testHost is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
#endif

    public async Task<int> RunAsync()
        => await _testHost.RunAsync();

    private static void LaunchAttachDebugger(SystemEnvironment environment)
    {
        if (environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER) == "1")
        {
            System.Diagnostics.Debugger.Launch();
        }
    }
}
