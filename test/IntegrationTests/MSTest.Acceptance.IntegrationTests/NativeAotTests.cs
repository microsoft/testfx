// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class NativeAotTests : AcceptanceTestBase<NopAssetFixture>
{
    // Source code for a project that validates MSTest supporting Native AOT.
    // Sets MSTestSourceGenMode=ReflectionFree explicitly (this is also the shipped default now) so
    // MSTest.SourceGeneration emits reflection-free metadata (materialized attributes + delegate-based
    // invokers) and the runtime does not fall back to reflection for user test discovery or invocation.
    private const string SourceCode = """
#file MSTestNativeAotTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <PublishAot>true</PublishAot>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
        <!-- Reflection-free is now the default, but pin it so the test stays meaningful even if the
             product default ever changes again. -->
        <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>
        <!-- Show individual trim/AOT warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
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

#pragma warning disable MSTESTEXP

[assembly: TestFilterProvider(typeof(MyTests.RunAllFilter))]

namespace MyTests;

public sealed class RunAllFilter : ITestFilter
{
    public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
}

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

    [TestMethod]
    [TestCategory("AsyncGenerated")]
    [DataRow(1, "small", true)]
    public async Task TestMethod3(int size, string category, bool enabled)
    {
        await Task.Yield();
        Assert.AreEqual(1, size);
        Assert.AreEqual("small", category);
        Assert.IsTrue(enabled);
    }

    [TestMethod]
    [TestCategory("AsyncGenerated")]
    [DataRow((object)new object[] { 1, 2 })]
    public async Task TestMethod4(object[] values)
    {
        await Task.Yield();
        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
    }

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void TestMethod5(int a, int b)
    {
    }

    public static IEnumerable<object[]> Data { get; }
        = new[]
        {
           new object[] { 1, 2 }
        };

    [TestMethod]
    [CombinatorialData]
    public void TestMethod6(
        [CombinatorialMemberData(nameof(MemberValues.Values), MemberType = typeof(MemberValues))]
        int value)
    {
        Assert.IsTrue(value is 1 or 2);
    }

    [TestMethod]
    [CombinatorialData]
    public void TestMethod7(
        [CombinatorialClassData(typeof(ClassValues), 3)]
        int value)
    {
        Assert.IsTrue(value is 3 or 4);
    }
}

public static class MemberValues
{
    public static List<int> Values => [1, 2];
}

public sealed class ClassValues(int start) : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return [start];
        yield return [start + 1];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
""";

    private const string AsyncVoidSourceCode = """

#file AsyncVoidTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public sealed class AsyncVoidTests
{
#pragma warning disable MSTEST0003 // Deliberately invalid to verify generated async metadata preserves async-void rejection.
    [TestMethod]
    public async void InvalidAsyncVoidTest()
    {
        await Task.Yield();
    }
#pragma warning restore MSTEST0003
}
""";

    [TestMethod]
    // The hosted AzDO agents for Mac OS don't have the required tooling for us to test Native AOT.
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task NativeAotTests_WillRunWithExitCodeZero(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestNativeAotTests_{tfm}",
            SourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            warnAsError: true,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        // Source files in this repo (and the source-generator output filename) whose absence in
        // publish output indicates MSTest itself is not surfacing trim/AOT warnings. Adding new MSTest
        // code that produces ILxxxx warnings will cause its source file to show up here and fail this
        // test. (The list mirrors TrimTests.Publish_WithTestAdapter_DoesNotSurfaceWarningsFromSuppressedSources.)
        foreach (string fileName in TrimAndAotAssertions.MSTestOwnedSourceFiles)
        {
            compilationResult.AssertOutputDoesNotContain(fileName);
        }

        string registryPath = Directory.GetFiles(
            generator.TargetAssetPath,
            "MSTestReflectionMetadata.Registry.g.cs",
            SearchOption.AllDirectories).Single();
        string registry = File.ReadAllText(registryPath);
        int synchronousMethodIndex = registry.IndexOf("Name = \"TestMethod1\"", StringComparison.Ordinal);
        int synchronousNextMethodIndex = registry.IndexOf("Name = \"TestMethod2\"", synchronousMethodIndex, StringComparison.Ordinal);
        Assert.IsGreaterThan(-1, synchronousMethodIndex);
        Assert.IsGreaterThan(synchronousMethodIndex, synchronousNextMethodIndex);
        StringAssert.Contains(
            registry.Substring(synchronousMethodIndex, synchronousNextMethodIndex - synchronousMethodIndex),
            "IsDescriptorSupported = true");

        int asyncMethodIndex = registry.IndexOf("Name = \"TestMethod3\"", StringComparison.Ordinal);
        int nextMethodIndex = registry.IndexOf("Name = \"TestMethod4\"", asyncMethodIndex, StringComparison.Ordinal);
        Assert.IsGreaterThan(-1, asyncMethodIndex);
        Assert.IsGreaterThan(asyncMethodIndex, nextMethodIndex);
        StringAssert.Contains(
            registry.Substring(asyncMethodIndex, nextMethodIndex - asyncMethodIndex),
            "AreAttributesComplete = true");
        StringAssert.Contains(
            registry.Substring(asyncMethodIndex, nextMethodIndex - asyncMethodIndex),
            "IsDescriptorSupported = false");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestNativeAotTests", tfm, RID, Verb.publish);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContainsSummary(failed: 0, passed: 9, skipped: 0);
        result.AssertExitCodeIs(0);

        TestHostResult asyncGeneratedResult = await testHost.ExecuteAsync(
            "--filter TestCategory=AsyncGenerated",
            cancellationToken: TestContext.CancellationToken);
        asyncGeneratedResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        asyncGeneratedResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow("net10.0")]
    public async Task NativeAotTests_RejectsAsyncVoidTestMethods(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestNativeAotAsyncVoidTests_{tfm}",
            (SourceCode + AsyncVoidSourceCode)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            warnAsError: true,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestNativeAotTests", tfm, RID, Verb.publish);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        result.AssertStandardErrorContains("UTA007: Method InvalidAsyncVoidTest");
        result.AssertExitCodeIsNot(0);
    }

    public TestContext TestContext { get; set; }

    private const string MetadataSourceCode = """
#file MSTestNativeAotMetadata.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <PublishAot>true</PublishAot>
        <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
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
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow("net10.0")]
    public async Task NativeAotTests_HonorsGeneratedInheritedTypeAndMethodMetadata(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestNativeAotMetadata_{tfm}",
            MetadataSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            warnAsError: true,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        foreach (string fileName in TrimAndAotAssertions.MSTestOwnedSourceFiles)
        {
            compilationResult.AssertOutputDoesNotContain(fileName);
        }

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestNativeAotMetadata", tfm, RID, Verb.publish);

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
