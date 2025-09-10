// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TimeoutTests : AcceptanceTestBase<TimeoutTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithInvalidArg_WithoutLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithInvalidArg_WithInvalidLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5y", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithInvalidArg_WithInvalidFormat_OutputInvalidMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5h6m", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithValidArg_WithTestTimeOut_OutputContainsCancelingMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1s", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertOutputContains("Canceling the test session");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithValidArg_WithSecondAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 12.5s", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain("Canceling the test session");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithValidArg_WithMinuteAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1m", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain("Canceling the test session");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TimeoutWithValidArg_WithHourAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1h", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain("Canceling the test session");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AssetName = "TimeoutTest";

        private const string TestCode = """
#file TimeoutTest.csproj
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
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
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
       Thread.Sleep(10000);
       context.Complete();
       return Task.CompletedTask;
    }
}
""";

        public string NoExtensionTargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
