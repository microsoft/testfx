// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Regression coverage for <see href="https://github.com/microsoft/testfx/issues/9710"/>.
///
/// <para>
/// A newer <c>Microsoft.Testing.Platform</c> combined with the OLDEST supported 2.x
/// <c>Microsoft.Testing.Extensions.Telemetry</c> must still load and run. The old telemetry
/// extension's <c>netstandard2.0</c> asset was compiled against the platform's internal
/// <c>Polyfills.Polyfill</c> helper (shared via <c>InternalsVisibleTo</c> before polyfills became
/// per-assembly embedded types), so its IL carries a reference to
/// <c>[Microsoft.Testing.Platform]Polyfills.Polyfill.Deconstruct(KeyValuePair&lt;,&gt;, out, out)</c>.
/// When that member is missing from the newer platform, <c>AppInsightsProvider.IngestLoopAsync</c>
/// throws <c>MissingMethodException</c> on its background thread and crashes the process.
/// </para>
///
/// <para>
/// Two conditions are required to surface the crash, and both were missing from
/// <see cref="ForwardCompatibilityTests"/>:
/// <list type="number">
/// <item>The <b>.NET Framework</b> host (<c>net462</c>) must be used so NuGet resolves the telemetry
/// extension's <c>netstandard2.0</c> asset (the .NET assets bind <c>Deconstruct</c> to the BCL and are
/// not affected). The original report crashed on <c>net472</c> while <c>net8.0</c> passed.</item>
/// <item>Telemetry must be <b>enabled</b> so <c>TelemetryManager</c> actually constructs
/// <c>AppInsightsProvider</c> and runs its ingest loop. When opted out (the acceptance default),
/// a <c>NopTelemetryService</c> is used and the crashing method never runs.</item>
/// </list>
/// </para>
/// </summary>
[TestClass]
public class TelemetryForwardCompatibilityTests : AcceptanceTestBase<TelemetryForwardCompatibilityTests.TestAssetFixture>
{
    private const string AssetName = "TelemetryForwardCompatibilityTest";

    // The crash is specific to the netstandard2.0 telemetry asset that ships with old 2.x extensions,
    // which is only resolved on the .NET Framework host. That host only exists on Windows.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NewerPlatform_WithOldestSupportedTelemetryExtension_TelemetryEnabled_OnNetFramework_ShouldNotCrash()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetFramework[0]);

        // disableTelemetry: false is essential - it is what forces AppInsightsProvider (and therefore the
        // old extension's IngestLoopAsync, where #9710 crashes) to actually be constructed and run.
        TestHostResult testHostResult = await testHost.ExecuteAsync(disableTelemetry: false, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // Guard explicitly against the #9710 failure mode so a regression produces an actionable message
        // rather than an opaque native crash exit code.
        Assert.IsFalse(
            testHostResult.StandardOutput.Contains("MissingMethodException", StringComparison.Ordinal),
            $"The test host crashed with a MissingMethodException, which is the #9710 regression.{Environment.NewLine}{testHostResult}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        // Oldest supported 2.x extension release. Its netstandard2.0 telemetry asset references the
        // platform's internal Polyfills.Polyfill helper, which is what makes it a forward-compat canary.
        private const string OldestSupportedExtensionVersion = "2.0.0";

        private const string TestCode = """
#file TelemetryForwardCompatibilityTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <PlatformTarget>x64</PlatformTarget>
        <LangVersion>preview</LangVersion>
        <!-- We provide our own Main, so disable the SDK-generated entry point. -->
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    </PropertyGroup>

    <ItemGroup>
        <!-- Use the locally built (newer) version of the platform. -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />

        <!-- Use the oldest supported 2.x telemetry extension to test forward compatibility. On .NET
             Framework this resolves the netstandard2.0 asset that references the platform's internal
             Polyfills.Polyfill helper (see #9710). -->
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$OldestSupportedExtensionVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());

        // Registers the AppInsightsProvider from the OLD telemetry extension. Combined with telemetry
        // being enabled at runtime, this is what exercises the ingest loop that crashes in #9710.
        builder.AddAppInsightsTelemetryProvider();

        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "TelemetryForwardCompatibilityTest", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        // This asset references only the platform and telemetry, and provides its own entry point, so
        // there is nothing for the MSTest source generator to process.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                // Multi-target so the net462 asset (where #9710 reproduces) exists on Windows while the
                // asset still builds on non-Windows (where TargetFrameworks.All omits .NET Framework).
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$OldestSupportedExtensionVersion$", OldestSupportedExtensionVersion));
    }

    public TestContext TestContext { get; set; }
}
