// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

using CodeCoverageBuilderHook = Microsoft.Testing.Extensions.CodeCoverage.TestingPlatformBuilderHook;
using HangDumpBuilderHook = Microsoft.Testing.Extensions.HangDump.TestingPlatformBuilderHook;
using MSBuildBuilderHook = Microsoft.Testing.Platform.MSBuild.TestingPlatformBuilderHook;
using PackagedAppBuilderHook = Microsoft.Testing.Extensions.PackagedApp.TestingPlatformBuilderHook;
using RetryBuilderHook = Microsoft.Testing.Extensions.Retry.TestingPlatformBuilderHook;
using TrxBuilderHook = Microsoft.Testing.Extensions.TrxReport.TestingPlatformBuilderHook;

namespace Microsoft.Testing.Platform.AppModelController;

internal static class Program
{
    private const string EnabledExtensionsEnvironmentVariable = "MSTEST_APPMODEL_CONTROLLER_EXTENSIONS";

    public static async Task<int> Main(string[] args)
    {
        bool isInformationalRequest;
        try
        {
            string bootstrapFile = Path.Combine(AppContext.BaseDirectory, PackagedAppControllerArguments.BootstrapFileName);
            string[]? bootstrapArguments = File.Exists(bootstrapFile)
                ? await File.ReadAllLinesAsync(bootstrapFile).ConfigureAwait(false)
                : null;
            (args, isInformationalRequest) = PackagedAppControllerArguments.Configure(
                args, Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable, bootstrapArguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 5;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 4;
        }

        HashSet<string> enabledExtensions = GetEnabledExtensions();
        if (isInformationalRequest)
        {
            using CancellationTokenSource cancellation = new();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            try
            {
                var environment = Environment.GetEnvironmentVariables()
                    .Cast<DictionaryEntry>()
                    .ToDictionary(entry => entry.Key.ToString()!, entry => entry.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
                TestHostLaunchContext context = new(
                    Environment.GetEnvironmentVariable(PackagedAppControllerArguments.TargetEnvironmentVariable)!,
                    args,
                    environment,
                    Environment.CurrentDirectory);
                CommandLineInformationalOutput? output =
                    Environment.GetEnvironmentVariable(PackagedAppControllerArguments.NativePipeEnvironmentVariable) is null or ""
                        ? new(context.FileName, args, Console.Out)
                        : null;
                try
                {
                    if (output is not null)
                    {
                        context = new(
                            context.FileName,
                            PackagedAppControllerArguments.AddNativeTransport(context.Arguments, output.PipeName),
                            context.EnvironmentVariables,
                            context.WorkingDirectory);
                    }

                    int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
                        new PackagedAppTestHostLauncher(),
                        context,
                        enabledExtensions.Contains("packagedapp"),
                        Console.Error,
                        cancellation.Token).ConfigureAwait(false);
                    if (output is not null && exitCode == 0)
                    {
                        await output.CompleteAsync().ConfigureAwait(false);
                    }

                    return exitCode;
                }
                finally
                {
                    if (output is not null)
                    {
                        await output.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
                return 4;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args).ConfigureAwait(false);

        if (enabledExtensions.Contains("msbuild"))
        {
            MSBuildBuilderHook.AddExtensions(builder, args);
        }

        if (enabledExtensions.Contains("packagedapp"))
        {
            PackagedAppBuilderHook.AddExtensions(builder, args);
        }

        if (enabledExtensions.Contains("hangdump"))
        {
            HangDumpBuilderHook.AddExtensions(builder, args);
        }

        if (enabledExtensions.Contains("retry"))
        {
            RetryBuilderHook.AddExtensions(builder, args);
        }

        if (enabledExtensions.Contains("trx"))
        {
            TrxBuilderHook.AddExtensions(builder, args);
        }

        if (enabledExtensions.Contains("codecoverage"))
        {
            CodeCoverageBuilderHook.AddExtensions(builder, args);
        }

        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, _) => ControllerTestFramework.Instance);

        using ITestApplication application = await builder.BuildAsync().ConfigureAwait(false);
        return await application.RunAsync().ConfigureAwait(false);
    }

    private static HashSet<string> GetEnabledExtensions()
        => new(
            (Environment.GetEnvironmentVariable(EnabledExtensionsEnvironmentVariable) ?? "msbuild;packagedapp")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

    private sealed class ControllerTestFramework : ITestFramework
    {
        public static ControllerTestFramework Instance { get; } = new();

        public string Uid => nameof(ControllerTestFramework);

        public string Version => "1.0.0";

        public string DisplayName => "Windows application-model controller";

        public string Description => "Controller-only test framework for packaged Windows application test hosts.";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
            => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task ExecuteRequestAsync(ExecuteRequestContext context)
            => throw new InvalidOperationException(
                "The packaged application test host was not launched. Ensure the packaged-app launcher is enabled and the configured target has a supported application layout.");

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
            => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }
}
