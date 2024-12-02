// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class GenericTestMethodTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public GenericTestMethodTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    public async Task TestDifferentGenericMethodTestCases()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.GetAssetPath("GenericTestMethodTests"), "GenericTestMethodTests", TargetFrameworks.NetCurrent.Arguments);

        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputMatchesRegex("""
            failed AMethodWithBadConstraints \(0\) \(\d+ms\)
              GenericArguments\[0], 'System\.Int32', on 'Void AMethodWithBadConstraints\[T]\(T\)' violates the constraint of type 'T'\.
                at System\.RuntimeType\.ValidateGenericArguments\(MemberInfo definition, RuntimeType\[] genericArguments, Exception e\)
                at System\.Reflection\.RuntimeMethodInfo\.MakeGenericMethod\(Type\[] methodInstantiation\)
            failed NonParameterizedTestMethod \(\d+ms\)
              The generic test method 'NonParameterizedTestMethod' doesn't have arguments, so the generic parameter cannot be inferred\.
            failed ParameterizedMethodSimple \(1\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter '1' and type 'System\.Byte'\.
                at TestClass\.ParameterizedMethodSimple\[T]\(T parameter\) in .+?UnitTest1\.cs:23
                at System\.RuntimeMethodHandle\.InvokeMethod\(Object target, Void\*\* arguments, Signature sig, Boolean isConstructor\)
                at System\.Reflection\.MethodBaseInvoker\.InvokeDirectByRefWithFewArgs\(Object obj, Span`1 copyOfArgs, BindingFlags invokeAttr\)
            failed ParameterizedMethodSimple \(2\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter '2' and type 'System\.Int32'\.
                at TestClass\.ParameterizedMethodSimple\[T]\(T parameter\) in .+?UnitTest1\.cs:23
                at System\.RuntimeMethodHandle\.InvokeMethod\(Object target, Void\*\* arguments, Signature sig, Boolean isConstructor\)
                at System\.Reflection\.MethodBaseInvoker\.InvokeDirectByRefWithFewArgs\(Object obj, Span`1 copyOfArgs, BindingFlags invokeAttr\)
            failed ParameterizedMethodSimple \("Hello world"\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodSimple' did run with parameter 'Hello world' and type 'System\.String'\.
                at TestClass\.ParameterizedMethodSimple\[T]\(T parameter\) in .+?UnitTest1\.cs:23
                at System\.RuntimeMethodHandle\.InvokeMethod\(Object target, Void\*\* arguments, Signature sig, Boolean isConstructor\)
                at System\.Reflection\.MethodBaseInvoker\.InvokeDirectByRefWithFewArgs\(Object obj, Span`1 copyOfArgs, BindingFlags invokeAttr\)
            failed ParameterizedMethodSimple \(null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodSimple threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T' could not be inferred\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(1,"Hello world",2,3\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: Found two conflicting types for generic parameter 'T2'\. The conflicting types are 'System\.Byte' and 'System\.Int32'\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(null,"Hello world","Hello again",3\) \(\d+ms\)
              Assert\.Fail failed\. Test method 'ParameterizedMethodTwoGenericParametersAndFourMethodParameters' did run with parameters '<null>', 'Hello world', 'Hello again', '3' and generic types 'System\.Int32', 'System\.String'\.
                at TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters\[T1,T2]\(T2 p1, String p2, T2 p3, T1 p4\) in .+?UnitTest1\.cs:31
                at System\.RuntimeMethodHandle\.InvokeMethod\(Object target, Void\*\* arguments, Signature sig, Boolean isConstructor\)
                at System\.Reflection\.MethodBaseInvoker\.InvokeDirectByRefWithFewArgs\(Object obj, Span`1 copyOfArgs, BindingFlags invokeAttr\)
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \("Hello hello","Hello world",null,null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T1' could not be inferred\.
            failed ParameterizedMethodTwoGenericParametersAndFourMethodParameters \(null,null,null,null\) \(\d+ms\)
              Test method TestClass\.ParameterizedMethodTwoGenericParametersAndFourMethodParameters threw exception: 
              System\.InvalidOperationException: The type of the generic parameter 'T1' could not be inferred\.
            failed ParameterizedMethodSimpleParams \(1\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                at System\.RuntimeType\.CreateInstanceCheckThis\(\)
                at System\.RuntimeType\.CreateInstanceImpl\(BindingFlags bindingAttr, Binder binder, Object\[] args, CultureInfo culture\)
                at System\.Activator\.CreateInstance\(Type type, Object\[] args\)
                at Microsoft\.VisualStudio\.TestPlatform\.MSTestAdapter\.PlatformServices\.ReflectionOperations2\.CreateInstance\(Type type, Object\[] parameters\) in .+?ReflectionOperations2.cs:60
            failed ParameterizedMethodSimpleParams \(1,2\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                at System\.RuntimeType\.CreateInstanceCheckThis\(\)
                at System\.RuntimeType\.CreateInstanceImpl\(BindingFlags bindingAttr, Binder binder, Object\[] args, CultureInfo culture\)
                at System\.Activator\.CreateInstance\(Type type, Object\[] args\)
                at Microsoft\.VisualStudio\.TestPlatform\.MSTestAdapter\.PlatformServices\.ReflectionOperations2\.CreateInstance\(Type type, Object\[] parameters\) in .+?ReflectionOperations2.cs:60
            failed ParameterizedMethodSimpleParams \("Hello world"\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                at System\.RuntimeType\.CreateInstanceCheckThis\()
                at System\.RuntimeType\.CreateInstanceImpl\(BindingFlags bindingAttr, Binder binder, Object\[] args, CultureInfo culture\)
                at System\.Activator\.CreateInstance\(Type type, Object\[] args\)
                at Microsoft\.VisualStudio\.TestPlatform\.MSTestAdapter\.PlatformServices\.ReflectionOperations2\.CreateInstance\(Type type, Object\[] parameters\) in .+?ReflectionOperations2.cs:60
            failed ParameterizedMethodSimpleParams \(null\) (\d+ms)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                at System\.RuntimeType\.CreateInstanceCheckThis\(\)
                at System\.RuntimeType\.CreateInstanceImpl\(BindingFlags bindingAttr, Binder binder, Object\[] args, CultureInfo culture\)
                at System\.Activator\.CreateInstance\(Type type, Object\[] args\)
                at Microsoft\.VisualStudio\.TestPlatform\.MSTestAdapter\.PlatformServices\.ReflectionOperations2\.CreateInstance\(Type type, Object\[] parameters\) in .+?ReflectionOperations2.cs:60
            failed ParameterizedMethodSimpleParams \(null,"Hello world"\) \(\d+ms\)
              Cannot create an instance of T\[] because Type\.ContainsGenericParameters is true\.
                at System\.RuntimeType\.CreateInstanceCheckThis\(\)
                at System\.RuntimeType\.CreateInstanceImpl\(BindingFlags bindingAttr, Binder binder, Object\[] args, CultureInfo culture\)
                at System\.Activator\.CreateInstance\(Type type, Object\[] args\)
                at Microsoft\.VisualStudio\.TestPlatform\.MSTestAdapter\.PlatformServices\.ReflectionOperations2\.CreateInstance\(Type type, Object\[] parameters\) in .+?ReflectionOperations2.cs:60
            """);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
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
}
""";
    }
}
