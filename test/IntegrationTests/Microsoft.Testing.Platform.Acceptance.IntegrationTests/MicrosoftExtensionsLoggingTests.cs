// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MicrosoftExtensionsLoggingTests : AcceptanceTestBase<MicrosoftExtensionsLoggingTests.TestAssetFixture>
{
    private const string AssetName = "MicrosoftExtensionsLoggingTest";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task MELBridge_WhenEnabled_ForwardsPlatformDiagnosticLogsToMELProvider(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic --diagnostic-verbosity Trace",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        // The test asset registers a custom Microsoft.Extensions.Logging provider that prefixes every
        // log line with "[MEL]" and writes it to stdout. If the bridge is wired correctly, MTP's own
        // diagnostic messages (which start being emitted very early in startup) must appear with that
        // prefix on stdout, in addition to being written to the --diagnostic file.
        testHostResult.AssertOutputContains("[MEL]");
        testHostResult.AssertOutputContains("Microsoft.Testing.Platform");

        // The provider writes "[MEL-FLUSH]" to stdout from its Dispose method. Asserting on it locks in
        // the fact that the bridge-owned LoggerFactory is disposed at shutdown — without that, buffered
        // MEL providers (Serilog, Application Insights, OpenTelemetry, ...) would never flush.
        testHostResult.AssertOutputContains("[MEL-FLUSH]");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task MELBridge_WhenDiagnosticIsOff_ConfigureDelegateIsNotInvokedAndNoLogsAreForwarded(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        // Without --diagnostic, MTP's log level defaults to None. The bridge short-circuits and never
        // invokes the user's configure delegate, so no MEL pipeline is constructed and no logs are forwarded.
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
        testHostResult.AssertOutputDoesNotContain("[MEL]");
        testHostResult.AssertOutputDoesNotContain("[MEL-FLUSH]");
        testHostResult.AssertOutputDoesNotContain("[MEL-CONFIGURE-INVOKED]");
    }

    public TestContext TestContext { get; set; } = null!;

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file MicrosoftExtensionsLoggingTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <NoWarn>$(NoWarn);TPEXP</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Logging" Version="$MicrosoftTestingExtensionsLoggingVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());

        // Forward MTP diagnostic logs to a custom MEL provider that prefixes every message
        // with "[MEL]" so the acceptance test can assert on stdout that the bridge is wired up.
        // Register the provider as a type so the MEL ServiceProvider owns its lifetime and disposes
        // it on shutdown (this is how Console/Debug/Serilog providers are registered, and matches
        // real-world consumer usage).
        // The marker "[MEL-CONFIGURE-INVOKED]" lets the no-diagnostic test assert that this delegate
        // is *not* invoked when MTP's log level resolves to None.
        builder.AddMicrosoftExtensionsLogging(logging =>
        {
            Console.WriteLine("[MEL-CONFIGURE-INVOKED]");
            logging.Services.AddSingleton<ILoggerProvider, MarkerLoggerProvider>();
        });

        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public sealed class MarkerLoggerProvider : ILoggerProvider
{
    private bool _disposed;

    public ILogger CreateLogger(string categoryName) => new MarkerLogger(categoryName);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Marker used by the acceptance test to verify the bridge-owned LoggerFactory is disposed
        // at shutdown so buffered MEL providers get a chance to flush.
        Console.WriteLine("[MEL-FLUSH]");
    }
}

public sealed class MarkerLogger(string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Print to stdout with the [MEL] marker the acceptance test will look for.
        Console.WriteLine($"[MEL] {logLevel} {categoryName}: {formatter(state, exception)}");
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        context.Complete();
        return Task.CompletedTask;
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsLoggingVersion$", MicrosoftTestingExtensionsLoggingVersion));
    }
}
