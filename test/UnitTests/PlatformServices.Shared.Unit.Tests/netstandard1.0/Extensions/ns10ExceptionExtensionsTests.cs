// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Extensions
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif
    using System;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

    [TestClass]
    public class ExceptionExtensionsTests
    {
        [TestMethod]
        public void GetExceptionMessageShouldReturnExceptionMessage()
        {
            Exception ex = new Exception("something bad happened");
            Assert.AreEqual("something bad happened", ex.GetExceptionMessage());
        }

        [TestMethod]
        public void GetExceptionMessageShouldReturnInnerExceptionMessageAsWell()
        {
            Exception ex = new Exception("something bad happened", new Exception("inner exception", new Exception("the real exception")));
            var expectedMessage = string.Concat(
                "something bad happened",
                Environment.NewLine,
                "inner exception",
                Environment.NewLine,
                "the real exception");

            Assert.AreEqual(expectedMessage, ex.GetExceptionMessage());
        }
    }
}
