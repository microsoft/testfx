// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region ThrowAssertFailed tests
    // See https://github.com/dotnet/sdk/issues/25373
    public void ThrowAssertFailedDoesNotThrowIfMessageContainsInvalidStringFormatComposite()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowAssertFailed("name", "{"));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("name failed. {"));
    }
    #endregion

    #region ThrowsException tests
    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnNoException()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowsException<ArgumentException>(() => { }));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected."));
    }

    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnWrongException()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowsException<ArgumentException>(() => throw new FormatException()));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected."));
    }

    public void ThrowsException_FuncArgument_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowsException<ArgumentException>(() => null));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected."));
    }

    public void ThrowsException_FuncArgumentOverloadWithMessage_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowsException<ArgumentException>(() => null, "message"));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected."));
    }

    public void ThrowsException_FuncArgumentOverloadWithMessagesAndParameters_AllowsToReturnNull()
    {
        Exception ex = VerifyThrows(() => Assert.ThrowsException<ArgumentException>(() => null, "message {0}", 1));

        Verify(ex is not null);
        Verify(typeof(AssertFailedException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected."));
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

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Verify(typeof(AssertFailedException) == innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected."));
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

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected."));
    }

    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false),
            "The world is not on fire.");
        Exception ex = VerifyThrows(t.Wait);

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected. The world is not on fire."));
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

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected. Happily ever after."));
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullAction()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(null, null, null);
            t.Wait();
        }

        Exception ex = VerifyThrows(A);

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Verify(typeof(ArgumentNullException) == innerException.GetType());
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullMessage()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(async () => await Task.FromResult(true).ConfigureAwait(false), null, null);
            t.Wait();
        }

        Exception ex = VerifyThrows(A);

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

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

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected. The world is not on fire ta.da-123."));
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

        Verify(ex is not null);

        Exception innerException = ex.InnerException;

        Verify(innerException is not null);
        Assert.AreEqual(typeof(AssertFailedException), innerException.GetType());
        Verify(innerException.Message.Contains("Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected. Happily ever after. The End."));
    }
    #endregion
}
