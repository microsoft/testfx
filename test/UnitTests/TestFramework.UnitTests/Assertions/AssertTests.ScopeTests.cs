// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
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
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.AreEqual(1, 2);
            }
        };

        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreEqual failed. Expected:<1>. Actual:<2>. 'expected' expression: '1', 'actual' expression: '2'.");
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
        innerException.InnerExceptions[0].Message.Should().Be("Assert.AreEqual failed. Expected:<1>. Actual:<2>. 'expected' expression: '1', 'actual' expression: '2'.");
        innerException.InnerExceptions[1].Message.Should().Be("Assert.IsTrue failed. 'condition' expression: 'false'.");
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
            .WithMessage("Assert.AreEqual failed. Expected:<1>. Actual:<2>. 'expected' expression: '1', 'actual' expression: '2'.");

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

    public void Scope_AssertIsNotNull_IsHardFailure()
    {
        object? value = null;
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsNotNull(value);
                Assert.IsTrue(true); // should not be reached
            }
        };

        // Assert.IsNotNull is a hard assertion — it throws immediately, even within a scope.
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotNull failed. 'value' expression: 'value'.");
    }

    public void Scope_AssertIsInstanceOfType_IsHardFailure()
    {
        object value = "hello";
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsInstanceOfType(value, typeof(int));
                Assert.IsTrue(true); // should not be reached
            }
        };

        // Assert.IsInstanceOfType is a hard assertion — it throws immediately, even within a scope.
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsInstanceOfType failed. 'value' expression: 'value'. Expected type:<System.Int32>. Actual type:<System.String>.");
    }

    public void Scope_AssertIsExactInstanceOfType_IsHardFailure()
    {
        object value = "hello";
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.IsExactInstanceOfType(value, typeof(object));
                Assert.IsTrue(true); // should not be reached
            }
        };

        // Assert.IsExactInstanceOfType is a hard assertion — it throws immediately, even within a scope.
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsExactInstanceOfType failed. 'value' expression: 'value'. Expected exact type:<System.Object>. Actual type:<System.String>.");
    }

    public void Scope_AssertContainsSingle_IsHardFailure()
    {
        int[] items = [1, 2, 3];
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.ContainsSingle(items);
                Assert.IsTrue(true); // should not be reached
            }
        };

        // Assert.ContainsSingle is a hard assertion — it throws immediately, even within a scope.
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 3 element(s). 'collection' expression: 'items'.");
    }
}
