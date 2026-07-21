// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class PlaywrightSdkTests : AcceptanceTestBase<PlaywrightSdkTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task EnablePlaywrightProperty_WhenUsingRunner_AllowsToRunPlaywrightTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.PlaywrightProjectPath, TestAssetFixture.PlaywrightProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // Depending on the machine, the test might fail due to the browser not being installed.
        // To avoid slowing down the tests, we will not run the installation so depending on machines we have different results.
        switch (testHostResult.ExitCode)
        {
            case 0:
                testHostResult.AssertOutputContainsSummary(0, 1, 0);
                break;

            case 2:
                testHostResult.AssertOutputContains("Microsoft.Playwright.PlaywrightException: Executable doesn't exist");
                break;

            default:
                Assert.Fail("Unexpected exit code");
                break;
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task EnablePlaywrightProperty_WhenUsingVSTest_AllowsToRunPlaywrightTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.PlaywrightProjectPath, TestAssetFixture.PlaywrightProjectName, tfm);
        string testApplicationSource = GetTestApplicationSourcePath(testHost);

        VSTestConsoleResult result = await VSTestConsoleLocator.RunAsync(
            $"\"{testApplicationSource}\"",
            workingDirectory: AssetFixture.PlaywrightProjectPath,
            cancellationToken: TestContext.CancellationToken);

        Assert.Contains("VSTest version", result.StandardOutput);

        // Depending on the machine, the test might fail due to the browser not being installed.
        // To avoid slowing down the tests, we will not run the installation so depending on machines we have different results.
        switch (result.ExitCode)
        {
            case 0:
                result.AssertTestRunSummary(0, 1, 0, 1);
                break;

            case 1:
                Assert.Contains("Microsoft.Playwright.PlaywrightException: Executable doesn't exist", result.StandardOutput);
                result.AssertTestRunSummary(1, 0, 0, 1);
                break;

            default:
                Assert.Fail("Unexpected exit code");
                break;
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string PlaywrightProjectName = "PlaywrightProject";

        private const string PlaywrightSourceCode = """
#file PlaywrightProject.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Disable all extensions by default -->
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnablePlaywright>true</EnablePlaywright>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Text.RegularExpressions" />
    <Using Include="System.Threading.Tasks" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNETTestSdkVersion)" />
    <PackageDownload Include="Microsoft.TestPlatform" Version="[$(MicrosoftNETTestSdkVersion)]" />
    <PackageDownload Include="Microsoft.TestPlatform.CLI" Version="[$(MicrosoftNETTestSdkVersion)]" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
namespace PlaywrightProject;

[TestClass]
public class UnitTest1 : PageTest
{
    [TestMethod]
    public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingToTheIntroPage()
    {
        await Page.GotoAsync("https://playwright.dev");

        // Expect a title "to contain" a substring.
        await Expect(Page).ToHaveTitleAsync(new Regex("Playwright"));

        // create a locator
        var getStarted = Page.Locator("text=Get Started");

        // Expect an attribute "to be strictly equal" to the value.
        await Expect(getStarted).ToHaveAttributeAsync("href", "/docs/intro");

        // Click the get started link.
        await getStarted.ClickAsync();

        // Expects the URL to contain intro.
        await Expect(Page).ToHaveURLAsync(new Regex(".*intro"));
    }
}

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}
""";

        public string PlaywrightProjectPath => GetAssetPath(PlaywrightProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (PlaywrightProjectName, PlaywrightProjectName,
                PlaywrightSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }
}
