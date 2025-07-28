// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region ThrowAssertFailed tests
    // See https://github.com/dotnet/sdk/issues/25373
    public void ThrowAssertFailedDoesNotThrowIfMessageContainsInvalidStringFormatComposite()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowAssertFailed("name", "{"));
        Verify(ex.Message.Contains("name failed. {"));
    }
    #endregion

    #region ThrowsException tests
    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnNoException()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowsException<ArgumentException>(() => { }));
        Verify(ex.Message.Equals("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ", StringComparison.Ordinal));
    }

    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnWrongException()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowsException<ArgumentException>(() => throw new FormatException()));
        Verify(ex.Message.Equals("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. ", StringComparison.Ordinal));
    }

    public void ThrowsException_FuncArgument_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowsException<ArgumentException>(() => null));
        Verify(ex.Message.Equals("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ", StringComparison.Ordinal));
    }

    public void ThrowsException_FuncArgumentOverloadWithMessage_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowsException<ArgumentException>(() => null, "message"));
        Verify(ex.Message.Equals("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. message", StringComparison.Ordinal));
    }

    public void ThrowsException_FuncArgumentOverloadWithMessagesAndParameters_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.ThrowsException<ArgumentException>(() => null, "message {0}", 1));
        Verify(ex.Message.Equals("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. message 1", StringComparison.Ordinal));
    }
    #endregion

    #region ThrowsExceptionAsync tests.
    public async Task ThrowsExceptionAsyncShouldNotThrowAssertionOnRightException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new ArgumentException();
            });

        // Should not throw an exception.
        await t.ConfigureAwait(false);
    }

    public void ThrowsExceptionAsyncShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false));
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Verify(typeof(AssertFailedException) == innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ", StringComparison.Ordinal));
    }

    public void ThrowsExceptionAsyncShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            });
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. ", StringComparison.Ordinal));
    }

    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false),
            "The world is not on fire.");
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. The world is not on fire.", StringComparison.Ordinal));
    }

    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after.");
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. Happily ever after.", StringComparison.Ordinal));
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullAction()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(null!, null!, null);
            t.Wait();
        }

        Exception ex = VerifyThrows(A);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Verify(typeof(ArgumentNullException) == innerException.GetType());
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullMessage()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(async () => await Task.FromResult(true).ConfigureAwait(false), null!, null);
            t.Wait();
        }

        Exception ex = VerifyThrows(A);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Verify(typeof(ArgumentNullException) == innerException.GetType());
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false),
            "The world is not on fire {0}.{1}-{2}.",
            "ta",
            "da",
            123);
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. The world is not on fire ta.da-123.", StringComparison.Ordinal));
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after. {0} {1}.",
            "The",
            "End");
        Exception ex = VerifyThrows(t.Wait);

        Exception? innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Equals("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. Happily ever after. The End.", StringComparison.Ordinal));
    }
    #endregion

    public void Throws_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => Assert.Throws<ArgumentException>(() => throw new ArgumentNullException());

    public void Throws_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        static void Action() => Assert.Throws<ArgumentException>(() => throw new Exception());
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void Throws_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.Throws<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.Throws<Exception>(GetString, $"Hello {GetString()}");
        Verify(ex is not null);
        Verify(ex2 is not null);
    }

    public void ThrowsExactly_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.ThrowsExactly<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.ThrowsExactly<Exception>(GetString, $"Hello {GetString()}");
        Verify(ex is not null);
        Verify(ex2 is not null);
    }

    public void ThrowsExactly_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        static void Action() => Assert.ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException());
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void ThrowsExactly_WhenExceptionExpectedType_ShouldNotThrow()
        => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException());

    public async Task ThrowsAsync_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => await Assert.ThrowsAsync<ArgumentException>(() => throw new ArgumentNullException());

    public void ThrowsAsync_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsAsync<ArgumentException>(() => throw new Exception());
        Exception ex = VerifyThrows(t.Wait);
        Assert.IsInstanceOfType(ex.InnerException, out AssertFailedException assertFailedException);
        Assert.AreEqual("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.Exception>. ", assertFailedException.Message);
    }

    public void ThrowsExactlyAsync_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsExactlyAsync<ArgumentException>(() => throw new ArgumentNullException());
        Exception ex = VerifyThrows(t.Wait);
        Assert.IsInstanceOfType(ex.InnerException, out AssertFailedException assertFailedException);
        Assert.AreEqual("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.ArgumentNullException>. ", assertFailedException.Message);
    }

    public async Task ThrowsExactlyAsync_WhenExceptionExpectedType_ShouldNotThrow()
        => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => throw new ArgumentNullException());

    public void Throws_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.Throws<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        Verify(!wasBuilderCalled);
    }

    public void Throws_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = VerifyThrows<AssertFailedException>(() => Assert.Throws<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is null);
        Verify(assertFailedEx.Message == "Assert.Throws failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");
    }

    public void Throws_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = VerifyThrows<AssertFailedException>(() => Assert.Throws<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is ArgumentOutOfRangeException { ParamName: "MyParamNameHere" });
        Verify(assertFailedEx.Message == "Assert.Throws failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");
    }

    public void ThrowsExactly_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        Verify(!wasBuilderCalled);
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = VerifyThrows<AssertFailedException>(() => Assert.ThrowsExactly<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is null);
        Verify(assertFailedEx.Message == "Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = VerifyThrows<AssertFailedException>(() => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is ArgumentOutOfRangeException { ParamName: "MyParamNameHere" });
        Verify(assertFailedEx.Message == "Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");
    }

    public async Task ThrowsAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        Verify(!wasBuilderCalled);
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = await VerifyThrowsAsync<AssertFailedException>(async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is null);
        Verify(assertFailedEx.Message == "Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = await VerifyThrowsAsync<AssertFailedException>(async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is ArgumentOutOfRangeException { ParamName: "MyParamNameHere" });
        Verify(assertFailedEx.Message == "Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        Verify(!wasBuilderCalled);
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = await VerifyThrowsAsync<AssertFailedException>(async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is null);
        Verify(assertFailedEx.Message == "Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        AssertFailedException assertFailedEx = await VerifyThrowsAsync<AssertFailedException>(async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        }));

        Verify(wasBuilderCalled);
        Verify(exceptionPassedToBuilder is ArgumentOutOfRangeException { ParamName: "MyParamNameHere" });
        Verify(assertFailedEx.Message == "Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");
    }
}
