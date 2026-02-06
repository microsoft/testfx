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
            .WithMessage("*Assert.AreEqual failed*");
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

        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Assert.AreEqual failed")
            .And.Contain("Assert.IsTrue failed");
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

    public void Scope_NestedScopes_InnerScopeCollectsItsOwnErrors()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.AreEqual(1, 2); // outer error

                Action innerAction = () =>
                {
                    using (Assert.Scope())
                    {
                        Assert.IsTrue(false); // inner error
                    }
                };

                innerAction.Should().Throw<AssertFailedException>()
                    .WithMessage("*Assert.IsTrue failed*");
            }
        };

        // Outer scope should only contain the outer error
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Assert.AreEqual failed");
    }

    public void Scope_DoubleDispose_DoesNotThrowTwice()
    {
        IDisposable scope = Assert.Scope();
        Assert.AreEqual(1, 2);

        Action firstDispose = () => scope.Dispose();
        firstDispose.Should().Throw<AssertFailedException>();

        // Second dispose should be a no-op
        Action secondDispose = () => scope.Dispose();
        secondDispose.Should().NotThrow();
    }

    public void Scope_AssertFail_IsCollected()
    {
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.Fail("first failure");
                Assert.Fail("second failure");
            }
        };

        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("first failure")
            .And.Contain("second failure");
    }
}
