// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class MSBuildTests_KnownExtensionRegistration : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSBuildTests";

    [DynamicData(nameof(GetBuildMatrixTfmBuildVerbConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    [TestMethod]
    public async Task Microsoft_Testing_Platform_Extensions_ShouldBe_Correctly_Registered(string tfm, BuildConfiguration compilationMode, Verb verb)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(Microsoft_Testing_Platform_Extensions_ShouldBe_Correctly_Registered),
            SourceCode
            .PatchCodeWithReplace("$TargetFrameworks$", tfm)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        await DotnetCli.RunAsync($"restore -r {RID} {testAsset.TargetAssetPath}{Path.DirectorySeparatorChar}MSBuildTests.csproj", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"{(verb == Verb.publish ? $"publish -f {tfm}" : "build")}  -c {compilationMode} -r {RID} -nodeReuse:false {testAsset.TargetAssetPath} -v:n", AcceptanceFixture.NuGetGlobalPackagesFolder.Path);
        string binlogFile = result.BinlogPath!;

        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, rid: RID, verb: verb, buildConfiguration: compilationMode);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");
        testHostResult.AssertOutputContains("--crashdump");
        testHostResult.AssertOutputContains("--report-trx");
        testHostResult.AssertOutputContains("--retry-failed-tests");
        testHostResult.AssertOutputContains("--hangdump");

        SL.Build binLog = SL.Serialization.Read(binlogFile);
        SL.Target generateSelfRegisteredExtensions = binLog.FindChildrenRecursive<SL.Target>().Single(t => t.Name == "_GenerateSelfRegisteredExtensions");
        SL.Task testingPlatformSelfRegisteredExtensions = generateSelfRegisteredExtensions.FindChildrenRecursive<SL.Task>().Single(t => t.Name == "TestingPlatformSelfRegisteredExtensions");
        SL.Message generatedSource = testingPlatformSelfRegisteredExtensions.FindChildrenRecursive<SL.Message>().Single(m => m.Text.Contains("SelfRegisteredExtensions source:"));

        Assert.Contains("Microsoft.Testing.Extensions.CrashDump.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
        Assert.Contains("Microsoft.Testing.Extensions.HangDump.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
        Assert.Contains("Microsoft.Testing.Extensions.HotReload.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
        Assert.Contains("Microsoft.Testing.Extensions.Retry.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
        Assert.Contains("Microsoft.Testing.Extensions.Telemetry.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
        Assert.Contains("Microsoft.Testing.Extensions.TrxReport.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
    }

    private const string SourceCode = """
#file MSBuildTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <TestingPlatformBuilderHook Include="A" >
        <DisplayName>DummyTestFramework</DisplayName>
        <TypeFullName>MyNamespaceRoot.Level1.Level2.DummyTestFrameworkRegistration</TypeFullName>
      </TestingPlatformBuilderHook>
    </ItemGroup>

    <PropertyGroup>
        <TargetFramework>$TargetFrameworks$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>preview</LangVersion>
        <OutputType>Exe</OutputType>
        <!-- Do not warn about package downgrade. NuGet uses alphabetical sort as ordering so -dev or -ci are considered downgrades of -preview. -->
        <NoWarn>$(NoWarn);NETSDK1201</NoWarn>

        <RootNamespace>(MSBuild Tests)</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using MSBuildTests;
using System.Collections.Generic;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace MyNamespaceRoot.Level1.Level2;

public static class DummyTestFrameworkRegistration
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] args)
    {
        testApplicationBuilder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
    }
}

internal sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

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

#file DummyClass.cs

namespace MSBuildTests.Microsoft;

// This serves as a test to ensure that we don't regress generation of 'global::'
// If the self registered generation used Microsoft.Testing.Extensions.ExtensionName.TestingPlatformBuilderHook.AddExtension,
// Then, without global::, Microsoft will be referring to MSBuildTests.Microsoft namespace and will fail to compile
public static class DummyClass { }
""";
}
