// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    // Shared expected message for the structured failure produced by `Assert.AreEqual(1, 2)`.
    // Used across the scope soft-failure tests below to reduce churn when the structured-message
    // format evolves; update this single constant rather than every assertion site.
    private const string AreEqual1And2StructuredMessage = """
        Assertion failed. Expected values to be equal.

        expected: 1
        actual:   2

        Assert.AreEqual(1, 2)
        """;

    public void Scope_NoFailures_DoesNotThrow()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsTrue(true);
                Assert.AreEqual(1, 1);
            }
        };

        action.Should().NotThrow();
    }

    public void Scope_SingleFailure_ThrowsOnDispose()
    {
        IDisposable scope = Assert.Scope();
        Assert.AreEqual(1, 2);
        Action action = () => scope.Dispose();

        action.Should().Throw<AssertFailedException>()
            .WithMessage(AreEqual1And2StructuredMessage);
    }

    public void Scope_MultipleFailures_CollectsAllErrors()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.AreEqual(1, 2);
                Assert.IsTrue(false);
            }
        };

        AggregateException innerException = action.Should().Throw<AssertFailedException>()
            .WithMessage("2 assertion(s) failed within the assert scope.")
            .WithInnerException<AggregateException>()
            .Which;

        innerException.InnerExceptions.Should().HaveCount(2);
        innerException.InnerExceptions[0].Message.Should().Be(AreEqual1And2StructuredMessage);
        innerException.InnerExceptions[1].Message.Should().Be(
            """
            Assertion failed. Expected condition to be true.

            actual: false

            Assert.IsTrue(false)
            """);
    }

    public void Scope_AfterDispose_AssertionsThrowNormally()
    {
        // Completing a scope should restore normal behavior.
        try
        {
            using (Assert.Scope())
            {
                // intentionally empty scope
            }
        }
        catch
        {
            // ignore
        }

        Action action = () => Assert.IsTrue(false);
        action.Should().Throw<AssertFailedException>();
    }

    public void Scope_NestedScope_ThrowsInvalidOperationException()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                using (Assert.Scope())
                {
                    // Should never reach here
                }
            }
        };

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Nested assert scopes are not allowed. Dispose the current scope before creating a new one.");
    }

    public void Scope_DoubleDispose_DoesNotThrowTwice()
    {
        IDisposable scope = Assert.Scope();
        Assert.AreEqual(1, 2);

        Action firstDispose = () => scope.Dispose();
        firstDispose.Should().Throw<AssertFailedException>()
            .WithMessage(AreEqual1And2StructuredMessage);

        // Second dispose should be a no-op
        Action secondDispose = () => scope.Dispose();
        secondDispose.Should().NotThrow();
    }

    public void Scope_AssertFail_IsHardFailure()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.Fail("first failure");
                Assert.Fail("second failure");
            }
        };

        // Assert.Fail is a hard assertion — it throws immediately, even within a scope.
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.Fail failed. first failure");
    }

    public void Scope_AssertIsNotNull_IsSoftFailure()
    {
        object? value = null;
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsNotNull(value);
                Assert.AreEqual(1, 2);
            }
        };

        // Assert.IsNotNull is a soft assertion — failure is collected within a scope.
        AggregateException innerException = action.Should().Throw<AssertFailedException>()
            .WithMessage("2 assertion(s) failed within the assert scope.")
            .WithInnerException<AggregateException>()
            .Which;

        innerException.InnerExceptions.Should().HaveCount(2);
        innerException.InnerExceptions[0].Message.Should().Be(
            """
            Assertion failed. Expected value to not be null.

            actual: null

            Assert.IsNotNull(value)
            """);
        innerException.InnerExceptions[1].Message.Should().Be(AreEqual1And2StructuredMessage);
    }

    public void Scope_AssertIsInstanceOfType_IsSoftFailure()
    {
        object value = "hello";
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsInstanceOfType(value, typeof(int));
                Assert.AreEqual(1, 2);
            }
        };

        // Assert.IsInstanceOfType is a soft assertion — failure is collected within a scope.
        AggregateException innerException = action.Should().Throw<AssertFailedException>()
            .WithMessage("2 assertion(s) failed within the assert scope.")
            .WithInnerException<AggregateException>()
            .Which;

        innerException.InnerExceptions.Should().HaveCount(2);
        innerException.InnerExceptions[0].Message.Should().Be(
            """
            Assertion failed. Expected value to be of type Int32 (or derived).

            expected type: System.Int32 (or derived)
            actual type:   System.String

            Assert.IsInstanceOfType(value)
            """);
        innerException.InnerExceptions[1].Message.Should().Be(AreEqual1And2StructuredMessage);
    }

    public void Scope_AssertIsExactInstanceOfType_IsSoftFailure()
    {
        object value = "hello";
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsExactInstanceOfType(value, typeof(object));
                Assert.AreEqual(1, 2);
            }
        };

        // Assert.IsExactInstanceOfType is a soft assertion — failure is collected within a scope.
        AggregateException innerException = action.Should().Throw<AssertFailedException>()
            .WithMessage("2 assertion(s) failed within the assert scope.")
            .WithInnerException<AggregateException>()
            .Which;

        innerException.InnerExceptions.Should().HaveCount(2);
        innerException.InnerExceptions[0].Message.Should().Be(
            """
            Assertion failed. Expected value to be exactly of type Object.

            expected type: System.Object
            actual type:   System.String

            Assert.IsExactInstanceOfType(value)
            """);
        innerException.InnerExceptions[1].Message.Should().Be(AreEqual1And2StructuredMessage);
    }

    public void Scope_AssertContainsSingle_IsSoftFailure()
    {
        int[] items = [1, 2, 3];
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.ContainsSingle(items);
                Assert.AreEqual(1, 2);
            }
        };

        // Assert.ContainsSingle is a soft assertion — failure is collected within a scope.
        AggregateException innerException = action.Should().Throw<AssertFailedException>()
            .WithMessage("2 assertion(s) failed within the assert scope.")
            .WithInnerException<AggregateException>()
            .Which;

        innerException.InnerExceptions.Should().HaveCount(2);
        innerException.InnerExceptions[0].Message.Should().Be(
            """
            Assertion failed. Expected collection to contain exactly one element.

            expected count: 1
            actual count:   3

            Assert.ContainsSingle(items)
            """);
        innerException.InnerExceptions[1].Message.Should().Be(AreEqual1And2StructuredMessage);
    }
}
