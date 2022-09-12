// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using System.Globalization;
using System.Threading.Tasks;
using MSTestAdapter.TestUtilities;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

public partial class AssertTests
{
    #region ThrowAssertFailed tests
    [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
    public void ThrowAssertFailedDoesNotThrowIfMessageContainsInvalidStringFormatComposite()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.ThrowAssertFailed("name", "{"));

        Assert.IsNotNull(ex);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), ex.GetType());
        StringAssert.Contains(ex.Message, "name failed. {");
    }
    #endregion

    #region ThrowsException tests

    [TestMethod]
    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnNoException()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.ThrowsException<ArgumentException>(() => { }));

        Assert.IsNotNull(ex);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), ex.GetType());
        StringAssert.Contains(ex.Message, "Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected.");
    }

    [TestMethod]
    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnWrongException()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.ThrowsException<ArgumentException>(
             () =>
             {
                 throw new FormatException();
             }));

        Assert.IsNotNull(ex);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), ex.GetType());
        StringAssert.Contains(ex.Message, "Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected.");
    }

    #endregion

    #region ThrowsExceptionAsync tests.

    [TestMethod]
    public async Task ThrowsExceptionAsyncShouldNotThrowAssertionOnRightException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new ArgumentException();
            });

        // Should not throw an exception.
        await t.ConfigureAwait(false);
    }

    [TestMethod]
    public void ThrowsExceptionAsyncShouldThrowAssertionOnNoException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
            });
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected.");
    }

    [TestMethod]
    public void ThrowsExceptionAsyncShouldThrowAssertionOnWrongException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            });
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected.");
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnNoException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
            },
            "The world is not on fire.");
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected. The world is not on fire.");
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnWrongException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after.");
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected. Happily ever after.");
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullAction()
    {
        static void a()
        {
            Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(null, null, null);
            t.Wait();
        }
        ActionUtility.ActionShouldThrowInnerExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullMessage()
    {
        static void a()
        {
            Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(async () => { await Task.FromResult(true).ConfigureAwait(false); }, null, null);
            t.Wait();
        }
        ActionUtility.ActionShouldThrowInnerExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnNoException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
            },
            "The world is not on fire {0}.{1}-{2}.",
            "ta",
            "da",
            123);
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected. The world is not on fire ta.da-123.");
    }

    [TestMethod]
    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnWrongException()
    {
        Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after. {0} {1}.",
            "The",
            "End");
        var ex = ActionUtility.PerformActionAndReturnException(() => t.Wait());

        Assert.IsNotNull(ex);

        var innerException = ex.InnerException;

        Assert.IsNotNull(innerException);
        Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), innerException.GetType());
        StringAssert.Contains(innerException.Message, "Assert.ThrowsException failed. Threw exception FormatException, but exception ArgumentException was expected. Happily ever after. The End.");
    }

    #endregion
}
