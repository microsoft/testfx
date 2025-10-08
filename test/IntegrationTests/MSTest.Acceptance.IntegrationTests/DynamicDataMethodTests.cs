// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DynamicDataMethodTests : AcceptanceTestBase<DynamicDataMethodTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DynamicDataTestWithParameterizedDataProviderMethod(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContainsSummary(failed: 3, passed: 9, skipped: 0);

        // failed TestMethodSingleParameterIntCountMismatchSmaller (0ms)
        //   Parameter count mismatch.
        //     at System.Reflection.MethodBaseInvoker.ThrowTargetParameterCountException()
        //     at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        //     at System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)
        //
        // failed TestMethodSingleParameterIntCountMismatchLarger (0ms)
        //   Parameter count mismatch.
        //     at System.Reflection.MethodBaseInvoker.ThrowTargetParameterCountException()
        //     at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        //     at System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)
        //
        // failed TestMethodParamsNotSupported (0ms)
        //   Dynamic data method 'TestClass1.GetDataParams' should be static, non-generic, and cannot have 'params' parameter.
        testHostResult.AssertOutputMatchesRegex(@"failed TestMethodSingleParameterIntCountMismatchSmaller \(\d+ms\)[\r\n]+\s+Parameter count mismatch.");
        testHostResult.AssertOutputMatchesRegex(@"failed TestMethodSingleParameterIntCountMismatchLarger \(\d+ms\)[\r\n]+\s+Parameter count mismatch.");
        testHostResult.AssertOutputMatchesRegex(@"failed TestMethodParamsNotSupported \(\d+ms\)[\r\n]+\s+Dynamic data method 'TestClass1.GetDataParams' should be static, non-generic, and cannot have 'params' parameter.");

        testHostResult.AssertOutputContains("TestMethodSingleParameterInt called with: 4");
        testHostResult.AssertOutputContains("TestMethodSingleParameterInt called with: 5");
        testHostResult.AssertOutputContains("TestMethodSingleParameterInt called with: 6");

        testHostResult.AssertOutputContains("TestMethodTwoParametersIntAndString called with: 4, Hello1");
        testHostResult.AssertOutputContains("TestMethodTwoParametersIntAndString called with: 5, Hello2");
        testHostResult.AssertOutputContains("TestMethodTwoParametersIntAndString called with: 6, Hello3");

        testHostResult.AssertOutputContains("TestMethodSingleParameterIntArray called with: 5");
        testHostResult.AssertOutputContains("TestMethodSingleParameterIntArray called with: 7");
        testHostResult.AssertOutputContains("TestMethodSingleParameterIntArray called with: 9");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "DynamicDataMethodTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file DynamicDataMethodTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>

#file TestClass1.cs
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    [TestMethod]
    [DynamicData(nameof(GetDataSingleParameterInt), 4)]
    public void TestMethodSingleParameterInt(int a)
    {
        // We call GetDataSingleParameterInt with 4, so it should yield 4, 5, and 6.
        Console.WriteLine($"TestMethodSingleParameterInt called with: {a}");
    }

    [TestMethod]
    [DynamicData(nameof(GetDataTwoParametersIntAndString), 4, "Hello")]
    public void TestMethodTwoParametersIntAndString(int a, string s)
    {
        // We call GetDataTwoParametersIntAndString with 4 and "Hello", so it should yield:
        // - (4, "Hello1")
        // - (5, "Hello2")
        // - (6, "Hello3")
        Console.WriteLine($"TestMethodTwoParametersIntAndString called with: {a}, {s}");
    }

    [TestMethod]
    [DynamicData(nameof(GetDataSingleParameterIntArray), [new int[] { 4, 5, 6 }])]
    public void TestMethodSingleParameterIntArray(int x)
    {
        // We call GetDataSingleParameterIntArray with an array (4, 5, and 6), so it should yield 5, 7, and 9.
        Console.WriteLine($"TestMethodSingleParameterIntArray called with: {x}");
    }

    [TestMethod]
    [DynamicData(nameof(GetDataSingleParameterInt))]
    public void TestMethodSingleParameterIntCountMismatchSmaller(int x)
    {
        // This test should fail due parameter count mismatch.
    }

    [TestMethod]
    [DynamicData(nameof(GetDataSingleParameterInt), 1, 2)]
    public void TestMethodSingleParameterIntCountMismatchLarger(int x)
    {
        // This test should fail due parameter count mismatch.
    }

    [TestMethod]
    [DynamicData(nameof(GetDataParams), 1, 2)]
    public void TestMethodParamsNotSupported(int x)
    {
        // This test should fail because we don't support params.
    }

    public static IEnumerable<int> GetDataSingleParameterInt(int i)
    {
        yield return i++;
        yield return i++;
        yield return i++;
    }

    public static IEnumerable<object[]> GetDataSingleParameterIntArray(int[] input)
    {
        yield return [1 + input[0]];
        yield return [2 + input[1]];
        yield return [3 + input[2]];
    }

    public static IEnumerable<object[]> GetDataTwoParametersIntAndString(int i, string s)
    {
        yield return new object[] { i++, s + "1" };
        yield return new object[] { i++, s + "2" };
        yield return new object[] { i++, s + "3" };
    }

    public static IEnumerable<int> GetDataParams(params int[] i)
    {
        yield return 0;
        yield return 1;
    }
}

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>
""";
    }

    public TestContext TestContext { get; set; }
}
