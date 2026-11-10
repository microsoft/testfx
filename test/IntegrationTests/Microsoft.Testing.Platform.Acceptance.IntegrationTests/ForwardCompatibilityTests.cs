// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ForwardCompatibilityTests : AcceptanceTestBase<ForwardCompatibilityTests.TestAssetFixture>
{
    private const string AssetName = "ForwardCompatibilityTest";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task NewerPlatform_WithPreviousExtensions_ShouldExecuteTests(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string arguments = "--hangdump --report-trx --retry-failed-tests 3";
        if (!TargetFrameworks.NetFramework.Contains(tfm))
        {
            arguments += " --crashdump";
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync(arguments, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
        testHostResult.AssertOutputContains($"PlatformAssemblyPath: {Path.Join(testHost.DirectoryName, "Microsoft.Testing.Platform.dll")}");

        string testResultsPath = Path.Combine(testHost.DirectoryName, "TestResults");
        Assert.IsTrue(Directory.Exists(testResultsPath), $"TestResults directory should exist at: {testResultsPath}");

        string[] trxFiles = Directory.GetFiles(testResultsPath, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.HasCount(1, trxFiles);
        XNamespace trxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement result = XDocument.Load(trxFiles[0]).Descendants(trxNamespace + "UnitTestResult").Single();
        Assert.AreEqual("ForwardCompatibilityTest", result.Attribute("testName")?.Value);
        Assert.AreEqual("Passed", result.Attribute("outcome")?.Value);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string PreviousExtensionVersion = "2.2.1";
        // 2.2.1 still calls the retired parameterless IProcess.WaitForExitAsync.
        private const string PreviousRetryExtensionVersion = "2.3.0";
        private const string PreviousMSBuildExtensionVersion = "2.3.0";

        private const string ForwardCompatibilityTestCode = """
#file ForwardCompatibilityTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <!-- We provide our own Main, so disable the SDK-generated entry point. -->
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    </PropertyGroup>

    <ItemGroup>
        <!-- Use the locally built (newer) version of the platform -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />

        <!-- Use previous version of all extensions to test forward compatibility -->
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$PreviousRetryExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$PreviousExtensionVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$PreviousExtensionVersion$" />
        <!--
            Microsoft.Testing.Platform.MSBuild ships the auto-loaded MSBuildConsumer extension.
            Use 2.3.0, which no longer depends on the legacy execution-time message
            removed from the platform in the next major release.
        -->
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$PreviousMSBuildExtensionVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.MSBuild;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"PlatformAssemblyPath: {typeof(ITestApplicationBuilder).Assembly.Location}");
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
        // Register MSBuild even without an active MSBuild connection to exercise extension loading.
        builder.AddMSBuild();

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
            new TestNode() { Uid = "0", DisplayName = "ForwardCompatibilityTest", Properties = new(PassedTestNodeStateProperty.CachedInstance, new TrxFullyQualifiedTypeNameProperty("MyNS.MyTestClass")) }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                ForwardCompatibilityTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$PreviousExtensionVersion$", PreviousExtensionVersion)
                .PatchCodeWithReplace("$PreviousRetryExtensionVersion$", PreviousRetryExtensionVersion)
                .PatchCodeWithReplace("$PreviousMSBuildExtensionVersion$", PreviousMSBuildExtensionVersion));
    }

    public TestContext TestContext { get; set; }
}
