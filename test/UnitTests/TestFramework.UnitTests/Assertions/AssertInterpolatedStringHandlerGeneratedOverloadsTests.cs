// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Regression coverage for the <c>AppendLiteral</c>/<c>AppendFormatted</c> overload set that the source
/// generator emits onto every <c>Assert.*InterpolatedStringHandler</c> struct (see
/// <c>GenerateAssertInterpolatedStringAppendMethodsAttribute</c>). These tests drive a real failing
/// assertion with an interpolated <c>$"..."</c> message so the compiler binds to each generated overload
/// on the handler struct and the rendered text is verified end-to-end. If the generator stops emitting an
/// overload (or emits it incorrectly), the corresponding case here fails to compile or renders wrong text.
/// </summary>
public sealed class AssertInterpolatedStringHandlerGeneratedOverloadsTests : TestContainer
{
    // AppendLiteral(string) + AppendFormatted<T>(T value)
    public void Literal_AndGenericValue_AreRendered()
    {
        int value = 7;
        Action action = () => Assert.IsTrue(false, $"value=|{value}|");
        action.Should().Throw<Exception>().WithMessage("*value=|7|*");
    }

    // AppendFormatted<T>(T value, string? format)
    public void GenericValueWithFormat_IsRendered()
    {
        int value = 7;
        Action action = () => Assert.IsTrue(false, $"|{value:D4}|");
        action.Should().Throw<Exception>().WithMessage("*|0007|*");
    }

    // AppendFormatted<T>(T value, int alignment)
    public void GenericValueWithAlignment_IsRendered()
    {
        int value = 7;
        Action action = () => Assert.IsTrue(false, $"|{value,5}|");
        action.Should().Throw<Exception>().WithMessage("*|    7|*");
    }

    // AppendFormatted<T>(T value, int alignment, string? format)
    public void GenericValueWithAlignmentAndFormat_IsRendered()
    {
        int value = 7;
        Action action = () => Assert.IsTrue(false, $"|{value,6:D4}|");
        action.Should().Throw<Exception>().WithMessage("*|  0007|*");
    }

    // AppendFormatted(string? value)
    public void StringValue_IsRendered()
    {
        string text = "hi";
        Action action = () => Assert.IsTrue(false, $"|{text}|");
        action.Should().Throw<Exception>().WithMessage("*|hi|*");
    }

    // AppendFormatted(string? value, int alignment, string? format)
    public void StringValueWithAlignment_IsRendered()
    {
        string text = "hi";
        Action action = () => Assert.IsTrue(false, $"|{text,5}|");
        action.Should().Throw<Exception>().WithMessage("*|   hi|*");
    }

    // AppendFormatted(object? value, int alignment, string? format)
    public void ObjectValueWithAlignmentAndFormat_IsRendered()
    {
        object boxed = 7;
        Action action = () => Assert.IsTrue(false, $"|{boxed,5:D2}|");
        action.Should().Throw<Exception>().WithMessage("*|   07|*");
    }

    // AppendFormatted(object? value) (defaulted alignment/format)
    public void ObjectValue_IsRendered()
    {
        object boxed = 7;
        Action action = () => Assert.IsTrue(false, $"|{boxed}|");
        action.Should().Throw<Exception>().WithMessage("*|7|*");
    }

#if NETCOREAPP3_1_OR_GREATER
    // AppendFormatted(ReadOnlySpan<char> value)
    public void ReadOnlySpanValue_IsRendered()
    {
        string text = "spantext";
        Action action = () => Assert.IsTrue(false, $"|{text.AsSpan()}|");
        action.Should().Throw<Exception>().WithMessage("*|spantext|*");
    }

    // AppendFormatted(ReadOnlySpan<char> value, int alignment, string? format)
    public void ReadOnlySpanValueWithAlignment_IsRendered()
    {
        string text = "abc";
        Action action = () => Assert.IsTrue(false, $"|{text.AsSpan(),5}|");
        action.Should().Throw<Exception>().WithMessage("*|  abc|*");
    }
#endif

    // The generated overloads must exist on every handler, not just AssertIsTrueInterpolatedStringHandler.
    // The following cases bind to other handler structs so a handler that lost its [Generate...] attribute
    // would fail to compile here.
    public void NonGenericHandler_IsFalse_RendersInterpolatedMessage()
    {
        int value = 42;
        Action action = () => Assert.IsFalse(true, $"isFalse|{value:D3}|");
        action.Should().Throw<Exception>().WithMessage("*isFalse|042|*");
    }

    // Exercises the one handler that uses the nullable 'AppendLiteral(string?)' overload
    // (AssertAreEqualInterpolatedStringHandler<TArgument>).
    public void GenericHandler_AreEqual_RendersInterpolatedMessage()
    {
        int value = 9;
        Action action = () => Assert.AreEqual(1, 2, $"areEqual|{value,4}|");
        action.Should().Throw<Exception>().WithMessage("*areEqual|   9|*");
    }

    public void Handler_IsNull_RendersInterpolatedMessage()
    {
        object notNull = new();
        string label = "obj";
        Action action = () => Assert.IsNull(notNull, $"isNull|{label}|");
        action.Should().Throw<Exception>().WithMessage("*isNull|obj|*");
    }
}
