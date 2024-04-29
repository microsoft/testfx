// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class IgnoreExitCodeTests : AcceptanceTestBase
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
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestAdapter : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

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

    private readonly AcceptanceFixture _acceptanceFixture;

    public IgnoreExitCodeTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    public static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, string CommandLine, string EnvironmentVariable)>> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)> buildConfig in GetBuildMatrixTfmBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string)>((buildConfig.Arguments.Tfm, buildConfig.Arguments.BuildConfiguration, "--ignore-exit-code 2", string.Empty), $"{buildConfig.Arguments.Tfm},{buildConfig.Arguments.BuildConfiguration},CommandLine");
            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string)>((buildConfig.Arguments.Tfm, buildConfig.Arguments.BuildConfiguration, string.Empty, "2"), $"{buildConfig.Arguments.Tfm},{buildConfig.Arguments.BuildConfiguration},EnvironmentVariable");
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix))]
    public async Task If_IgnoreExitCode_Specified_Should_Return_Success_ExitCode(string tfm, BuildConfiguration buildConfiguration, string commandLine, string environmentVariable)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
        AssetName,
        SourceCode
        .PatchCodeWithReplace("$TargetFramework$", tfm)
        .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        compilationResult = await DotnetCli.RunAsync(
            $"build -m:1 -nodeReuse:false {generator.TargetAssetPath} -c {buildConfiguration} -r {RID}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testHost = TestInfrastructure.TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            command: commandLine,
            environmentVariables: environmentVariable is null ? null : new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE, environmentVariable },
            });
        testHostResult.AssertOutputContains("Failed! - Failed: 1, Passed: 0, Skipped: 0, Total: 1");
        Assert.AreEqual(0, testHostResult.ExitCode);
    }
}
