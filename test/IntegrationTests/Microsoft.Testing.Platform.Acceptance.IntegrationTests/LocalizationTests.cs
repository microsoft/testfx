// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

// Temporarily disabled: OneLocBuild keeps reverting the TerminalResources.*.xlf targets to English,
// which makes these tests fail on every loc check-in. Re-enable once proper translations flow. See https://github.com/microsoft/testfx/issues/9295.
[Ignore("Disabled until OneLocBuild ships proper TerminalResources translations. See https://github.com/microsoft/testfx/issues/9295.")]
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

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Verify the summary line is in French ("Résumé de série de tests : Réussite!")
        testHostResult.AssertOutputContainsNormalized("Résumé de série de tests : Réussite!");

        // Verify the count labels are in French
        testHostResult.AssertOutputContainsNormalized("total: 2");
        testHostResult.AssertOutputContainsNormalized("échec: 0");
        testHostResult.AssertOutputContainsNormalized("opération réussie: 2");
        testHostResult.AssertOutputContainsNormalized("ignoré: 0");

        // Verify English strings are NOT in the output
        testHostResult.AssertOutputDoesNotContainNormalized("Test run summary:");
        testHostResult.AssertOutputDoesNotContainNormalized("succeeded:");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithSpanishLocale_OutputContainsTranslatedSummary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "es" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

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

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // French should win because TESTINGPLATFORM_UI_LANGUAGE has higher precedence
        testHostResult.AssertOutputContainsNormalized("Résumé de série de tests : Réussite!");
        testHostResult.AssertOutputDoesNotContainNormalized("Resumen de la serie de pruebas:");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
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
using System.Text;
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
        Console.OutputEncoding = Encoding.UTF8;
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

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
