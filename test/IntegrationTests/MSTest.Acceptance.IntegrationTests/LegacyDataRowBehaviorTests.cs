// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LegacyDataRowBehaviorTests : AcceptanceTestBase<LegacyDataRowBehaviorTests.TestAssetFixture>
{
    private const string AssetName = "LegacyDataRowBehavior";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task InheritedAndOverriddenDataRowsHaveExactNamesAndCounts(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);

        TestHostResult allResults = await testHost.ExecuteAsync(
            "--filter TestCategory=DataRowSimple --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        allResults.AssertExitCodeIs(ExitCode.Success);
        allResults.AssertOutputContainsSummary(failed: 0, passed: 5, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            allResults,
            "DataRowTestMethod (\"BaseString1\")",
            "DataRowTestMethod (\"BaseString2\")",
            "DataRowTestMethod (\"BaseString3\")",
            "DataRowTestMethod (\"DerivedString1\")",
            "DataRowTestMethod (\"DerivedString2\")");

        TestHostResult derivedResults = await testHost.ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.DerivedDataRows --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        derivedResults.AssertExitCodeIs(ExitCode.Success);
        derivedResults.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            derivedResults,
            "DataRowTestMethod (\"DerivedString1\")",
            "DataRowTestMethod (\"DerivedString2\")");
        derivedResults.AssertOutputDoesNotContain("BaseString1");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task OptionalParamsAndInvalidArgumentsProduceExactResults(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.ParameterBindingCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 3, passed: 11, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "SomeOptional (123)",
            "SomeOptional (123,\"first\")",
            "SomeOptional (123,\"second\",\"third\")",
            "AllOptional ()",
            "AllOptional (123)",
            "AllOptional (123,\"fourth\")",
            "AllOptional (123,\"fifth\",\"sixth\")",
            "ParamsArray (2)",
            "ParamsArray (2,\"one\")",
            "ParamsArray (2,\"one\",\"two\")",
            "ParamsArray (2,\"one\",\"two\",\"three\")");
        LegacyAcceptanceAssert.Failed(
            result,
            "InvalidArguments ()",
            "InvalidArguments (2)",
            "InvalidArguments (2,\"required\",\"optional\",\"extra\")");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DataRowDisplayNamesPreserveSerializationShapes(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);
        string[] expectedTests =
        [
            "Doubles (10.01,20.01)",
            "Doubles (10.02,20.02)",
            "Mixed (10,10,10,10,10,10,10,\"10\")",
            "DataRowEnums_SByte (Alfa)",
            "DataRowEnums_SByte (Beta)",
            "DataRowEnums_SByte (Gamma)",
            "DataRowEnums_Byte (Alfa)",
            "DataRowEnums_Byte (Beta)",
            "DataRowEnums_Byte (Gamma)",
            "DataRowEnums_Short (Alfa)",
            "DataRowEnums_Short (Beta)",
            "DataRowEnums_Short (Gamma)",
            "DataRowEnums_UShort (Alfa)",
            "DataRowEnums_UShort (Beta)",
            "DataRowEnums_UShort (Gamma)",
            "DataRowEnums_Int (Alfa)",
            "DataRowEnums_Int (Beta)",
            "DataRowEnums_Int (Gamma)",
            "DataRowEnums_UInt (Alfa)",
            "DataRowEnums_UInt (Beta)",
            "DataRowEnums_UInt (Gamma)",
            "DataRowEnum_Long (Alfa)",
            "DataRowEnum_Long (Beta)",
            "DataRowEnum_Long (Gamma)",
            "DataRowEnum_ULong (Alfa)",
            "DataRowEnum_ULong (Beta)",
            "DataRowEnum_ULong (Gamma)",
            "DataRowEnums_Nullable_SByte (null)",
            "DataRowEnums_Nullable_SByte (Alfa)",
            "DataRowEnums_Nullable_SByte (Beta)",
            "DataRowEnums_Nullable_SByte (Gamma)",
            "DataRowEnums_Nullable_Byte (null)",
            "DataRowEnums_Nullable_Byte (Alfa)",
            "DataRowEnums_Nullable_Byte (Beta)",
            "DataRowEnums_Nullable_Byte (Gamma)",
            "DataRowEnums_Nullable_Short (null)",
            "DataRowEnums_Nullable_Short (Alfa)",
            "DataRowEnums_Nullable_Short (Beta)",
            "DataRowEnums_Nullable_Short (Gamma)",
            "DataRowEnums_Nullable_UShort (null)",
            "DataRowEnums_Nullable_UShort (Alfa)",
            "DataRowEnums_Nullable_UShort (Beta)",
            "DataRowEnums_Nullable_UShort (Gamma)",
            "DataRowEnums_Nullable_Int (null)",
            "DataRowEnums_Nullable_Int (Alfa)",
            "DataRowEnums_Nullable_Int (Beta)",
            "DataRowEnums_Nullable_Int (Gamma)",
            "DataRowEnums_Nullable_UInt (null)",
            "DataRowEnums_Nullable_UInt (Alfa)",
            "DataRowEnums_Nullable_UInt (Beta)",
            "DataRowEnums_Nullable_UInt (Gamma)",
            "DataRowEnums_Nullable_Long (null)",
            "DataRowEnums_Nullable_Long (Alfa)",
            "DataRowEnums_Nullable_Long (Beta)",
            "DataRowEnums_Nullable_Long (Gamma)",
            "DataRowEnums_Nullable_ULong (null)",
            "DataRowEnums_Nullable_ULong (Alfa)",
            "DataRowEnums_Nullable_ULong (Beta)",
            "DataRowEnums_Nullable_ULong (Gamma)",
            "DataRowEnums_MixedTypes_Byte (Alfa,True,1)",
            "DataRowEnums_MixedTypes_Byte (Beta,False,2)",
            "DataRowEnums_MixedTypes_Byte (Gamma,True,3)",
            "NonSerializable (System.String)",
            "NonSerializable (System.Int32)",
            "NonSerializable (LegacyDataRowBehavior.SerializationCases)",
        ];

        TestHostResult discovery = await testHost.ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.SerializationCases --list-tests",
            cancellationToken: TestContext.CancellationToken);

        discovery.AssertExitCodeIs(ExitCode.Success);
        discovery.AssertOutputContains("Test discovery summary: found 65 test(s)");
        LegacyAcceptanceAssert.OutputContains(discovery, expectedTests);

        TestHostResult execution = await testHost.ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.SerializationCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        execution.AssertExitCodeIs(ExitCode.Success);
        execution.AssertOutputContainsSummary(failed: 0, passed: 65, skipped: 0);
        LegacyAcceptanceAssert.Passed(execution, expectedTests);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RegularDataRowShapesAndOverloadsExecute(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.RegularShapes --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 10, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "OneStringArray ([\"\"])",
            "TwoObjectArrays ([\"\",1],[3])",
            "SixteenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16])",
            "NullObjectArray ([null])",
            "NullValue (null)",
            "MethodWithOverload (1)",
            "MethodWithOverload (2)",
            "MethodWithOverload (\"a\")",
            "MethodWithOverload (\"b\")",
            "MultipleIntegersWrappedWithParams (1,2,3,4,5)");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomDataAndTestMethodDisplayNamesArePreserved(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter ClassName=LegacyDataRowBehavior.DisplayNameCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "Overridden DisplayName",
            "SomeCustomDisplayName2 (\"SomeData\")",
            "SomeCustomDisplayName3 (\"SomeData\")");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public TestHost GetTestHost(string tfm)
            => TestHost.LocateFrom(GetAssetPath(AssetName), AssetName, tfm);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file LegacyDataRowBehavior.csproj
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
</Project>

#file DataRowCases.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegacyDataRowBehavior;

[TestClass]
public class BaseDataRows
{
    [TestCategory("DataRowSimple")]
    [TestMethod]
    [DataRow("BaseString1")]
    [DataRow("BaseString2")]
    [DataRow("BaseString3")]
    public virtual void DataRowTestMethod(string value) => Assert.IsNotNull(value);
}

[TestClass]
public class DerivedDataRows : BaseDataRows
{
    [TestCategory("DataRowSimple")]
    [TestMethod]
    [DataRow("DerivedString1")]
    [DataRow("DerivedString2")]
    public override void DataRowTestMethod(string value) => Assert.IsNotNull(value);
}

[TestClass]
public class ParameterBindingCases
{
    [TestMethod]
    [DataRow(123)]
    [DataRow(123, "first")]
    [DataRow(123, "second", "third")]
    public void SomeOptional(int value, string first = null, string second = null)
        => Assert.AreEqual(123, value);

    [TestMethod]
    [DataRow]
    [DataRow(123)]
    [DataRow(123, "fourth")]
    [DataRow(123, "fifth", "sixth")]
    public void AllOptional(int value = 0, string first = null, string second = null)
        => Assert.IsGreaterThanOrEqualTo(0, value);

    [TestMethod]
    [DataRow(2)]
    [DataRow(2, "one")]
    [DataRow(2, "one", "two")]
    [DataRow(2, "one", "two", "three")]
    public void ParamsArray(int value, params string[] values)
        => Assert.AreEqual(2, value);

    [TestMethod]
    [DataRow]
    [DataRow(2)]
    [DataRow(2, "required", "optional", "extra")]
    public void InvalidArguments(int value, string required, string optional = null)
        => Assert.Fail("Invalid rows must fail before invoking the method.");
}

[TestClass]
public class SerializationCases
{
    public enum SByteEnum : sbyte
    {
        Alfa = sbyte.MinValue,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum ByteEnum : byte
    {
        Alfa,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum ShortEnum : short
    {
        Alfa = byte.MaxValue + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum UShortEnum : ushort
    {
        Alfa = short.MaxValue + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum IntEnum
    {
        Alfa = ushort.MaxValue + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum UIntEnum : uint
    {
        Alfa = ((uint)int.MaxValue) + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum LongEnum : long
    {
        Alfa = ((long)uint.MaxValue) + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    public enum ULongEnum : ulong
    {
        Alfa = ((ulong)long.MaxValue) + 1,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    [TestMethod]
    [DataRow(10.01d, 20.01d)]
    [DataRow(10.02d, 20.02d)]
    public void Doubles(double first, double second)
        => Assert.IsTrue(first > 10d && second > 20d);

    [TestMethod]
    [DataRow((byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    public void Mixed(byte b, int i, uint ui, long l, ulong ul, float f, double d, string s)
        => Assert.AreEqual("10", s);

    [TestMethod]
    [DataRow(SByteEnum.Alfa)]
    [DataRow(SByteEnum.Beta)]
    [DataRow(SByteEnum.Gamma)]
    public void DataRowEnums_SByte(SByteEnum value) { }

    [TestMethod]
    [DataRow(ByteEnum.Alfa)]
    [DataRow(ByteEnum.Beta)]
    [DataRow(ByteEnum.Gamma)]
    public void DataRowEnums_Byte(ByteEnum value) { }

    [TestMethod]
    [DataRow(ShortEnum.Alfa)]
    [DataRow(ShortEnum.Beta)]
    [DataRow(ShortEnum.Gamma)]
    public void DataRowEnums_Short(ShortEnum value) { }

    [TestMethod]
    [DataRow(UShortEnum.Alfa)]
    [DataRow(UShortEnum.Beta)]
    [DataRow(UShortEnum.Gamma)]
    public void DataRowEnums_UShort(UShortEnum value) { }

    [TestMethod]
    [DataRow(IntEnum.Alfa)]
    [DataRow(IntEnum.Beta)]
    [DataRow(IntEnum.Gamma)]
    public void DataRowEnums_Int(IntEnum value) { }

    [TestMethod]
    [DataRow(UIntEnum.Alfa)]
    [DataRow(UIntEnum.Beta)]
    [DataRow(UIntEnum.Gamma)]
    public void DataRowEnums_UInt(UIntEnum value) { }

    [TestMethod]
    [DataRow(LongEnum.Alfa)]
    [DataRow(LongEnum.Beta)]
    [DataRow(LongEnum.Gamma)]
    public void DataRowEnum_Long(LongEnum value) { }

    [TestMethod]
    [DataRow(ULongEnum.Alfa)]
    [DataRow(ULongEnum.Beta)]
    [DataRow(ULongEnum.Gamma)]
    public void DataRowEnum_ULong(ULongEnum value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(SByteEnum.Alfa)]
    [DataRow(SByteEnum.Beta)]
    [DataRow(SByteEnum.Gamma)]
    public void DataRowEnums_Nullable_SByte(SByteEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(ByteEnum.Alfa)]
    [DataRow(ByteEnum.Beta)]
    [DataRow(ByteEnum.Gamma)]
    public void DataRowEnums_Nullable_Byte(ByteEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(ShortEnum.Alfa)]
    [DataRow(ShortEnum.Beta)]
    [DataRow(ShortEnum.Gamma)]
    public void DataRowEnums_Nullable_Short(ShortEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(UShortEnum.Alfa)]
    [DataRow(UShortEnum.Beta)]
    [DataRow(UShortEnum.Gamma)]
    public void DataRowEnums_Nullable_UShort(UShortEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(IntEnum.Alfa)]
    [DataRow(IntEnum.Beta)]
    [DataRow(IntEnum.Gamma)]
    public void DataRowEnums_Nullable_Int(IntEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(UIntEnum.Alfa)]
    [DataRow(UIntEnum.Beta)]
    [DataRow(UIntEnum.Gamma)]
    public void DataRowEnums_Nullable_UInt(UIntEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(LongEnum.Alfa)]
    [DataRow(LongEnum.Beta)]
    [DataRow(LongEnum.Gamma)]
    public void DataRowEnums_Nullable_Long(LongEnum? value) { }

    [TestMethod]
    [DataRow(null)]
    [DataRow(ULongEnum.Alfa)]
    [DataRow(ULongEnum.Beta)]
    [DataRow(ULongEnum.Gamma)]
    public void DataRowEnums_Nullable_ULong(ULongEnum? value) { }

    [TestMethod]
    [DataRow(ByteEnum.Alfa, true, 1)]
    [DataRow(ByteEnum.Beta, false, 2)]
    [DataRow(ByteEnum.Gamma, true, 3)]
    public void DataRowEnums_MixedTypes_Byte(ByteEnum value, bool flag, int number) { }

    [TestMethod]
    [DataRow(typeof(string))]
    [DataRow(typeof(int))]
    [DataRow(typeof(SerializationCases))]
    public void NonSerializable(Type value) => Assert.IsNotNull(value);
}

[TestClass]
public class RegularShapes
{
    [TestMethod]
    [DataRow(new string[] { "" })]
    public void OneStringArray(string[] values) => Assert.HasCount(1, values);

    [TestMethod]
    [DataRow(new object[] { "", 1 }, new object[] { 3 })]
    public void TwoObjectArrays(object[] first, object[] second)
    {
        Assert.HasCount(2, first);
        Assert.HasCount(1, second);
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 },
        new object[] { 13 }, new object[] { 14 }, new object[] { 15 }, new object[] { 16 })]
    public void SixteenObjectArrays(
        object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8,
        object[] o9, object[] o10, object[] o11, object[] o12,
        object[] o13, object[] o14, object[] o15, object[] o16) { }

    [TestMethod]
    [DataRow(null)]
    public void NullObjectArray(object[] value) => Assert.IsNull(value);

    [TestMethod]
    [DataRow(null)]
    public void NullValue(object value) => Assert.IsNull(value);

    [TestMethod] [DataRow("a")] [DataRow("b")] public void MethodWithOverload(string value) { }
    [TestMethod] [DataRow(1)] [DataRow(2)] public void MethodWithOverload(int value) { }

    [TestMethod]
    [DataRow(1, 2, 3, 4, 5)]
    public void MultipleIntegersWrappedWithParams(params int[] values) => Assert.HasCount(5, values);
}

[TestClass]
public class DisplayNameCases
{
    [TestMethod]
    [DummyDataRow]
    public void CustomDataRowDisplayName() { }

    [TestMethod(DisplayName = "SomeCustomDisplayName2")]
    [DataRow("SomeData")]
    public void DataRowTestMethodDisplayName(string value) { }

    [TestMethod(DisplayName = "SomeCustomDisplayName3")]
    [DynamicData(nameof(Data))]
    public void DynamicDataTestMethodDisplayName(string value) { }

    public static IEnumerable<object[]> Data => [["SomeData"]];

    private sealed class DummyDataRowAttribute : DataRowAttribute
    {
        public override string GetDisplayName(MethodInfo methodInfo, object[] data) => "Overridden DisplayName";
    }
}
""";
    }
}
