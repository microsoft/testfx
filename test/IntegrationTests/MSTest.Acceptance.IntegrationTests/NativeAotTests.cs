// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class NativeAotTests : AcceptanceTestBase
{
    private const string SourceCodeCsproj = """
        #file NativeAotTests.csproj
        <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <LangVersion>preview</LangVersion>
                <PublishAot>true</PublishAot>
                <NoWarn>$(NoWarn);IL2104;IL2026;IL3053</NoWarn>
                <!--
                    This makes sure that the project is referencing MSTest.TestAdapter.dll when MSTest.TestAdapter nuget is imported,
                    without this the dll is just copied into the output folder.
                -->
                <EnableMSTestRunner>true</EnableMSTestRunner>
            </PropertyGroup>
            <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
                <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
            </ItemGroup>
        </Project>
        """;

    private const string SourceCodeSimple = $$"""
        {{SourceCodeCsproj}}

        #file TestClass1.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace MyTests;

        [TestClass]
        public class UnitTest1
        {
            [ClassInitialize]
            public static void ClassInit(TestContext testContext)
            {
            }

            [ClassCleanup]
            public static void ClassClean()
            {
            }

            [TestInitialize]
            public void TestInit()
            {
            }

            [TestCleanup]
            public void TestClean()
            {
            }

            [AssemblyInitialize]
            public static void AssemblyInit(TestContext testContext)
            {
            }

            [AssemblyCleanup]
            public static void AssemblyClean()
            {
            }

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
            [DynamicData(nameof(Data))]
            public void TestMethod3(int a, int b)
            {
            }

            public static IEnumerable<object[]> Data { get; }
                = new[]
                {
                   new object[] { 1, 2 }
                };
        }
        """;

    private const string SourceCodeWithFailingAssert = $$"""
        {{SourceCodeCsproj}}

        #file TestClass1.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace MyTests;

        [TestClass]
        public class UnitTest1
        {
            [ClassInitialize]
            public static void ClassInit(TestContext testContext)
            {
            }

            [ClassCleanup]
            public static void ClassClean()
            {
            }

            [TestInitialize]
            public void TestInit()
            {
            }

            [TestCleanup]
            public void TestClean()
            {
            }

            [AssemblyInitialize]
            public static void AssemblyInit(TestContext testContext)
            {
            }

            [AssemblyCleanup]
            public static void AssemblyClean()
            {
            }

            [TestMethod]
            public void TestMethod1()
            {
                Assert.Fail("Failing TestMethod1");
            }

            [TestMethod]
            [DataRow(0, 1)]
            [DataRow(2, 3)]
            public void TestMethod2(int a, int b)
            {
                if (a == 2) Assert.Fail("Failing a specific case of TestMethod2");
            }

            [TestMethod]
            [DynamicData(nameof(Data))]
            public void TestMethod3(int a, int b)
            {
                if (a == 2) Assert.Fail("Failing a specific case of TestMethod3");
            }

            public static IEnumerable<object[]> Data { get; }
                = new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 2, 3 },
                };
        }
        """;

    private const string SourceCodeWithInCompatibleLibrary = $$"""
        #file TestProject/NativeAotTests.csproj
        <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <LangVersion>preview</LangVersion>
                <PublishAot>true</PublishAot>
                <NoWarn>$(NoWarn);IL2104;IL2026;IL3053</NoWarn>
                <!--
                    This makes sure that the project is referencing MSTest.TestAdapter.dll when MSTest.TestAdapter nuget is imported,
                    without this the dll is just copied into the output folder.
                -->
                <EnableMSTestRunner>true</EnableMSTestRunner>
            </PropertyGroup>
            <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
                <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
                <ProjectReference Include="..\TestBadLibrary\TestBadLibrary.csproj" />
            </ItemGroup>
        </Project>
        
        #file TestProject/TestClass1.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;
        
        namespace MyTests;
        
        [TestClass]
        public class UnitTest1
        {
            [TestMethod]
            public void TestMethod1()
            {
                Assert.IsTrue(TestBadLibrary.ClassToBeTested.M());
            }
        }

        #file TestBadLibrary/TestBadLibrary.csproj
        <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Library</OutputType>
                <LangVersion>preview</LangVersion>
            </PropertyGroup>
        </Project>
        
        #file TestBadLibrary/ClassToBeTested.cs

        using System;
        using System.Diagnostics.CodeAnalysis;
        using System.Reflection;

        namespace TestBadLibrary;

        public static class ClassToBeTested
        {
            [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Intentionally not trimmer friendly. That is what we are testing ;)")]
            public static bool M()
            {
                var asm = Assembly.GetExecutingAssembly();
                var type = asm.GetType("TestBadLibrary.ClassToBeTested");
                var m = type!.GetMethod("CalledByReflection");
                return (bool)m!.Invoke(null, null)!;
            }

            private static bool CalledByReflection()
            {
                return true;
            }
        }

        """;

    private readonly AcceptanceFixture _acceptanceFixture;

    public NativeAotTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    public async Task NativeAotTests_WillRunWithExitCodeZero()
        // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
        // or sometimes doesn't fail but the native code generation is not done.
        // Retrying the restore/publish on fresh asset seems to be more effective than retrying on the same asset.
        => await RetryHelper.RetryAsync(
            async () =>
            {
                using TestAsset generator = await TestAsset.GenerateAssetAsync(
                    "NativeAotTests",
                    SourceCodeSimple
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                    .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments)
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                    .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
                    addPublicFeeds: true);

                await DotnetCli.RunAsync(
                    $"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    retryCount: 0);
                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                    $"publish -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    timeoutInSeconds: 100,
                    retryCount: 0);
                compilationResult.AssertOutputContains("Generating native code");

                var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "NativeAotTests", TargetFrameworks.NetCurrent.Arguments, RID, Verb.publish);

                TestHostResult result = await testHost.ExecuteAsync();
                result.AssertExitCodeIs(0);
            }, times: 15, every: TimeSpan.FromSeconds(5));

    public async Task NativeAotTests_WillFailAsserts()
     // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
     // or sometimes doesn't fail but the native code generation is not done.
     // Retrying the restore/publish on fresh asset seems to be more effective than retrying on the same asset.
     => await RetryHelper.RetryAsync(
         async () =>
         {
             using TestAsset generator = await TestAsset.GenerateAssetAsync(
                 "NativeAotTests",
                 SourceCodeWithFailingAssert
                 .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                 .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                 .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments)
                 .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                 .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
                 addPublicFeeds: true);

             await DotnetCli.RunAsync(
                 $"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                 _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                 retryCount: 0);
             DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                 $"publish -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                 _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                 timeoutInSeconds: 100,
                 retryCount: 0);
             compilationResult.AssertOutputContains("Generating native code");

             var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "NativeAotTests", TargetFrameworks.NetCurrent.Arguments, RID, Verb.publish);

             TestHostResult result = await testHost.ExecuteAsync();
             result.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

             result.AssertOutputContains("failed TestMethod1");
             result.AssertOutputContains("Assert.Fail failed. Failing TestMethod1");

             result.AssertOutputContains("failed TestMethod2 (2,3)");
             result.AssertOutputContains("Assert.Fail failed. Failing a specific case of TestMethod2");

             result.AssertOutputContains("failed TestMethod3 (2,3)");
             result.AssertOutputContains("Assert.Fail failed. Failing a specific case of TestMethod3");

             result.AssertOutputContains("Test run summary: Failed!");
             result.AssertOutputContains("total: 5");
             result.AssertOutputContains("failed: 3");
             result.AssertOutputContains("succeeded: 2");
             result.AssertOutputContains("skipped: 0");
         }, times: 15, every: TimeSpan.FromSeconds(5));

    public async Task NativeAotTests_WillFailBecauseTestedLibraryIsNotCompatible()
     // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
     // or sometimes doesn't fail but the native code generation is not done.
     // Retrying the restore/publish on fresh asset seems to be more effective than retrying on the same asset.
     => await RetryHelper.RetryAsync(
         async () =>
         {
             using TestAsset generator = await TestAsset.GenerateAssetAsync(
                 "NativeAotTests",
                 SourceCodeWithInCompatibleLibrary
                 .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                 .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                 .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments)
                 .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                 .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
                 addPublicFeeds: true);

             await DotnetCli.RunAsync(
                 $"restore -m:1 -nodeReuse:false {Path.Combine(generator.TargetAssetPath, "TestProject")} -r {RID}",
                 _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                 retryCount: 0);
             DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                 $"publish -m:1 -nodeReuse:false {Path.Combine(generator.TargetAssetPath, "TestProject")} -r {RID}",
                 _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                 timeoutInSeconds: 100,
                 retryCount: 0);
             compilationResult.AssertOutputContains("Generating native code");

             var testHost = TestHost.LocateFrom(Path.Combine(generator.TargetAssetPath, "TestProject"), "NativeAotTests", TargetFrameworks.NetCurrent.Arguments, RID, Verb.publish);

             TestHostResult result = await testHost.ExecuteAsync();
             result.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

             result.AssertOutputContains("System.NullReferenceException: Object reference not set to an instance of an object.");
             result.AssertOutputContains("TestBadLibrary.ClassToBeTested.M()");

             result.AssertOutputContains("Test run summary: Failed!");
             result.AssertOutputContains("total: 1");
             result.AssertOutputContains("failed: 1");
             result.AssertOutputContains("succeeded: 0");
             result.AssertOutputContains("skipped: 0");
         }, times: 15, every: TimeSpan.FromSeconds(5));
}
