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
                Task t = TestFrameworkV2.Assert.ThrowsExceptionAsync<ArgumentException>(async () => { await Task.FromResult(true).ConfigureAwait(false); }, null, null);
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

        #region Nullable Booleans tests.

        [TestMethod]
        public void IsFalseNullableBooleansShouldFailWithNull()
        {
            bool? nullBool = null;
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsFalse(nullBool));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "Assert.IsFalse failed");
        }

        [TestMethod]
        public void IsTrueNullableBooleansShouldFailWithNull()
        {
            bool? nullBool = null;

            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsTrue(nullBool));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "Assert.IsTrue failed");
        }
        #endregion

        #region AreNotEqual tests.

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualType()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(null, null);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(null, null, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualString()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual("A", "A");
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual("A", "A", "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual("A", "a", true);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualInt()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(1, 1);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1, 1, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualLong()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(1L, 1L);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1L, 1L, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(1L, 2L, 1L);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDecimal()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDouble()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.2, 0.1);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenFloatDouble()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }
        #endregion

        #region AreEqual tests.
        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualType()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(null, "string");
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(null, "string", "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualString()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual("A", "a");
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualStringWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual("A", "a", "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual("A", "a", false);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualInt()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(1, 2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualIntWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1, 2, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualLong()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(1L, 2L);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualLongWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1L, 2L, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualLongWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(10L, 20L, 5L);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDouble()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(0.1, 0.2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, 0.05);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDecimal()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, 0.05M);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenFloatDouble()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        [TestMethod]
        public void AreEqualShouldFailWhenFloatDoubleWithMessage()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, "A Message"));
            Assert.IsNotNull(ex);
            StringAssert.Contains(ex.Message, "A Message");
        }

        [TestMethod]
        public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
        {
            Action action = () => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, 50E-2);
            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
        }

        #endregion
    }
}
