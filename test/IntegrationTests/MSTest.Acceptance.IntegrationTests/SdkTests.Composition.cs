// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

public sealed partial class SdkTests
{
    private const string UIPayloadsIgnoreMessage = "UI payloads are produced only by Windows packs.";

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
                         Text="ClassicComposition:Uwp=$(_MSTestUwpApplicationModel);WinUI=$(_MSTestWinUIApplicationModel);EndComposition" />
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
        string projectPath = Path.Join(testAsset.TargetAssetPath, "ClassicUwp.csproj");
        BoundedCommandLineResult result = await RunWindowsApplicationModelCommandAsync(
            $"\"{tools.MSBuildPath}\" \"{projectPath}\" /restore /t:PrintClassicComposition /p:Configuration=Release /p:Platform=x64 /bl:\"{Path.Join(testAsset.TargetAssetPath, "classic-uwp.binlog")}\"",
            testAsset.TargetAssetPath,
            TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, $"{result.StandardOutput}{Environment.NewLine}{result.ErrorOutput}");
        Assert.Contains("ClassicComposition:Uwp=true;WinUI=false;EndComposition", result.StandardOutput);
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, "uap10.0.16299", expectAdapter: true);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, WindowsApplicationModelAssetKind.ClassicUwp);
    }

    public static IEnumerable<TestDataRow<(string ApplicationModel, string Runner)>> AdapterInjectionData =>
    [
        new(("Default", "NativeAOT")),
        WindowsPackageDataRow(("Uwp", "NativeAOT")),
        WindowsPackageDataRow(("WinUI", "NativeAOT")),
        WindowsPackageDataRow(("Uwp", "VSTest")),
        WindowsPackageDataRow(("WinUI", "VSTest")),
    ];

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DynamicData(nameof(AdapterInjectionData))]
    public async Task MSTestSdk_AdapterInjection_UsesTestAdapterWithCentralPackageManagement(string applicationModel, string runner)
    {
        string runnerProperties = runner == "VSTest"
            ? "<UseVSTest>true</UseVSTest>"
            : "<PublishAot>true</PublishAot><RuntimeIdentifier>win-x64</RuntimeIdentifier><TestingExtensionsProfile>None</TestingExtensionsProfile>";
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            $"{applicationModel}{runner}CompositionSdk",
            $"""
            {GetUIApplicationModelProperties(applicationModel)}
            <WindowsPackageType>None</WindowsPackageType>
            {runnerProperties}
            """,
            expectedUiAssetKind: GetUIAssetKind(applicationModel),
            useCentralPackageManagement: true);

        result.AssertOutputContains(
            $";Uwp={(applicationModel == "Uwp" ? "true" : "false")};WinUI={(applicationModel == "WinUI" ? "true" : "false")};EndComposition");
        Assert.MatchesRegex(@"PackageReferences=[^\r\n]*\bMSTest\.TestAdapter\b", result.StandardOutput);
        if (runner == "NativeAOT")
        {
            result.AssertOutputContains("MSTest.SourceGeneration");
        }
    }

    public static IEnumerable<TestDataRow<(string ApplicationModel, bool EnableSourceGeneration, bool UseCentralPackageManagement, bool LateOptIn)>> HelperCompositionData =>
    [
        new(("Default", false, false, false)),
        new(("Default", true, true, false)),
        WindowsPackageDataRow(("Uwp", false, false, false)),
        WindowsPackageDataRow(("Uwp", true, true, false)),
        WindowsPackageDataRow(("WinUI", false, false, false)),
        WindowsPackageDataRow(("WinUI", true, true, false)),
        new(("Default", true, false, true)),
        new(("Default", true, true, true)),
    ];

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [DynamicData(nameof(HelperCompositionData))]
    public async Task MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition(
        string applicationModel, bool enableSourceGeneration, bool useCentralPackageManagement, bool lateSourceGenerationOptIn)
    {
        const string Source = """
            #file Helper.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <EnableWindowsTargeting>true</EnableWindowsTargeting>
                <IsTestApplication>false</IsTestApplication>
                $UIProperties$
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
            .PatchCodeWithReplace("$UIProperties$", GetUIApplicationModelProperties(applicationModel))
            .PatchCodeWithReplace("$TestMethodAttribute$", applicationModel == "Default" ? "TestMethod" : "Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethod")
            .PatchCodeWithReplace("$EnableSourceGeneration$", lateSourceGenerationOptIn ? string.Empty : enableSourceGeneration.ToString());
        if (useCentralPackageManagement)
        {
            source += CompositionCentralPackageManagementSource;
        }

        if (lateSourceGenerationOptIn)
        {
            source += """

                #file Directory.Build.targets
                <Project>
                  <PropertyGroup>
                    <EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>
                  </PropertyGroup>
                </Project>
                """;
        }

        // Only generated code needs the adapter's runtime hooks; UI helpers otherwise get the UI APIs from MSTest.TestFramework alone.
        bool expectAdapter = enableSourceGeneration;
        WindowsApplicationModelAssetKind? uiAssetKind = GetUIAssetKind(applicationModel);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"{applicationModel}HelperSdk", source);
        if (lateSourceGenerationOptIn)
        {
            DotnetMuxerResult resolution = await DotnetCli.RunAsync(
                $"msbuild \"{Path.Join(testAsset.TargetAssetPath, "Helper.csproj")}\" -restore -t:ResolveReferences;WriteHelperComposition -p:Configuration=Release -bl:\"{Path.Join(testAsset.TargetAssetPath, "helper-resolution.binlog")}\"",
                cancellationToken: TestContext.CancellationToken);
            resolution.AssertExitCodeIs(0);
            await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, targetFramework, expectAdapter);
        }

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Join(testAsset.TargetAssetPath, "helper.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, targetFramework, expectAdapter);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, uiAssetKind, expectAdapter);
        string[] copyLocal = await File.ReadAllLinesAsync(Path.Join(testAsset.TargetAssetPath, "copy-local-assets.txt"), TestContext.CancellationToken);
        Assert.DoesNotContain(IsMSTestApplicationPayload, copyLocal);
        Assert.DoesNotContain(
            "031F8871-2660-4208-8F6B-FC142B40ABFF",
            await File.ReadAllTextAsync(Path.Join(testAsset.TargetAssetPath, "builder-hooks.txt"), TestContext.CancellationToken));
        Assert.IsEmpty(Directory.GetFiles(Path.Join(testAsset.TargetAssetPath, "bin", "Release", targetFramework), "*.dll", SearchOption.AllDirectories)
            .Where(IsMSTestApplicationPayload));
        await AssertGeneratedHelperSourcesAsync(testAsset.TargetAssetPath, targetFramework, enableSourceGeneration);
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = UIPayloadsIgnoreMessage)]
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
                $UIProperties$
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
            .PatchCodeWithReplace("$UIProperties$", GetUIApplicationModelProperties(applicationModel));
        if (useCentralPackageManagement)
        {
            source += CompositionCentralPackageManagementSource;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"{applicationModel}ApplicationWithHelperSdk", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Join(testAsset.TargetAssetPath, "application.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, uiTargetFramework, expectAdapter: true);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, GetUIAssetKind(applicationModel));
        string helperDirectory = Path.Join(testAsset.TargetAssetPath, "Helper");
        await AssertResolvedMSTestCompositionAsync(helperDirectory, TargetFrameworks.NetCurrent, expectAdapter: true);
        await AssertGeneratedHelperSourcesAsync(helperDirectory, TargetFrameworks.NetCurrent, enableSourceGeneration: true);

        string uiAdapterDirectory = $"/mstest.testadapter/{MSTestVersion.ToLowerInvariant()}/buildtransitive/net10.0/";
        string[] references = await File.ReadAllLinesAsync(Path.Join(testAsset.TargetAssetPath, "resolved-mstest-assets.txt"), TestContext.CancellationToken);
        foreach (string assembly in new[] { "MSTest.TestAdapter.dll", "MSTestAdapter.PlatformServices.dll" })
        {
            string packagePath = Assert.ContainsSingle(references.Where(path => path.EndsWith(assembly, StringComparison.OrdinalIgnoreCase)));
            Assert.EndsWith($"{uiAdapterDirectory}{assembly.ToLowerInvariant()}", NormalizePackagePath(packagePath), assembly);
            string applicationPath = Path.Join(testAsset.TargetAssetPath, "bin", "Release", uiTargetFramework, assembly);
            Assert.AreSequenceEqual(
                SHA256.HashData(await File.ReadAllBytesAsync(packagePath, TestContext.CancellationToken)),
                SHA256.HashData(await File.ReadAllBytesAsync(applicationPath, TestContext.CancellationToken)),
                $"The application must copy {assembly} from the UI adapter build in MSTest.TestAdapter, not its ordinary helper.");
            Assert.IsFalse(File.Exists(Path.Join(helperDirectory, "bin", "Release", TargetFrameworks.NetCurrent, assembly)));
        }
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = UIPayloadsIgnoreMessage)]
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
                         Text="InnerComposition:TargetFramework=$(TargetFramework);Uwp=$(_MSTestUwpApplicationModel);WinUI=$(_MSTestWinUIApplicationModel);EndComposition" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-mstest-assets-$(TargetFramework).txt"
                                  Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
              </Target>
            </Project>
            """;
        string uiTargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0";
        string uiProperty = applicationModel == "WinUI" ? "UseWinUI" : "UseUwp";
        string source = SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", $"{TargetFrameworks.NetCurrent};{uiTargetFramework}")
            .PatchCodeWithReplace("$ExtraProperties$", $"""
                <EnableWindowsTargeting>true</EnableWindowsTargeting>
                <WindowsPackageType>None</WindowsPackageType>
                <EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
                <{uiProperty} Condition=" '$(TargetFramework)' == '{uiTargetFramework}' ">true</{uiProperty}>
                """)
            .PatchCodeWithReplace("</Project>", ReportTarget);
        if (useCentralPackageManagement)
        {
            source += CompositionCentralPackageManagementSource;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"Mixed{applicationModel}CompositionSdk", source);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release -bl:\"{Path.Join(testAsset.TargetAssetPath, "multi-tfm.binlog")}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);

        result.AssertOutputContains($"InnerComposition:TargetFramework={TargetFrameworks.NetCurrent};Uwp=false;WinUI=false;EndComposition");
        result.AssertOutputContains(
            $"InnerComposition:TargetFramework={uiTargetFramework};Uwp={(applicationModel == "Uwp" ? "true" : "false")};WinUI={(applicationModel == "WinUI" ? "true" : "false")};EndComposition");
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, TargetFrameworks.NetCurrent, expectAdapter: true);
        await AssertResolvedMSTestCompositionAsync(testAsset.TargetAssetPath, uiTargetFramework, expectAdapter: true);
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, uiAssetKind: null, reportName: $"resolved-mstest-assets-{TargetFrameworks.NetCurrent}.txt");
        await AssertResolvedMSTestReferencesAsync(testAsset.TargetAssetPath, GetUIAssetKind(applicationModel), reportName: $"resolved-mstest-assets-{uiTargetFramework}.txt");
    }

    private static TestDataRow<T> WindowsPackageDataRow<T>(T value)
        => new(value) { IgnoreMessage = OperatingSystem.IsWindows() ? null : UIPayloadsIgnoreMessage };

    private static WindowsApplicationModelAssetKind? GetUIAssetKind(string applicationModel)
        => applicationModel switch
        {
            "Uwp" => WindowsApplicationModelAssetKind.ModernUwp,
            "WinUI" => WindowsApplicationModelAssetKind.WinUI,
            _ => null,
        };

    private static string GetUIApplicationModelProperties(string applicationModel)
        => applicationModel switch
        {
            "Uwp" => "<UseUwp>true</UseUwp>",
            "WinUI" => "<UseWinUI>true</UseWinUI>",
            _ => string.Empty,
        };

    // The UI framework assemblies are compile-time APIs that helpers may copy; only the adapter makes an output a test application.
    private static bool IsMSTestApplicationPayload(string path)
        => Path.GetFileName(path) is "MSTest.TestAdapter.dll" or "MSTestAdapter.PlatformServices.dll"
            or "MSTest.TestAdapter.resources.dll" or "MSTestAdapter.PlatformServices.resources.dll";

    private static string NormalizePackagePath(string path)
        => path.Replace('\\', '/').ToLowerInvariant();

    private async Task AssertGeneratedHelperSourcesAsync(string projectDirectory, string targetFramework, bool enableSourceGeneration)
    {
        string generatedDirectory = Path.Join(projectDirectory, "obj", "Release", targetFramework, "generated");
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

    private async Task AssertResolvedMSTestCompositionAsync(string projectDirectory, string targetFramework, bool expectAdapter)
    {
        using var assets = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Join(projectDirectory, "obj", "project.assets.json"), TestContext.CancellationToken));
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
        // The removed UI package ids stay listed so that a stale reference to them fails the comparison.
        string[] compositionPackages =
        [
            "MSTest.TestFramework", "MSTest.TestFramework.Extensions", "MSTest.TestAdapter", "MSTest.Uwp", "MSTest.WinUI",
        ];
        string[] actual = target.EnumerateObject()
            .Where(package => compositionPackages.Any(id => package.Name.StartsWith($"{id}/", StringComparison.OrdinalIgnoreCase)))
            .Select(package => package.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectAdapter
            ? [$"MSTest.TestAdapter/{MSTestVersion}", $"MSTest.TestFramework/{MSTestVersion}"]
            : [$"MSTest.TestFramework/{MSTestVersion}"];
        Assert.AreSequenceEqual(expected.Order(StringComparer.Ordinal).ToArray(), actual, targetFramework);
    }

    private async Task AssertResolvedMSTestReferencesAsync(
        string projectDirectory,
        WindowsApplicationModelAssetKind? uiAssetKind,
        bool expectAdapter = true,
        string reportName = "resolved-mstest-assets.txt")
    {
        string[] references = await File.ReadAllLinesAsync(Path.Join(projectDirectory, reportName), TestContext.CancellationToken);
        string version = MSTestVersion.ToLowerInvariant();
        string frameworkPath = Assert.ContainsSingle(references.Where(path => path.EndsWith("MSTest.TestFramework.dll", StringComparison.OrdinalIgnoreCase)));
        string compatibilityPath = Assert.ContainsSingle(references.Where(path => path.EndsWith("MSTest.TestFramework.Extensions.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(Path.Join(Path.GetDirectoryName(frameworkPath), "MSTest.TestFramework.Extensions.dll"), compatibilityPath);
        Assert.Contains($"/mstest.testframework/{version}/lib/", NormalizePackagePath(frameworkPath), frameworkPath);

        string? adapterDirectory = !expectAdapter ? null : uiAssetKind switch
        {
            null => $"/mstest.testadapter/{version}/lib/",
            WindowsApplicationModelAssetKind.ClassicUwp => $"/mstest.testadapter/{version}/lib/uap10.0.16299/",
            _ => $"/mstest.testadapter/{version}/buildtransitive/net10.0/",
        };
        string? uwpDirectory = uiAssetKind switch
        {
            WindowsApplicationModelAssetKind.ClassicUwp => $"/mstest.testframework/{version}/lib/uap10.0.16299/",
            WindowsApplicationModelAssetKind.ModernUwp => $"/mstest.testframework/{version}/buildtransitive/net10.0/uwp/",
            _ => null,
        };
        string? winUIDirectory = uiAssetKind == WindowsApplicationModelAssetKind.WinUI
            ? $"/mstest.testframework/{version}/buildtransitive/net10.0/winui/"
            : null;
        (string Assembly, string? ExpectedDirectory)[] expectedAssemblies =
        [
            ("MSTest.TestAdapter.dll", adapterDirectory),
            ("MSTestAdapter.PlatformServices.dll", adapterDirectory),
            ("MSTest.Uwp.dll", uwpDirectory),
            ("MSTest.WinUI.dll", winUIDirectory),
        ];
        foreach ((string assembly, string? expectedDirectory) in expectedAssemblies)
        {
            string[] matches = references.Where(path => string.Equals(Path.GetFileName(path), assembly, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (expectedDirectory is null)
            {
                Assert.IsEmpty(matches, assembly);
            }
            else
            {
                string path = NormalizePackagePath(Assert.ContainsSingle(matches));
                string directory = path[..(path.LastIndexOf('/') + 1)];
                if (expectedDirectory.EndsWith("/lib/", StringComparison.Ordinal))
                {
                    // Ordinary assets come from lib/<tfm>/, whose folder name follows the consumer's target framework.
                    directory = directory[..(directory.TrimEnd('/').LastIndexOf('/') + 1)];
                }

                Assert.EndsWith(expectedDirectory, directory, path);
            }
        }
    }
}
