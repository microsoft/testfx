// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

public sealed partial class SdkTests
{
    private const string CompositionCentralPackageManagementSource = """

        #file Directory.Packages.props
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
            <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
          </PropertyGroup>
        </Project>
        """;

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Default", "MSTest.TestAdapter", false)]
    [DataRow("Default", "MSTest.TestAdapter", true)]
    [DataRow("Uwp", "MSTest.Uwp", false)]
    [DataRow("Uwp", "MSTest.Uwp", true)]
    [DataRow("WinUI", "MSTest.WinUI", false)]
    [DataRow("WinUI", "MSTest.WinUI", true)]
    public async Task MSTestSdk_ExplicitApplicationModel_SelectsCompositionBeforeLateToolingImports(
        string applicationModel, string expectedAdapterPackageId, bool useCentralPackageManagement)
    {
        string lateProperties = applicationModel switch
        {
            "Uwp" => "<UseUwpTools>true</UseUwpTools>",
            "WinUI" => "<UseWinUI>true</UseWinUI>",
            _ => "<UseUwpTools>false</UseUwpTools>",
        };
        string earlyProperties = applicationModel == "Default" ? "<UseUwp>true</UseUwp>" : string.Empty;
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            $"Explicit{applicationModel}Sdk",
            $"""
            <MSTestApplicationModel>{applicationModel}</MSTestApplicationModel>
            <WindowsPackageType>None</WindowsPackageType>
            {earlyProperties}
            """,
            expectedAdapterPackageId: expectedAdapterPackageId,
            useCentralPackageManagement: useCentralPackageManagement,
            lateApplicationModelProperties: lateProperties);

        result.AssertOutputContains($";Model={applicationModel};AdapterPackage={expectedAdapterPackageId};EndComposition");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("MSTest.TestAdapter", "MSTest.Uwp")]
    [DataRow("MSTest.TestAdapter", "MSTest.WinUI")]
    [DataRow("MSTest.Uwp", "MSTest.WinUI")]
    public async Task ExplicitPackages_RejectConflictingAdaptersBeforeCompilation(string firstPackage, string secondPackage)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            "ConflictingMSTestPackages",
            $$"""
            #file ConflictingMSTestPackages.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0-windows10.0.26100.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{firstPackage}}" Version="{{MSTestVersion}}" />
                <PackageReference Include="{{secondPackage}}" Version="{{MSTestVersion}}" />
              </ItemGroup>
            </Project>
            """);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            "msbuild -restore -t:PrepareForBuild -bl:conflicting-adapters.binlog",
            workingDirectory: asset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(1);
        result.AssertOutputContains("Reference exactly one MSTest adapter composition: MSTest.TestAdapter, MSTest.Uwp, or MSTest.WinUI.");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP requires desktop MSBuild and the UWP workload.")]
    [MemberCondition(
        typeof(AcceptanceTestBase),
        nameof(AcceptanceTestBase.IsWindowsApplicationModelTestEnvironment),
        IgnoreMessage = "Requires the dedicated preflight-gated Windows application-model test environment.")]
    public async Task MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition()
    {
        const string Source = """
            #file ClassicUwp.csproj
            <Project>
              <Import Project="Sdk.props" Sdk="MSBuild.Sdk.Extras" Version="$MSBuildSdkExtrasVersion$" />
              <Import Project="Sdk.props" Sdk="MSTest.Sdk" Version="$MSTestVersion$" />
              <PropertyGroup>
                <TargetFramework>uap10.0.16299</TargetFramework>
                <TargetPlatformVersion>10.0.16299.0</TargetPlatformVersion>
                <TargetPlatformMinVersion>10.0.16299.0</TargetPlatformMinVersion>
                <OutputType>AppContainerExe</OutputType>
                <Platforms>x64</Platforms>
                <PlatformTarget>x64</PlatformTarget>
                <UseDotNetNativeToolchain>false</UseDotNetNativeToolchain>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
                <EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NETCore.UniversalWindowsPlatform" Version="6.2.14" />
              </ItemGroup>
              <Import Project="Sdk.targets" Sdk="MSBuild.Sdk.Extras" Version="$MSBuildSdkExtrasVersion$" />
              <Import Project="Sdk.targets" Sdk="MSTest.Sdk" Version="$MSTestVersion$" />
              <Target Name="PrintClassicComposition" DependsOnTargets="PrepareForBuild;ResolveReferences">
                <Message Importance="high"
                         Text="ClassicComposition:Model=$(_MSTestApplicationModel);AdapterPackage=$(_MSTestAdapterPackageId);EndComposition" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-mstest-assets.txt"
                                  Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
              </Target>
            </Project>
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            "ClassicUwpCompositionSdk",
            Source
                .PatchCodeWithReplace("$MSBuildSdkExtrasVersion$", MSBuildSdkExtrasVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        VisualStudioTestTools tools = await WindowsApplicationModelTestTools.LocateClassicUwpVisualStudioToolsAsync(TestContext.CancellationToken);
        string projectPath = Path.Combine(testAsset.TargetAssetPath, "ClassicUwp.csproj");
        BoundedCommandLineResult result = await RunWindowsApplicationModelCommandAsync(
            $"\"{tools.MSBuildPath}\" \"{projectPath}\" /restore /t:PrintClassicComposition /p:Configuration=Release /p:Platform=x64 /bl:\"{Path.Combine(testAsset.TargetAssetPath, "classic-uwp.binlog")}\"",
            testAsset.TargetAssetPath,
            TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, $"{result.StandardOutput}{Environment.NewLine}{result.ErrorOutput}");
        Assert.Contains("ClassicComposition:Model=Uwp;AdapterPackage=MSTest.Uwp;EndComposition", result.StandardOutput);
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, "uap10.0.16299", "MSTest.Uwp");
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, "MSTest.Uwp");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Default", "MSTest.TestAdapter")]
    [DataRow("Uwp", "MSTest.Uwp")]
    [DataRow("WinUI", "MSTest.WinUI")]
    public async Task MSTestSdk_UwpToolingOptOut_RespectsExplicitApplicationModel(string applicationModel, string expectedAdapterPackageId)
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            $"UwpToolingOptOut{applicationModel}Sdk",
            $"""
            <MSTestApplicationModel>{applicationModel}</MSTestApplicationModel>
            <UseUwp>true</UseUwp>
            <UseUwpTools>false</UseUwpTools>
            <WindowsPackageType>None</WindowsPackageType>
            """,
            expectedAdapterPackageId: expectedAdapterPackageId);

        result.AssertOutputContains($";Model={applicationModel};AdapterPackage={expectedAdapterPackageId};EndComposition");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Default", "NativeAOT", "MSTest.TestAdapter")]
    [DataRow("Uwp", "NativeAOT", "MSTest.Uwp")]
    [DataRow("WinUI", "NativeAOT", "MSTest.WinUI")]
    [DataRow("Uwp", "VSTest", "MSTest.Uwp")]
    [DataRow("WinUI", "VSTest", "MSTest.WinUI")]
    public async Task MSTestSdk_AdapterInjection_UsesSelectedCompositionWithCentralPackageManagement(
        string applicationModel, string runner, string expectedAdapterPackageId)
    {
        string runnerProperties = runner == "VSTest"
            ? "<UseVSTest>true</UseVSTest>"
            : "<PublishAot>true</PublishAot><RuntimeIdentifier>win-x64</RuntimeIdentifier><TestingExtensionsProfile>None</TestingExtensionsProfile>";
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            $"{applicationModel}{runner}CompositionSdk",
            $"""
            <MSTestApplicationModel>{applicationModel}</MSTestApplicationModel>
            <WindowsPackageType>None</WindowsPackageType>
            {runnerProperties}
            """,
            expectedAdapterPackageId: expectedAdapterPackageId,
            useCentralPackageManagement: true);

        result.AssertOutputContains($";Model={applicationModel};AdapterPackage={expectedAdapterPackageId};EndComposition");
        if (runner == "NativeAOT")
        {
            result.AssertOutputContains("MSTest.SourceGeneration");
        }
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Desktop", "", "Invalid value 'Desktop' for MSTestApplicationModel.", DisplayName = "Unknown application model")]
    [DataRow("Default", "<UseWinUI>true</UseWinUI>", "MSTestApplicationModel 'Default' conflicts", DisplayName = "Ordinary model conflicts with WinUI")]
    [DataRow("Default", "<UseUwpTools>true</UseUwpTools>", "MSTestApplicationModel 'Default' conflicts", DisplayName = "Ordinary model conflicts with UWP tooling")]
    [DataRow("Default", "<UseUwp>true</UseUwp>", "MSTestApplicationModel 'Default' conflicts", DisplayName = "Ordinary model conflicts with inferred UWP")]
    [DataRow("Uwp", "<UseWinUI>true</UseWinUI>", "MSTestApplicationModel 'Uwp' conflicts", DisplayName = "UWP model conflicts with WinUI")]
    [DataRow("WinUI", "<UseUwpTools>true</UseUwpTools>", "MSTestApplicationModel 'WinUI' conflicts", DisplayName = "WinUI model conflicts with UWP tooling")]
    [DataRow("WinUI", "<UseUwp>true</UseUwp>", "MSTestApplicationModel 'WinUI' conflicts", DisplayName = "WinUI model conflicts with inferred UWP")]
    public async Task MSTestSdk_ApplicationModel_RejectsInvalidOrConflictingChoices(
        string applicationModel, string toolingProperties, string expectedError)
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "InvalidApplicationModelSdk",
            $"""
            <MSTestApplicationModel>{applicationModel}</MSTestApplicationModel>
            <WindowsPackageType>None</WindowsPackageType>
            {toolingProperties}
            """,
            failIfReturnValueIsNotZero: false);

        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains(expectedError);
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Default", false, null)]
    [DataRow("Default", true, "MSTest.TestAdapter")]
    [DataRow("Uwp", false, "MSTest.Uwp")]
    [DataRow("Uwp", true, "MSTest.Uwp")]
    [DataRow("WinUI", false, "MSTest.WinUI")]
    [DataRow("WinUI", true, "MSTest.WinUI")]
    public async Task MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition(
        string applicationModel, bool enableSourceGeneration, string? expectedAdapterPackageId)
    {
        const string Source = """
            #file Helper.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <EnableWindowsTargeting>true</EnableWindowsTargeting>
                <IsTestApplication>false</IsTestApplication>
                <MSTestApplicationModel>$ApplicationModel$</MSTestApplicationModel>
                <EnableMSTestSourceGeneration>$EnableSourceGeneration$</EnableMSTestSourceGeneration>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
              </PropertyGroup>
              <Target Name="WriteHelperComposition" AfterTargets="Build">
                <PropertyGroup>
                  <_HelperBuilderHooks>@(TestingPlatformBuilderHook)</_HelperBuilderHooks>
                </PropertyGroup>
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-mstest-assets.txt"
                                  Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/copy-local-assets.txt"
                                  Lines="@(ReferenceCopyLocalPaths->'%(FullPath)')" Overwrite="true" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/builder-hooks.txt"
                                  Lines="BuilderHooks=[$(_HelperBuilderHooks)]" Overwrite="true" />
              </Target>
            </Project>

            #file HelperTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class HelperTests
            {
                [$TestMethodAttribute$]
                public void TestFromHelper() => Assert.AreEqual("Helper", typeof(HelperTests).Assembly.GetName().Name);
            }
            """;

        string targetFramework = applicationModel == "Default" ? TargetFrameworks.NetCurrent : $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0";
        string source = Source
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", targetFramework)
            .PatchCodeWithReplace("$ApplicationModel$", applicationModel)
            .PatchCodeWithReplace("$TestMethodAttribute$", applicationModel == "Default" ? "TestMethod" : "Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethod")
            .PatchCodeWithReplace("$EnableSourceGeneration$", enableSourceGeneration.ToString());
        if (enableSourceGeneration)
        {
            source += CompositionCentralPackageManagementSource;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"{applicationModel}HelperSdk", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Combine(testAsset.TargetAssetPath, "helper.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, targetFramework, expectedAdapterPackageId);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, expectedAdapterPackageId);
        string[] copyLocal = await File.ReadAllLinesAsync(Path.Combine(testAsset.TargetAssetPath, "copy-local-assets.txt"), TestContext.CancellationToken);
        Assert.DoesNotContain(IsMSTestApplicationPayload, copyLocal);
        Assert.DoesNotContain(
            "031F8871-2660-4208-8F6B-FC142B40ABFF",
            await File.ReadAllTextAsync(Path.Combine(testAsset.TargetAssetPath, "builder-hooks.txt"), TestContext.CancellationToken));
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(testAsset.TargetAssetPath, "bin", "Release", targetFramework), "*.dll", SearchOption.AllDirectories)
            .Where(IsMSTestApplicationPayload));
        await AssertGeneratedHelperSourcesAsync(testAsset.TargetAssetPath, targetFramework, enableSourceGeneration);
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Uwp", false)]
    [DataRow("WinUI", true)]
    public async Task MSTestSdk_UiApplication_WithOrdinarySourceGeneratedHelper_DoesNotResolveOrdinaryAdapter(
        string applicationModel, bool useCentralPackageManagement)
    {
        const string Source = """
            #file Application.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$UiTargetFramework$</TargetFramework>
                <EnableWindowsTargeting>true</EnableWindowsTargeting>
                <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
                <MSTestApplicationModel>$ApplicationModel$</MSTestApplicationModel>
                <WindowsPackageType>None</WindowsPackageType>
                <EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="Helper/Helper.csproj" />
                <Compile Remove="Helper/**" />
              </ItemGroup>
              <Target Name="WriteApplicationComposition" AfterTargets="ResolveReferences">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-mstest-assets.txt"
                                  Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
              </Target>
            </Project>

            #file ApplicationTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class ApplicationTests : HelperTests;

            #file Helper/Helper.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <IsTestApplication>false</IsTestApplication>
                <EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
              </PropertyGroup>
            </Project>

            #file Helper/HelperTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class HelperTests
            {
                [TestMethod]
                public void TestFromHelper() => Assert.AreEqual("Helper", typeof(HelperTests).Assembly.GetName().Name);
            }
            """;

        string uiTargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0";
        string source = Source
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$UiTargetFramework$", uiTargetFramework)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ApplicationModel$", applicationModel);
        if (useCentralPackageManagement)
        {
            source += CompositionCentralPackageManagementSource;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"{applicationModel}ApplicationWithHelperSdk", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Combine(testAsset.TargetAssetPath, "application.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        string expectedAdapterPackageId = $"MSTest.{applicationModel}";
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, uiTargetFramework, expectedAdapterPackageId);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, expectedAdapterPackageId);
        string helperDirectory = Path.Combine(testAsset.TargetAssetPath, "Helper");
        await AssertResolvedMSTestCompositionAsync(helperDirectory, TargetFrameworks.NetCurrent, "MSTest.TestAdapter");
        await AssertGeneratedHelperSourcesAsync(helperDirectory, TargetFrameworks.NetCurrent, enableSourceGeneration: true);

        string[] references = await File.ReadAllLinesAsync(Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt"), TestContext.CancellationToken);
        foreach (string assembly in new[] { "MSTest.TestAdapter.dll", "MSTestAdapter.PlatformServices.dll" })
        {
            string packagePath = Assert.ContainsSingle(references.Where(path => path.EndsWith(assembly, StringComparison.OrdinalIgnoreCase)));
            string applicationPath = Path.Combine(testAsset.TargetAssetPath, "bin", "Release", uiTargetFramework, assembly);
            Assert.AreSequenceEqual(
                SHA256.HashData(await File.ReadAllBytesAsync(packagePath, TestContext.CancellationToken)),
                SHA256.HashData(await File.ReadAllBytesAsync(applicationPath, TestContext.CancellationToken)),
                $"The application must copy {assembly} from {expectedAdapterPackageId}, not its ordinary helper.");
            Assert.IsFalse(File.Exists(Path.Combine(helperDirectory, "bin", "Release", TargetFrameworks.NetCurrent, assembly)));
        }
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DataRow("Uwp", false)]
    [DataRow("Uwp", true)]
    [DataRow("WinUI", false)]
    [DataRow("WinUI", true)]
    public async Task MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework(
        string applicationModel, bool useCentralPackageManagement)
    {
        const string ReportTarget = """
              <Target Name="WriteInnerComposition" AfterTargets="ResolveReferences">
                <Message Importance="high"
                         Text="InnerComposition:TargetFramework=$(TargetFramework);Model=$(_MSTestApplicationModel);AdapterPackage=$(_MSTestAdapterPackageId);EndComposition" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-mstest-assets-$(TargetFramework).txt"
                                  Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
              </Target>
            </Project>
            """;
        string uiTargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0";
        string source = SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", $"{TargetFrameworks.NetCurrent};{uiTargetFramework}")
            .PatchCodeWithReplace("$ExtraProperties$", $"""
                <EnableWindowsTargeting>true</EnableWindowsTargeting>
                <WindowsPackageType>None</WindowsPackageType>
                <EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
                <MSTestApplicationModel Condition=" '$(TargetFramework)' == '{uiTargetFramework}' ">{applicationModel}</MSTestApplicationModel>
                """)
            .PatchCodeWithReplace("</Project>", ReportTarget);
        if (useCentralPackageManagement)
        {
            source += CompositionCentralPackageManagementSource;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"Mixed{applicationModel}CompositionSdk", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Combine(testAsset.TargetAssetPath, "multi-tfm.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        result.AssertOutputContains($"InnerComposition:TargetFramework={TargetFrameworks.NetCurrent};Model=Default;AdapterPackage=MSTest.TestAdapter;EndComposition");
        result.AssertOutputContains($"InnerComposition:TargetFramework={uiTargetFramework};Model={applicationModel};AdapterPackage=MSTest.{applicationModel};EndComposition");
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, TargetFrameworks.NetCurrent, "MSTest.TestAdapter");
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, uiTargetFramework, $"MSTest.{applicationModel}");
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, "MSTest.TestAdapter", $"resolved-mstest-assets-{TargetFrameworks.NetCurrent}.txt");
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, $"MSTest.{applicationModel}", $"resolved-mstest-assets-{uiTargetFramework}.txt");
    }

    private static bool IsMSTestApplicationPayload(string path)
        => Path.GetFileName(path) is "MSTest.TestAdapter.dll" or "MSTestAdapter.PlatformServices.dll" or "MSTest.Uwp.dll" or "MSTest.WinUI.dll"
            or "MSTest.TestAdapter.resources.dll" or "MSTestAdapter.PlatformServices.resources.dll" or "MSTest.Uwp.resources.dll" or "MSTest.WinUI.resources.dll";

    private async Task AssertGeneratedHelperSourcesAsync(string projectDirectory, string targetFramework, bool enableSourceGeneration)
    {
        string generatedDirectory = Path.Combine(projectDirectory, "obj", "Release", targetFramework, "generated");
        string[] files = Directory.Exists(generatedDirectory)
            ? Directory.GetFiles(generatedDirectory, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.HasCount(enableSourceGeneration ? 3 : 0, files, generatedDirectory);
        if (enableSourceGeneration)
        {
            string registration = Assert.ContainsSingle(files.Where(path => path.EndsWith("MSTestReflectionMetadata.Registration.g.cs", StringComparison.Ordinal)));
            Assert.Contains("ReflectionMetadataHook.Register", await File.ReadAllTextAsync(registration, TestContext.CancellationToken));
        }
    }

    private async Task AssertResolvedMSTestCompositionAsync(string projectDirectory, string targetFramework, string? expectedAdapterPackageId)
    {
        using var assets = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(projectDirectory, "obj", "project.assets.json"), TestContext.CancellationToken));
        JsonProperty framework = Assert.ContainsSingle(
            assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject()
                .Where(property => property.Name == targetFramework || property.Value.GetProperty("targetAlias").GetString() == targetFramework));
        // Inspect only this inner framework's resolved closure, never the union of all assets targets.
        JsonElement targets = assets.RootElement.GetProperty("targets");
        string targetName = framework.Name;
        if (targetFramework.StartsWith("uap", StringComparison.Ordinal) && !targets.TryGetProperty(targetName, out _))
        {
            targetName = $"UAP,Version=v{targetFramework["uap".Length..]}";
        }

        JsonElement target = targets.GetProperty(targetName);
        string[] compositionPackages =
        [
            "MSTest.TestFramework", "MSTest.TestFramework.Extensions", "MSTest.TestAdapter", "MSTest.Uwp", "MSTest.WinUI",
        ];
        string[] actual = target.EnumerateObject()
            .Where(package => compositionPackages.Any(id => package.Name.StartsWith($"{id}/", StringComparison.OrdinalIgnoreCase)))
            .Select(package => package.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectedAdapterPackageId is null
            ? [$"MSTest.TestFramework/{MSTestVersion}"]
            : [$"MSTest.TestFramework/{MSTestVersion}", $"{expectedAdapterPackageId}/{MSTestVersion}"];
        Assert.AreSequenceEqual(expected.Order(StringComparer.Ordinal).ToArray(), actual, targetFramework);

        string? uiAsset = expectedAdapterPackageId switch
        {
            "MSTest.Uwp" when targetFramework.StartsWith("uap", StringComparison.Ordinal) => "lib/uap10.0.16299/MSTest.Uwp.dll",
            "MSTest.Uwp" => "lib/net10.0-windows10.0.17763/MSTest.Uwp.dll",
            "MSTest.WinUI" => "lib/net10.0-windows10.0.18362/MSTest.WinUI.dll",
            _ => null,
        };
        if (uiAsset is not null)
        {
            JsonElement compile = target.GetProperty($"{expectedAdapterPackageId}/{MSTestVersion}").GetProperty("compile");
            Assert.Contains(uiAsset, compile.EnumerateObject().Select(asset => asset.Name), targetFramework);
        }
    }

    private async Task AssertResolvedMSTestReferencesAsync(string projectDirectory, string? expectedAdapterPackageId, string reportName = "resolved-mstest-assets.txt")
    {
        string[] references = await File.ReadAllLinesAsync(Path.Combine(projectDirectory, reportName), TestContext.CancellationToken);
        Assert.ContainsSingle(references.Where(path => path.EndsWith("MSTest.TestFramework.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(path => path.EndsWith("MSTest.TestFramework.Extensions.dll", StringComparison.OrdinalIgnoreCase), references);
        foreach (string assembly in new[] { "MSTest.TestAdapter.dll", "MSTestAdapter.PlatformServices.dll", "MSTest.Uwp.dll", "MSTest.WinUI.dll" })
        {
            string[] matches = references.Where(path => path.EndsWith(assembly, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (expectedAdapterPackageId is null
                || (assembly == "MSTest.Uwp.dll" && expectedAdapterPackageId != "MSTest.Uwp")
                || (assembly == "MSTest.WinUI.dll" && expectedAdapterPackageId != "MSTest.WinUI"))
            {
                Assert.IsEmpty(matches, assembly);
            }
            else
            {
                string path = Assert.ContainsSingle(matches).Replace('\\', '/').ToLowerInvariant();
                Assert.Contains($"/{expectedAdapterPackageId.ToLowerInvariant()}/{MSTestVersion.ToLowerInvariant()}/", path, assembly);
            }
        }
    }
}
