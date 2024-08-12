// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

using Polly.Simmy.Fault;

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests_KnownExtensionRegistration : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSBuildTests";

    public MSBuildTests_KnownExtensionRegistration(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildVerbConfiguration))]
    public async Task Microsoft_Testing_Platform_Extensions_ShouldBe_Correctly_Registered(string tfm, BuildConfiguration compilationMode, Verb verb)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                    nameof(Microsoft_Testing_Platform_Extensions_ShouldBe_Correctly_Registered),
                    SourceCode
                    .PatchCodeWithReplace("$TargetFrameworks$", tfm)
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
                string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");
                await DotnetCli.RunAsync($"restore -r {RID} {testAsset.TargetAssetPath}{Path.DirectorySeparatorChar}MSBuildTests.csproj", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
                await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")}  -c {compilationMode} -r {RID} -nodeReuse:false -bl:{binlogFile} {testAsset.TargetAssetPath} -v:n", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

                var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, rid: RID, verb: verb, buildConfiguration: compilationMode);
                TestHostResult testHostResult = await testHost.ExecuteAsync("--help");
                testHostResult.AssertOutputContains("--crashdump");
                testHostResult.AssertOutputContains("--report-trx");
                testHostResult.AssertOutputContains("--retry-failed-tests");
                testHostResult.AssertOutputContains("--hangdump");

                SL.Build binLog = SL.Serialization.Read(binlogFile);
                SL.Target generateAutoRegisteredExtensions = binLog.FindChildrenRecursive<SL.Target>().Single(t => t.Name == "_GenerateAutoRegisteredExtensions");
                SL.Task testingPlatformAutoRegisteredExtensions = generateAutoRegisteredExtensions.FindChildrenRecursive<SL.Task>().Single(t => t.Name == "TestingPlatformAutoRegisteredExtensions");
                SL.Message generatedSource = testingPlatformAutoRegisteredExtensions.FindChildrenRecursive<SL.Message>().Single(m => m.Text.Contains("AutoRegisteredExtensions source:"));

                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.CrashDump.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.HangDump.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.HotReload.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.Retry.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.Telemetry.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
                Assert.IsTrue(generatedSource.Text.Contains("Microsoft.Testing.Extensions.TrxReport.TestingPlatformBuilderHook.AddExtensions"), generatedSource.Text);
            }, 3, TimeSpan.FromSeconds(5));

    private const string SourceCode = """
#file MSBuildTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <TestingPlatformBuilderHook Include="A" >
        <DisplayName>DummyAdapter</DisplayName>
        <TypeFullName>MyNamespaceRoot.Level1.Level2.DummyAdapterRegistration</TypeFullName>
      </TestingPlatformBuilderHook>
    </ItemGroup>

    <PropertyGroup>
        <TargetFramework>$TargetFrameworks$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>preview</LangVersion>
        <OutputType>Exe</OutputType>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
        <!-- Platform is only needed because Retry/HotReload rely on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using System.Collections.Generic;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace MyNamespaceRoot.Level1.Level2;

public static class DummyAdapterRegistration
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] args)
    {
        testApplicationBuilder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyAdapter());
    }
}

internal sealed class DummyAdapter : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyAdapter);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "DummyTest", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal sealed class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}
""";
}
