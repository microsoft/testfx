// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class LocalizationFailingTests : AcceptanceTestBase<LocalizationFailingTests.TestAssetFixture>
{
    private const string AssetName = "LocalizationTestsFailing";

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

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Execution_WithFailingTest_OutputContainsTranslatedFailureSummary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.FailingAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "fr" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // Verify failure summary is in French ("Résumé de série de tests : Échec!")
        AssertOutputContainsNormalized(testHostResult, "Résumé de série de tests : Échec!");
        AssertOutputContainsNormalized(testHostResult, "échec: 1");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string FailingAssetName = "LocalizationTestsFailing";

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

        public string FailingAssetPath => GetAssetPath(FailingAssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (FailingAssetName, FailingAssetName,
                FailingTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
