// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Text.RegularExpressions;

    using MSTestAdapter.TestUtilities;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class StringAssertTests
    {
        [TestMethod]
        public void ThatShouldReturnAnInstanceOfStringAssert()
        {
            Assert.IsNotNull(TestFrameworkV2.StringAssert.That);
        }

        [TestMethod]
        public void ThatShouldCacheStringAssertInstance()
        {
            Assert.AreEqual(TestFrameworkV2.StringAssert.That, TestFrameworkV2.StringAssert.That);
        }

        [TestMethod]
        public void StringAssertContains()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string notInString = "I'm not in the string above";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.Contains(actual, notInString));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.Contains failed");
        }

        [TestMethod]
        public void StringAssertStartsWith()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string notInString = "I'm not in the string above";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.StartsWith(actual, notInString));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.StartsWith failed");
        }

        [TestMethod]
        public void StringAssertEndsWith()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string notInString = "I'm not in the string above";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.EndsWith(actual, notInString));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.EndsWith failed");
        }

        [TestMethod]
        public void StringAssertDoesNotMatch()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            Regex doesMatch = new Regex("quick brown fox");
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.DoesNotMatch(actual, doesMatch));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.DoesNotMatch failed");
        }

        [TestMethod]
        public void StringAssertContainsIgnoreCase()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string inString = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.Contains(actual, inString, StringComparison.OrdinalIgnoreCase));
            Assert.IsNull(ex);
        }

        [TestMethod]
        public void StringAssertStartsWithIgnoreCase()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string inString = "THE QUICK";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.StartsWith(actual, inString, StringComparison.OrdinalIgnoreCase));
            Assert.IsNull(ex);
        }

        [TestMethod]
        public void StringAssertEndsWithIgnoreCase()
        {
            string actual = "The quick brown fox jumps over the lazy dog.";
            string inString = "LAZY DOG.";
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.EndsWith(actual, inString, StringComparison.OrdinalIgnoreCase));
            Assert.IsNull(ex);
        }

        [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
        public void StringAssertContainsDoesNotThrowFormatException()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.Contains(":-{", "x"));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.Contains failed");
        }

        [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
        public void StringAssertContainsDoesNotThrowFormatExceptionWithArguments()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.Contains("{", "x", "message {0}", "arg"));
            Assert.IsNotNull(ex);
            TestFrameworkV1.StringAssert.Contains(ex.Message, "StringAssert.Contains failed");
        }

        [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
        public void StringAssertContainsFailsIfMessageIsInvalidStringFormatComposite()
        {
            var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.StringAssert.Contains("a", "b", "message {{0}", "arg"));
            Assert.IsNotNull(ex);
            Assert.AreEqual(typeof(FormatException), ex.GetType());
        }
    }
}
