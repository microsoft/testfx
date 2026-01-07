// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ForwardCompatibilityTests : AcceptanceTestBase<ForwardCompatibilityTests.TestAssetFixture>
{
    private const string AssetName = "ForwardCompatibilityTest";

    [TestMethod]
    public async Task NewerPlatform_WithPreviousExtensions_ShouldExecuteTests()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--crashdump --hangdump --report-trx --retry-failed-tests 3", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);

        string testResultsPath = Path.Combine(testHost.DirectoryName, "TestResults");
        Assert.IsTrue(Directory.Exists(testResultsPath), $"TestResults directory should exist at: {testResultsPath}");

        string[] trxFiles = Directory.GetFiles(testResultsPath, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.IsNotEmpty(trxFiles, $"At least one TRX file should be generated in: {testResultsPath}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string PreviousExtensionVersion = "2.0.0";

        private const string ForwardCompatibilityTestCode = """
#file ForwardCompatibilityTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <!-- Use the locally built (newer) version of the platform -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />

        <!-- Use previous version of all extensions to test forward compatibility -->
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$PreviousExtensionVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
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
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_,__) => new DummyTestFramework());

        // Add all extensions to ensure forward compatibility
        builder.AddCrashDumpProvider();
        builder.AddHangDumpProvider();
        builder.AddHotReloadProvider();
        builder.AddRetryProvider();
        builder.AddAppInsightsTelemetryProvider();
        builder.AddTrxReportProvider();

        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
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
            new TestNode() { Uid = "0", DisplayName = "ForwardCompatibilityTest", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                ForwardCompatibilityTestCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$PreviousExtensionVersion$", PreviousExtensionVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
