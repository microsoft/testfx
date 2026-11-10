// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Compatibility coverage for the oldest telemetry extension supported on legacy targets after
/// <see href="https://github.com/microsoft/testfx/issues/12314"/> retires the platform's polyfill exports.
///
/// <para>
/// Telemetry 2.0.0's <c>netstandard2.0</c> asset references the platform's old
/// <c>Polyfills.Polyfill.Deconstruct</c> export and is intentionally no longer supported.
/// Telemetry 2.1.0 embeds its own helpers and remains supported on .NET Framework.
/// Keep telemetry enabled to exercise the background ingest loop rather than only registration.
/// </para>
///
/// <para>
/// Two conditions are required to exercise the original legacy-target failure path from
/// <see href="https://github.com/microsoft/testfx/issues/9710"/>, and both were missing from the original
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

    // The .NET Framework host resolves the netstandard2.0 telemetry asset and only exists on Windows.
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
        // The first stable release without a dependency on the retired platform polyfill exports.
        private const string OldestSupportedExtensionVersion = "2.1.0";

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

        <!-- Keep the oldest telemetry release whose netstandard2.0 asset does not require the
             platform polyfill exports retired in MTP 3. -->
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
