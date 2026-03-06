// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class LocalizationTests : AcceptanceTestBase<LocalizationTests.TestAssetFixture>
{
    private const string AssetName = "LocalizationTests";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithFrenchLocale_OutputContainsTranslatedSummary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "fr" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // Verify the summary line is in French ("Résumé de série de tests : Réussite!")
        testHostResult.AssertOutputContains("Résumé de série de tests : Réussite!");

        // Verify the count labels are in French
        testHostResult.AssertOutputContains("total: 2");
        testHostResult.AssertOutputContains("échec: 0");
        testHostResult.AssertOutputContains("opération réussie: 2");
        testHostResult.AssertOutputContains("ignoré: 0");

        // Verify English strings are NOT in the output
        testHostResult.AssertOutputDoesNotContain("Test run summary:");
        testHostResult.AssertOutputDoesNotContain("succeeded:");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithSpanishLocale_OutputContainsTranslatedSummary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "es" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // Verify the summary line is in Spanish ("Resumen de la serie de pruebas: Correcta!")
        testHostResult.AssertOutputContains("Resumen de la serie de pruebas: Correcta!");

        // Verify the count labels are in Spanish
        testHostResult.AssertOutputContains("total: 2");
        testHostResult.AssertOutputContains("con errores: 0");
        testHostResult.AssertOutputContains("correcto: 2");
        testHostResult.AssertOutputContains("omitido: 0");

        // Verify English strings are NOT in the output
        testHostResult.AssertOutputDoesNotContain("Test run summary:");
        testHostResult.AssertOutputDoesNotContain("succeeded:");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithTestingPlatformUILanguage_TakesPrecedenceOverDotnetCLI(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "es",
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "fr",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // French should win because TESTINGPLATFORM_UI_LANGUAGE has higher precedence
        testHostResult.AssertOutputContains("Résumé de série de tests : Réussite!");
        testHostResult.AssertOutputDoesNotContain("Resumen de la serie de pruebas:");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithFailingTest_OutputContainsTranslatedFailureSummary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.FailingAssetPath, AssetName + "Failing", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "fr" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

        // Verify failure summary is in French ("Résumé de série de tests : Échec!")
        testHostResult.AssertOutputContains("Résumé de série de tests : Échec!");
        testHostResult.AssertOutputContains("échec: 1");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string TestCode = """
#file LocalizationTests.csproj
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
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,sp) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => string.Empty;
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "Test2", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        context.Complete();
    }
}
""";

        private const string FailingTestCode = """
#file LocalizationTestsFailing.csproj
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
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,sp) => new FailingTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class FailingTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(FailingTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(FailingTestFramework);
    public string Description => string.Empty;
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "FailingTest", Properties = new(new FailedTestNodeStateProperty("Something went wrong")) }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public string FailingAssetPath => GetAssetPath(AssetName + "Failing");

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

            yield return (AssetName + "Failing", AssetName + "Failing",
                FailingTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
