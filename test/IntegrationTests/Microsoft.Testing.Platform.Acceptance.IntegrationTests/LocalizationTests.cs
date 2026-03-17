// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class LocalizationTests : AcceptanceTestBase<LocalizationTests.TestAssetFixture>
{
    private const string AssetName = "LocalizationTests";

    // Localized resource strings may use different Unicode normalization forms (NFC vs NFD)
    // than C# string literals. Normalizing both sides to FormC avoids false mismatches
    // with the ordinal comparison used by AssertOutputContains.
    // French locale also uses non-breaking space (U+00A0) before colons per typographic convention,
    // so we normalize NBSP to regular space for comparison.
    private static string NormalizeForComparison(string text)
        => text.Normalize(NormalizationForm.FormC).Replace('\u00A0', ' ');

    private static void AssertOutputContainsNormalized(TestHostResult testHostResult, string value)
    {
        string normalizedOutput = NormalizeForComparison(testHostResult.StandardOutput);
        string normalizedValue = NormalizeForComparison(value);
        Assert.IsTrue(
            normalizedOutput.Contains(normalizedValue, StringComparison.Ordinal),
            $"Output does not contain '{value}'.{Environment.NewLine}Output:{Environment.NewLine}{testHostResult.StandardOutput}");
    }

    private static void AssertOutputDoesNotContainNormalized(TestHostResult testHostResult, string value)
    {
        string normalizedOutput = NormalizeForComparison(testHostResult.StandardOutput);
        string normalizedValue = NormalizeForComparison(value);
        Assert.IsFalse(
            normalizedOutput.Contains(normalizedValue, StringComparison.Ordinal),
            $"Output should not contain '{value}'.{Environment.NewLine}Output:{Environment.NewLine}{testHostResult.StandardOutput}");
    }

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
        AssertOutputContainsNormalized(testHostResult, "Résumé de série de tests : Réussite!");

        // Verify the count labels are in French
        AssertOutputContainsNormalized(testHostResult, "total: 2");
        AssertOutputContainsNormalized(testHostResult, "échec: 0");
        AssertOutputContainsNormalized(testHostResult, "opération réussie: 2");
        AssertOutputContainsNormalized(testHostResult, "ignoré: 0");

        // Verify English strings are NOT in the output
        AssertOutputDoesNotContainNormalized(testHostResult, "Test run summary:");
        AssertOutputDoesNotContainNormalized(testHostResult, "succeeded:");
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
        AssertOutputContainsNormalized(testHostResult, "Résumé de série de tests : Réussite!");
        AssertOutputDoesNotContainNormalized(testHostResult, "Resumen de la serie de pruebas:");
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
