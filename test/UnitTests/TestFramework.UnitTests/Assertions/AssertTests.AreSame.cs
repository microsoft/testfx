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
            Assert.AreSame failed.
            Expected references to be the same.
              expected (new object()): <System.Object> (HashCode=*)
              actual (new object()): <System.Object> (HashCode=*)
            """);
    }

    public void AreSame_StringMessage_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message");
    }

    public void AreSame_StringMessage_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object(), "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame failed. User-provided message
            Expected references to be the same.
              expected (new object()): <System.Object> (HashCode=*)
              actual (new object()): <System.Object> (HashCode=*)
            """);
    }

    public void AreSame_InterpolatedString_PassSameObject_ShouldPass()
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
            Assert.AreSame failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString()*
            Expected references to be the same.
              expected (new object()): <System.Object> (HashCode=*)
              actual (new object()): <System.Object> (HashCode=*)
            """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void AreNotSame_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object());

    public void AreSame_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1);
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame failed.
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (HashCode=*)
              actual: 1 (HashCode=*)
            """);
    }

    public void AreSame_StringMessage_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame failed. User-provided message
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (HashCode=*)
              actual: 1 (HashCode=*)
            """);
    }

    public void AreSame_InterpolatedString_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreSame failed. User-provided message System.Object
            Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual().
              expected: 1 (HashCode=*)
              actual: 1 (HashCode=*)
            """);
    }

    public void AreNotSame_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o);
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreNotSame failed.
            Expected references to be different.
              notExpected (o): <System.Object>
              actual (o): <System.Object>
            """);
    }

    public void AreNotSame_StringMessage_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message");

    public void AreNotSame_StringMessage_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o, "User-provided message");
        action.Should().Throw<Exception>().WithMessage("""
            Assert.AreNotSame failed. User-provided message
            Expected references to be different.
              notExpected (o): <System.Object>
              actual (o): <System.Object>
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
            Assert.AreNotSame failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString()*
            Expected references to be different.
              notExpected (o): DummyClassTrackingToStringCalls
              actual (o): DummyClassTrackingToStringCalls
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
                Assert.AreSame failed.
                Expected references to be the same.
                  expected (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): <System.Object> (HashCode=*)
                  actual (new object()): <System.Object> (HashCode=*)
                """);
    }

    public void AreSame_WithLongToStringValue_ShouldTruncateValue()
    {
        Action action = () => Assert.AreSame(new ObjectWithLongToString(), new ObjectWithLongToString());
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.AreSame failed.
                Expected references to be the same.
                  expected (new ObjectWithLongToString()): {new string('L', 256)}... (300 chars) (HashCode=*)
                  actual (new ObjectWithLongToString()): {new string('L', 256)}... (300 chars) (HashCode=*)
                """);
    }

    public void AreSame_WithNewlineInToString_ShouldEscapeNewlines()
    {
        Action action = () => Assert.AreSame(new ObjectWithNewlineToString(), new ObjectWithNewlineToString());
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreSame failed.
                Expected references to be the same.
                  expected (new ObjectWithNewlineToString()): line1\r\nline2\nline3 (HashCode=*)
                  actual (new ObjectWithNewlineToString()): line1\r\nline2\nline3 (HashCode=*)
                """);
    }

    public void AreNotSame_WithLongExpression_ShouldTruncateExpression()
    {
        object aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = new object();

        Action action = () => Assert.AreNotSame(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreNotSame failed.
                Expected references to be different.
                  notExpected (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): <System.Object>
                  actual (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): <System.Object>
                """);
    }

    public void AreNotSame_WithLongToStringValue_ShouldTruncateValue()
    {
        var obj = new ObjectWithLongToString();

        Action action = () => Assert.AreNotSame(obj, obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.AreNotSame failed.
                Expected references to be different.
                  notExpected (obj): {new string('L', 256)}... (300 chars)
                  actual (obj): {new string('L', 256)}... (300 chars)
                """);
    }

    public void AreNotSame_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var obj = new ObjectWithNewlineToString();

        Action action = () => Assert.AreNotSame(obj, obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.AreNotSame failed.
                Expected references to be different.
                  notExpected (obj): line1\r\nline2\nline3
                  actual (obj): line1\r\nline2\nline3
                """);
    }

    #endregion
}
