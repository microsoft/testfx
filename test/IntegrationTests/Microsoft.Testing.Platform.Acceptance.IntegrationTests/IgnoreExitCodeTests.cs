// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class IgnoreExitCodeTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "TestProject";

    private const string SourceCode = """
#file TestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file Program.cs
using System;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
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

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new FailedTestNodeStateProperty()),
        }));

        context.Complete();
    }
}
""";

    public static IEnumerable<(string Tfm, BuildConfiguration BuildConfiguration, string CommandLine, string EnvironmentVariable)> GetBuildMatrix()
    {
        foreach ((string Tfm, BuildConfiguration BuildConfiguration) buildConfig in GetBuildMatrixTfmBuildConfiguration())
        {
            yield return new(buildConfig.Tfm, buildConfig.BuildConfiguration, "--ignore-exit-code 2", string.Empty);
            yield return new(buildConfig.Tfm, buildConfig.BuildConfiguration, string.Empty, "2");
        }
    }

    [DynamicData(nameof(GetBuildMatrix))]
    [TestMethod]
    public async Task If_IgnoreExitCode_Specified_Should_Return_Success_ExitCode(string tfm, BuildConfiguration buildConfiguration, string commandLine, string environmentVariable)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", tfm)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        string assetPath = generator.TargetAssetPath;
        string globalPackagesPath = AcceptanceFixture.NuGetGlobalPackagesFolder.Path;
        await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {assetPath} -r {RID}", globalPackagesPath, cancellationToken: TestContext.CancellationToken);
        await DotnetCli.RunAsync($"build -m:1 -nodeReuse:false {assetPath} -c {buildConfiguration} -r {RID}", globalPackagesPath, cancellationToken: TestContext.CancellationToken);
        var host = TestInfrastructure.TestHost.LocateFrom(assetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
        TestHostResult hostResult = await host.ExecuteAsync(
            command: commandLine,
            environmentVariables: new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE, environmentVariable },
            },
            cancellationToken: TestContext.CancellationToken);
        hostResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
        Assert.AreEqual(0, hostResult.ExitCode);
    }

    public TestContext TestContext { get; set; }
}
