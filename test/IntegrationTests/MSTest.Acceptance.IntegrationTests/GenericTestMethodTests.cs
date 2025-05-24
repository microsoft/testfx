// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class GenericTestMethodTests : AcceptanceTestBase<GenericTestMethodTests.TestAssetFixture>
{
    [TestMethod]
    public async Task TestDifferentGenericMethodTestCases()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath("GenericTestMethodTests"), "GenericTestMethodTests", TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputMatchesRegex(
            """
            failed AMethodWithBadConstraints \(0\) \(\d+ms\)
              GenericArguments\[0], 'System\.Int32', on 'Void AMethodWithBadConstraints\[T]\(T\)' violates the constraint of type 'T'\.
                .+?
            failed NonParameterizedTestMethod \(\d+ms\)
              The generic test method 'NonParameterizedTestMethod' doesn't have arguments, so the generic parameter cannot be inferred\.
                .+?
            failed ParameterizedMethodSimple \(1\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter '1' and type 'System\.Byte'\.
                .+?
            failed ParameterizedMethodSimple \(2\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter '2' and type 'System\.Int32'\.
                .+?
            failed ParameterizedMethodSimple \("Hello world"\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter 'Hello world' and type 'System\.String'\.
                .+?
            failed ParameterizedMethodSimple \(null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodSimple threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T' could not be inferred\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(1,"Hello world",2,3\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: Found two conflicting types for generic parameter 'T2'\. The conflicting types are 'System\.Byte' and 'System\.Int32'\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(null,"Hello world","Hello again",3\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodTwoGenericParametersAndFourMethodParameters' did run with parameters '<null>', 'Hello world', 'Hello again', '3' and generic types 'System\.Int32', 'System\.String'\.
                .+?
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \("Hello hello","Hello world",null,null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T1' could not be inferred\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(null,null,null,null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T1' could not be inferred\.
            failed ParameterizedMethodSimpleParams \(1\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                .+?
            failed ParameterizedMethodSimpleParams \(1,2\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                .+?
            failed ParameterizedMethodSimpleParams \("Hello world"\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                .+?
            failed ParameterizedMethodSimpleParams \(null\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                .+?
            failed ParameterizedMethodSimpleParams \(null,"Hello world"\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                .+?
            failed ParameterizedMethodWithNestedGeneric \(System\.Collections\.Generic\.List`1\[System.String],System\.Collections\.Generic\.List`1\[System.String]\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodWithNestedGeneric' did run with first list \[Hello, World] and second list \[Unit, Testing]
                .+?
            failed ParameterizedMethodWithNestedGeneric \(System\.Collections\.Generic\.List`1\[System.Int32],System\.Collections\.Generic\.List`1\[System.Int32]\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodWithNestedGeneric' did run with first list \[0, 1] and second list \[2, 3]
                .+?
            """, RegexOptions.Singleline);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return ("GenericTestMethodTests", "GenericTestMethodTests",
                SourceGenericTestMethod
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceGenericTestMethod = """
#file GenericTestMethodTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    [DataRow(0)]
    public void AMethodWithBadConstraints<T>(T p) where T : IDisposable
        => Assert.Fail($"Test method 'AMethodWithBadConstraints' did run with T type being '{typeof(T)}'.");

    [TestMethod]
    public void NonParameterizedTestMethod<T>()
        => Assert.Fail("Test method 'NonParameterizedTestMethod' did run.");

    [TestMethod]
    [DataRow((byte)1)]
    [DataRow((int)2)]
    [DataRow("Hello world")]
    [DataRow(null)]
    public void ParameterizedMethodSimple<T>(T parameter)
        => Assert.Fail($"Test method 'ParameterizedMethodSimple' did run with parameter '{parameter?.ToString() ?? "<null>"}' and type '{typeof(T)}'.");

    [TestMethod]
    [DataRow((byte)1, "Hello world", (int)2, 3)]
    [DataRow(null, "Hello world", "Hello again", 3)]
    [DataRow("Hello hello", "Hello world", null, null)]
    [DataRow(null, null, null, null)]
    public void ParameterizedMethodTwoGenericParametersAndFourMethodParameters<T1, T2>(T2 p1, string p2, T2 p3, T1 p4)
        => Assert.Fail($"Test method 'ParameterizedMethodTwoGenericParametersAndFourMethodParameters' did run with parameters '{p1?.ToString() ?? "<null>"}', '{p2 ?? "<null>"}', '{p3?.ToString() ?? "<null>"}', '{p4?.ToString() ?? "<null>"}' and generic types '{typeof(T1)}', '{typeof(T2)}'.");

    [TestMethod]
    [DataRow((byte)1)]
    [DataRow((byte)1, 2)]
    [DataRow("Hello world")]
    [DataRow(null)]
    [DataRow(null, "Hello world")]
    public void ParameterizedMethodSimpleParams<T>(params T[] parameter)
        => Assert.Fail($"Test method 'ParameterizedMethodSimple' did run with parameter '{string.Join(",", parameter)}' and type '{typeof(T)}'.");

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void ParameterizedMethodWithNestedGeneric<T>(List<T> a, List<T> b)
    {
        Assert.AreEqual(2, a.Count);
        Assert.AreEqual(2, b.Count);
        Assert.Fail($"Test method 'ParameterizedMethodWithNestedGeneric' did run with first list [{a[0]}, {a[1]}] and second list [{b[0]}, {b[1]}]");
    }

    public static IEnumerable<object[]> Data
    {
        get
        {
            yield return new object[] { new List<string>() { "Hello", "World" }, new List<string>() { "Unit", "Testing" } };
            yield return new object[] { new List<int>() { 0, 1 }, new List<int>() { 2, 3 } };
        }
    }
}
""";
    }
}
