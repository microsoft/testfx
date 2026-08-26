// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// MSTest.SourceGeneration is most commonly used in Native AOT and trimming scenarios
/// (see <see cref="NativeAotTests"/> and <see cref="TrimTests"/>), but the package itself
/// is a plain Roslyn source generator that should work in any SDK-style project. These
/// acceptance tests pin that behavior: referencing the package on a non-AOT, non-trimmed
/// project must still build, emit the generated metadata file, and successfully run tests
/// through the source-generated <c>ReflectionMetadataHook</c> module initializer.
/// </summary>
[TestClass]
public class SourceGenerationNonAotTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestSourceGenNonAot";
    private const string SdkAssetName = "MSTestSourceGenSdkNonAot";

    // Source code for a non-AOT, non-trimmed test project that references MSTest.SourceGeneration.
    // EmitCompilerGeneratedFiles is enabled so we can statically assert the generator ran and
    // wrote out the expected metadata file alongside the assembly.
    private const string SourceCode = """
#file MSTestSourceGenNonAot.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>
        <!-- Persist the generator output to disk so the test can assert it was emitted. -->
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file TestClass1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    [TestMethod]
    [DataRow(0, 1)]
    public void TestMethod2(int a, int b)
    {
    }

    // Exercises the source-generated DynamicData accessor (property source) at runtime: if the generated
    // DynamicDataSourceResolver registration produced the wrong data, this test would fail or not run.
    public static IEnumerable<object[]> Data => new[] { new object[] { 1, 2 }, new object[] { 3, 4 } };

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void TestMethod3(int a, int b)
    {
        Assert.AreEqual(a + 1, b);
    }

    [TestMethod]
    [DataRow(1, "managed", true)]
    public async Task TestMethod4(int size, string category, bool enabled)
    {
        await Task.Yield();
        Assert.AreEqual(1, size);
        Assert.AreEqual("managed", category);
        Assert.IsTrue(enabled);
    }

    [TestMethod]
    public void Overload()
    {
    }

    [TestMethod]
    [DataRow(42)]
    public void Overload(int value)
    {
        Assert.AreEqual(42, value);
    }
}
""";

    private const string CentralPackageManagementSourceCode = """

#file Directory.Packages.props
<Project>
    <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
    </PropertyGroup>
</Project>
""";

    private const string SdkSourceCode = """
#file MSTestSourceGenSdkNonAot.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <IsTestApplication>$IsTestApplication$</IsTestApplication>
        <PublishAot>$PublishAot$</PublishAot>
        <UseVSTest>$UseVSTest$</UseVSTest>
        $EnableMicrosoftTestingPlatformProperty$
        $EnableMSTestSourceGenerationProperty$
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    $ExtraItems$
</Project>

#file TestClass1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";

    private const string TestingPlatformPackageReference = """
<ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
</ItemGroup>
""";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task SourceGenerationNonAot_BuildsAndRunsTests_WithExitCodeZero(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{AssetName}_{tfm}",
            SourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        // Plain `dotnet build` — no -r RID, no publish, no PublishAot, no PublishTrimmed.
        // This is the scenario the follow-up review surfaced: the source generator must also
        // work for users who opt in to source-generated discovery without trimming/AOT.
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        // Static evidence the source generator actually ran in the build (not just that
        // the package was restored). EmitCompilerGeneratedFiles writes the generator
        // output under obj/<config>/<tfm>/generated/<generator-assembly>/<full-type-name>/<hintname>.
        // This asset explicitly selects ReflectionFree mode because the assertions below verify
        // its compact registry and registration shape.
        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the source generator should have emitted a '*MSTestReflectionMetadata*.g.cs' file under '{objGenerated}'");

        string registry = File.ReadAllText(generatedFiles.Single(path => path.EndsWith("MSTestReflectionMetadata.Registry.g.cs", StringComparison.Ordinal)));
        Assert.DoesNotContain("DataRows", registry, "DataRowAttribute instances are authoritative; a second argument-array descriptor is redundant.");
        Assert.DoesNotContain("ParameterNames", registry, "runtime registration only resolves overloads by parameter type.");
        StringAssert.Contains(registry, "SupportsGeneratedDescriptors = true");
        StringAssert.Contains(registry, "IsDescriptorSupported = true");
        StringAssert.Contains(registry, "AreGeneratedDescriptorsComplete = false");

        string registration = File.ReadAllText(generatedFiles.Single(path => path.EndsWith("MSTestReflectionMetadata.Registration.g.cs", StringComparison.Ordinal)));
        StringAssert.Contains(registration, "availableMethods ??= type.GetMethods(memberFlags)");
        StringAssert.Contains(registration, "ResolveMethod(availableMethods, method.Name, method.ParameterTypes)");
        StringAssert.Contains(registration, "methodInfo.GetCustomAttributes(typeof(AsyncStateMachineAttribute), inherit: false)");
        StringAssert.Contains(registration, "methodInfo.GetCustomAttributes(typeof(DebuggerStepThroughAttribute), inherit: false)");
        StringAssert.Contains(registration, "descriptorTestMethods[type] = descriptorMethodRoots.ToArray()");
        StringAssert.Contains(registration, "descriptorTestMethods, descriptorCompleteTypes.ToArray()");

        // Behavioral evidence: tests still discover and run when the source-generated
        // ReflectionMetadataHook is the only metadata provider wired in at module init.
        // If the hook crashed during ModuleInitializer or swapped in a broken provider, the
        // test host would fail before printing a summary. (We assert the full count to also
        // catch silent discovery regressions where tests are not picked up.)
        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 7, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MSTestSdk_SourceGenerationOptIn_LibraryBuilds(bool useCentralPackageManagement)
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_Library_{useCentralPackageManagement}",
            (useCentralPackageManagement ? SdkSourceCode + CentralPackageManagementSourceCode : SdkSourceCode)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$UseVSTest$", "false")
            .PatchCodeWithReplace("$EnableMicrosoftTestingPlatformProperty$", string.Empty)
            .PatchCodeWithReplace(
                "$EnableMSTestSourceGenerationProperty$",
                "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.HasCount(3, generatedFiles, $"the MSTest.Sdk source-generation opt-in should have emitted exactly three metadata files under '{objGenerated}'");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MSTestSdk_SourceGenerationOptIn_ApplicationBuildsAndRuns(bool useCentralPackageManagement)
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_Application_{useCentralPackageManagement}",
            (useCentralPackageManagement ? SdkSourceCode + CentralPackageManagementSourceCode : SdkSourceCode)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "true")
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$UseVSTest$", "false")
            .PatchCodeWithReplace(
                "$EnableMicrosoftTestingPlatformProperty$",
                "<EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>")
            .PatchCodeWithReplace(
                "$EnableMSTestSourceGenerationProperty$",
                "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.HasCount(3, generatedFiles, $"the MSTest.Sdk source-generation opt-in should have emitted exactly three metadata files under '{objGenerated}'");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, SdkAssetName, tfm, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_NativeAotLibraryBuilds()
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_NativeAotLibrary",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "true")
            .PatchCodeWithReplace("$UseVSTest$", "false")
            .PatchCodeWithReplace("$EnableMicrosoftTestingPlatformProperty$", string.Empty)
            .PatchCodeWithReplace("$EnableMSTestSourceGenerationProperty$", string.Empty)
            .PatchCodeWithReplace("$ExtraItems$", TestingPlatformPackageReference)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.HasCount(3, generatedFiles, $"the NativeAOT library source generator should have emitted exactly three metadata files under '{objGenerated}'");
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_VSTestTakesPrecedenceOverNativeAot()
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_VSTestNativeAot",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "true")
            .PatchCodeWithReplace("$PublishAot$", "true")
            .PatchCodeWithReplace("$UseVSTest$", "true")
            .PatchCodeWithReplace("$EnableMicrosoftTestingPlatformProperty$", string.Empty)
            .PatchCodeWithReplace(
                "$EnableMSTestSourceGenerationProperty$",
                "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.HasCount(3, generatedFiles, $"the VSTest source-generation opt-in should have emitted exactly three metadata files under '{objGenerated}'");
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively()
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_Pack",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$UseVSTest$", "false")
            .PatchCodeWithReplace("$EnableMicrosoftTestingPlatformProperty$", string.Empty)
            .PatchCodeWithReplace(
                "$EnableMSTestSourceGenerationProperty$",
                "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        string packageDirectory = Path.Combine(generator.TargetAssetPath, "packages");
        DotnetMuxerResult packResult = await DotnetCli.RunAsync(
            $"pack {generator.TargetAssetPath} -c {BuildConfiguration.Release} -p:IsPackable=true -p:PackageVersion=1.0.0-test -o \"{packageDirectory}\"",
            cancellationToken: TestContext.CancellationToken);
        packResult.AssertExitCodeIs(0);

        string package = Directory.GetFiles(packageDirectory, "*.nupkg").Single();
        using ZipArchive archive = ZipFile.OpenRead(package);
        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        string[] dependencyIds = nuspec
            .Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => element.Attribute("id")?.Value)
            .OfType<string>()
            .ToArray();

        CollectionAssert.Contains(dependencyIds, "MSTest.TestAdapter");
        CollectionAssert.DoesNotContain(dependencyIds, "MSTest.SourceGeneration");
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_RejectsNetStandardTestLibrary()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_NetStandard",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", "netstandard2.0")
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$UseVSTest$", "false")
            .PatchCodeWithReplace("$EnableMicrosoftTestingPlatformProperty$", string.Empty)
            .PatchCodeWithReplace(
                "$EnableMSTestSourceGenerationProperty$",
                "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(1);
        buildResult.AssertOutputContains(
            "MSTest source generation is not supported for .NET Standard target frameworks because the required MSTest.TestAdapter runtime hooks are unavailable.");
    }

    public TestContext TestContext { get; set; } = null!;

    private const string MetadataSourceCode = """
#file MSTestSourceGenNonAotMetadata.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file MetadataTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public sealed class MethodMetadataTests
{
    [TestMethod]
    [TestCategory("GeneratedMetadata")]
    public void CategorizedTest()
    {
    }

    [TestMethod]
    [Ignore("generated method ignore")]
    public void IgnoredMethod()
        => Assert.Fail("The generated method-level IgnoreAttribute was not honored.");
}

[TestCategory("InheritedTypeMetadata")]
public abstract class CategorizedBase
{
}

[TestClass]
public sealed class InheritedCategorizedTests : CategorizedBase
{
    [TestMethod]
    public void CategorizedThroughBaseClass()
    {
    }
}
""";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task SourceGenerationNonAot_HonorsGeneratedInheritedTypeAndMethodMetadata(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{AssetName}Metadata_{tfm}",
            MetadataSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the reflection-free source generator should have emitted metadata under '{objGenerated}'");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestSourceGenNonAotMetadata", tfm, buildConfiguration: BuildConfiguration.Release);

        TestHostResult fullRun = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        fullRun.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 1);
        fullRun.AssertExitCodeIs(0);

        TestHostResult categoryRun = await testHost.ExecuteAsync(
            "--filter TestCategory=GeneratedMetadata",
            cancellationToken: TestContext.CancellationToken);
        categoryRun.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        categoryRun.AssertExitCodeIs(0);

        TestHostResult inheritedCategoryRun = await testHost.ExecuteAsync(
            "--filter TestCategory=InheritedTypeMetadata",
            cancellationToken: TestContext.CancellationToken);
        inheritedCategoryRun.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        inheritedCategoryRun.AssertExitCodeIs(0);
    }
}
