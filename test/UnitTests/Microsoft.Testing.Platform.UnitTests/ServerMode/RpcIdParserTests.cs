// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class RpcIdParserTests
{
    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("-0", 0)]
    [DataRow("42", 42)]
    [DataRow("-42", -42)]
    [DataRow("2147483647", int.MaxValue)]
    [DataRow("-2147483648", int.MinValue)]
    public void TryParseNumericId_PlainIntegerWithinInt32Range_ReturnsValue(string value, int expected)
        => AssertParses(value, expected);

    [TestMethod]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    [DataRow("9999999999")]
    public void TryParseNumericId_PlainIntegerOutsideInt32Range_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("-0.0", 0)]
    public void TryParseNumericId_ZeroMantissa_ReturnsZero(string value, int expected)
        => AssertParses(value, expected);

    [TestMethod]
    [DataRow("1.0", 1)]
    [DataRow("-42.000", -42)]
    [DataRow("2147483647.0", int.MaxValue)]
    public void TryParseNumericId_ZeroFraction_ReturnsInteger(string value, int expected)
        => AssertParses(value, expected);

    [TestMethod]
    [DataRow("1.1")]
    [DataRow("-42.001")]
    [DataRow("0.0001")]
    public void TryParseNumericId_NonZeroFractionWithoutScaling_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    [TestMethod]
    [DataRow("1.5e2", 150)]
    [DataRow("1.23e2", 123)]
    [DataRow("-1.5E2", -150)]
    [DataRow("0.001e3", 1)]
    public void TryParseNumericId_PositiveExponentAbsorbsFraction_ReturnsValue(string value, int expected)
        => AssertParses(value, expected);

    [TestMethod]
    [DataRow("1.5e0")]
    [DataRow("1.23e1")]
    [DataRow("-0.01E1")]
    public void TryParseNumericId_PositiveExponentLeavesNonZeroFraction_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    [TestMethod]
    [DataRow("1.23e10")]
    [DataRow("1e11")]
    [DataRow("-2147483649e0")]
    public void TryParseNumericId_PositiveExponentOverflowsInt32_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    [TestMethod]
    [DataRow("100e-2", 1)]
    [DataRow("1200e-2", 12)]
    [DataRow("-1000E-3", -1)]
    public void TryParseNumericId_NegativeExponentTrimsTrailingZeros_ReturnsValue(string value, int expected)
        => AssertParses(value, expected);

    [TestMethod]
    [DataRow("10e-2")]
    [DataRow("1e-1")]
    [DataRow("150e-2")]
    public void TryParseNumericId_NegativeExponentLeavesNonZeroFraction_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    [TestMethod]
    [DataRow("1e+")]
    public void TryParseNumericId_MalformedExponent_ReturnsFalse(string value)
        => AssertDoesNotParse(value);

    private static void AssertParses(string value, int expected)
    {
        bool parsed = RpcIdParser.TryParseNumericId(value, out int actual);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, actual);
    }

    private static void AssertDoesNotParse(string value)
    {
        bool parsed = RpcIdParser.TryParseNumericId(value, out int actual);

        Assert.IsFalse(parsed);
        Assert.AreEqual(default, actual);
    }
}
