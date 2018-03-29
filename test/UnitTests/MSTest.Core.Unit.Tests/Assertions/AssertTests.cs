// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Threading.Tasks;
    using MSTestAdapter.TestUtilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class AssertTests
    {
        #region That tests

        [TestMethod]
        public void ThatShouldReturnAnInstanceOfAssert()
        {
            Assert.IsNotNull(TestFrameworkV2.Assert.That);
        }

        [TestMethod]
        public void ThatShouldCacheAssertInstance()
        {
            Assert.AreEqual(TestFrameworkV2.Assert.That, TestFrameworkV2.Assert.That);
        }

        #endregion

        #region ThrowsException tests

        [TestMethod]
        public void ThrowsExceptionWithLamdaExpressionsShouldThrowAssertionOnNoException()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.ThrowsException<ArgumentException>(() => { }));

            Assert.IsNotNull(ex);
            Assert.AreEqual(typeof(TestFrameworkV2.AssertFailedException), ex.GetType());
            StringAssert.Contains(ex.Message, "Assert.ThrowsException failed. No exception thrown. ArgumentException exception was expected.");
        }

        [TestMethod]
        public void ThrowsExceptionWithLamdaExpressionsShouldThrowAssertionOnWrongException()
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
                        await Task.Delay(5);
                        throw new ArgumentException();
                    });

            // Should not throw an exception.
            await t;
        }

        [TestMethod]
        public void ThrowsExceptionAsyncShouldThrowAssertionOnNoException()
        {
            Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
                async () =>
                {
                    await Task.Delay(5);
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
                    await Task.Delay(5);
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
                        await Task.Delay(5);
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
                    await Task.Delay(5);
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
            Action a = () =>
                {
                    Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(null, null, null);
                    t.Wait();
                };
            ActionUtility.ActionShouldThrowInnerExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullMessage()
        {
            Action a = () =>
            {
                Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(async () => { await Task.FromResult(true); }, null, null);
                t.Wait();
            };
            ActionUtility.ActionShouldThrowInnerExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnNoException()
        {
            Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(
                async () =>
                {
                    await Task.Delay(5);
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
                    await Task.Delay(5);
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

        #region ReplaceNullChars tests

        [TestMethod]
        public void ReplaceNullCharsShouldReturnStringIfNullOrEmpty()
        {
            Assert.IsNull(TestFrameworkV2.Assert.ReplaceNullChars(null));
            Assert.AreEqual(string.Empty, TestFrameworkV2.Assert.ReplaceNullChars(string.Empty));
        }

        [TestMethod]
        public void ReplaceNullCharsShouldReplaceNullCharsInAString()
        {
            Assert.AreEqual("The quick brown fox \\0 jumped over the la\\0zy dog\\0", TestFrameworkV2.Assert.ReplaceNullChars("The quick brown fox \0 jumped over the la\0zy dog\0"));
        }

        #endregion

        #region InstanceOfType tests

        [TestMethod]
        public void InstanceOfTypeShouldFailWhenValueIsNull()
        {
            Action action = () => TestFrameworkV2.Assert.IsInstanceOfType(null, typeof(AssertTests));
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void InstanceOfTypeShouldFailWhenTypeIsNull()
        {
            Action action = () => TestFrameworkV2.Assert.IsInstanceOfType(5, null);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void InstanceOfTypeShouldPassOnSameInstance()
        {
            TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(int));
        }

        [TestMethod]
        public void InstanceOfTypeShouldPassOnHigherInstance()
        {
            TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(object));
        }

        [TestMethod]
        public void InstanceNotOfTypeShouldFailWhenValueIsNull()
        {
            Action action = () => TestFrameworkV2.Assert.IsNotInstanceOfType(null, typeof(AssertTests));
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
        {
            Action action = () => TestFrameworkV2.Assert.IsNotInstanceOfType(5, null);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void InstanceNotOfTypeShouldPassOnWrongInstance()
        {
            TestFrameworkV2.Assert.IsNotInstanceOfType(5L, typeof(int));
        }

        [TestMethod]
        public void InstanceNotOfTypeShouldPassOnSubInstance()
        {
            TestFrameworkV2.Assert.IsNotInstanceOfType(new object(), typeof(int));
        }

        #endregion
    }
}
