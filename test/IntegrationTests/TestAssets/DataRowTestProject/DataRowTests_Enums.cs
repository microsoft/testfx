// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Test type")]
public class DataRowTests_Enums
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

    #region Basic Tests
    [TestMethod]
    [DataRow(SByteEnum.Alfa)]
    [DataRow(SByteEnum.Beta)]
    [DataRow(SByteEnum.Gamma)]
    public void DataRowEnums_SByte(SByteEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(ByteEnum.Alfa)]
    [DataRow(ByteEnum.Beta)]
    [DataRow(ByteEnum.Gamma)]
    public void DataRowEnums_Byte(ByteEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(ShortEnum.Alfa)]
    [DataRow(ShortEnum.Beta)]
    [DataRow(ShortEnum.Gamma)]
    public void DataRowEnums_Short(ShortEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(UShortEnum.Alfa)]
    [DataRow(UShortEnum.Beta)]
    [DataRow(UShortEnum.Gamma)]
    public void DataRowEnums_UShort(UShortEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(IntEnum.Alfa)]
    [DataRow(IntEnum.Beta)]
    [DataRow(IntEnum.Gamma)]
    public void DataRowEnums_Int(IntEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(UIntEnum.Alfa)]
    [DataRow(UIntEnum.Beta)]
    [DataRow(UIntEnum.Gamma)]
    public void DataRowEnums_UInt(UIntEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(LongEnum.Alfa)]
    [DataRow(LongEnum.Beta)]
    [DataRow(LongEnum.Gamma)]
    public void DataRowEnum_Long(LongEnum testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(ULongEnum.Alfa)]
    [DataRow(ULongEnum.Beta)]
    [DataRow(ULongEnum.Gamma)]
    public void DataRowEnum_ULong(ULongEnum testEnum) => Assert.IsTrue(true);
    #endregion

    #region Basic Tests (Nullable)
    [TestMethod]
    [DataRow(null)]
    [DataRow(SByteEnum.Alfa)]
    [DataRow(SByteEnum.Beta)]
    [DataRow(SByteEnum.Gamma)]
    public void DataRowEnums_Nullable_SByte(SByteEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(ByteEnum.Alfa)]
    [DataRow(ByteEnum.Beta)]
    [DataRow(ByteEnum.Gamma)]
    public void DataRowEnums_Nullable_Byte(ByteEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(ShortEnum.Alfa)]
    [DataRow(ShortEnum.Beta)]
    [DataRow(ShortEnum.Gamma)]
    public void DataRowEnums_Nullable_Short(ShortEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(UShortEnum.Alfa)]
    [DataRow(UShortEnum.Beta)]
    [DataRow(UShortEnum.Gamma)]
    public void DataRowEnums_Nullable_UShort(UShortEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(IntEnum.Alfa)]
    [DataRow(IntEnum.Beta)]
    [DataRow(IntEnum.Gamma)]
    public void DataRowEnums_Nullable_Int(IntEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(UIntEnum.Alfa)]
    [DataRow(UIntEnum.Beta)]
    [DataRow(UIntEnum.Gamma)]
    public void DataRowEnums_Nullable_UInt(UIntEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(LongEnum.Alfa)]
    [DataRow(LongEnum.Beta)]
    [DataRow(LongEnum.Gamma)]
    public void DataRowEnums_Nullable_Long(LongEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(null)]
    [DataRow(ULongEnum.Alfa)]
    [DataRow(ULongEnum.Beta)]
    [DataRow(ULongEnum.Gamma)]
    public void DataRowEnums_Nullable_ULong(ULongEnum? testEnum) => Assert.IsTrue(true);
    #endregion

    #region Mixed Types
    [TestMethod]
    [DataRow(ByteEnum.Alfa, true, 1)]
    [DataRow(ByteEnum.Beta, false, 2)]
    [DataRow(ByteEnum.Gamma, true, 3)]
    public void DataRowEnums_MixedTypes_Byte(ByteEnum testEnum, bool arg1, int arg2) => Assert.IsTrue(true);
    #endregion
}
