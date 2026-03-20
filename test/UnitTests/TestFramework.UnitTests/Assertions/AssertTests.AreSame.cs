// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void AreSame_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o);
    }

    public void AreSame_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object());
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame(new object(), new object())
            Expected references to be the same. Objects are not equal.
              expected: <System.Object> (Hash=*)
              actual:   <System.Object> (Hash=*)
            """);
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message");
    }

    public void AreSame_StringMessage_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object(), "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame(new object(), new object())
            User-provided message
            Expected references to be the same. Objects are not equal.
              expected: <System.Object> (Hash=*)
              actual:   <System.Object> (Hash=*)
            """);
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreSame(o, o, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task AreSame_InterpolatedString_PassDifferentObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreSame(new object(), new object(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).WithMessage("""
            Assert.AreSame(new object(), new object())
            User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString()*
            Expected references to be the same. Objects are not equal.
              expected: <System.Object> (Hash=*)
              actual:   <System.Object> (Hash=*)
            """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void AreNotSame_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object());

    public void AreSame_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1);
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame(1, 1)
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (Hash=*)
              actual:   1 (Hash=*)
            """);
    }

    public void AreSame_StringMessage_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame(1, 1)
            User-provided message
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (Hash=*)
              actual:   1 (Hash=*)
            """);
    }

    public void AreSame_InterpolatedString_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame(1, 1)
            User-provided message System.Object
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (Hash=*)
              actual:   1 (Hash=*)
            """);
    }

    public void AreNotSame_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o);
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreNotSame(o, o)
            Expected references to be different.
              not expected: <System.Object> (Hash=*)
              actual:       <System.Object> (Hash=*)
            """);
    }

    public void AreNotSame_StringMessage_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message");

    public void AreNotSame_StringMessage_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o, "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreNotSame(o, o)
            User-provided message
            Expected references to be different.
              not expected: <System.Object> (Hash=*)
              actual:       <System.Object> (Hash=*)
            """);
    }

    public void AreNotSame_InterpolatedString_PassDifferentObject_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotSame(new object(), new object(), $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task AreNotSame_InterpolatedString_PassSameObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotSame(o, o, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).WithMessage("""
            Assert.AreNotSame(o, o)
            User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString()*
            Expected references to be different.
              not expected: DummyClassTrackingToStringCalls (Hash=*)
              actual:       DummyClassTrackingToStringCalls (Hash=*)
            """);
        o.WasToStringCalled.Should().BeTrue();
    }

    #region AreSame/AreNotSame truncation and newline escaping

    public void AreSame_WithLongExpression_ShouldTruncateExpression()
    {
        object aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = new object();

        Action action = () => Assert.AreSame(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, new object());
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreSame(aVeryLongVariableNameThatExceedsOneHundredCharacte..., new object())
                Expected references to be the same. Objects are not equal.
                  expected: <System.Object> (Hash=*)
                  actual:   <System.Object> (Hash=*)
                """);
    }

    public void AreSame_WithLongToStringValue_ShouldTruncateValue()
    {
        Action action = () => Assert.AreSame(new ObjectWithLongToString(), new ObjectWithLongToString());
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.AreSame(new ObjectWithLongToString(), new ObjectWithLongToString())
                Expected references to be the same. Objects are not equal.
                  expected: {new string('L', 256)}... 44 more (Hash=*)
                  actual:   {new string('L', 256)}... 44 more (Hash=*)
                """);
    }

    public void AreSame_WithNewlineInToString_ShouldEscapeNewlines()
    {
        Action action = () => Assert.AreSame(new ObjectWithNewlineToString(), new ObjectWithNewlineToString());
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreSame(new ObjectWithNewlineToString(), new ObjectWithNewlineToString())
                Expected references to be the same. Objects are not equal.
                  expected: line1\r\nline2\nline3 (Hash=*)
                  actual:   line1\r\nline2\nline3 (Hash=*)
                """);
    }

    public void AreNotSame_WithLongExpression_ShouldTruncateExpression()
    {
        object aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = new object();

        Action action = () => Assert.AreNotSame(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreNotSame(aVeryLongVariableNameThatExceedsOneHundredCharacte..., aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                Expected references to be different.
                  not expected: <System.Object> (Hash=*)
                  actual:       <System.Object> (Hash=*)
                """);
    }

    public void AreNotSame_WithLongToStringValue_ShouldTruncateValue()
    {
        var obj = new ObjectWithLongToString();

        Action action = () => Assert.AreNotSame(obj, obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.AreNotSame(obj, obj)
                Expected references to be different.
                  not expected: {new string('L', 256)}... 44 more (Hash=*)
                  actual:       {new string('L', 256)}... 44 more (Hash=*)
                """);
    }

    public void AreNotSame_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var obj = new ObjectWithNewlineToString();

        Action action = () => Assert.AreNotSame(obj, obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreNotSame(obj, obj)
                Expected references to be different.
                  not expected: line1\r\nline2\nline3 (Hash=*)
                  actual:       line1\r\nline2\nline3 (Hash=*)
                """);
    }

    #endregion
}
