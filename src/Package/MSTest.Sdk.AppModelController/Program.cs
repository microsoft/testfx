// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

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
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args).ConfigureAwait(false);

        HashSet<string> enabledExtensions = GetEnabledExtensions();
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
        {
            context.Complete();
            return Task.CompletedTask;
        }

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
            => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }
}
