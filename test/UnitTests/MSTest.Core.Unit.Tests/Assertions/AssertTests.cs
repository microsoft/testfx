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

[TestClass]
public partial class AssertTests
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

    #region BuildUserMessage tests
    [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageThrowsWhenMessageContainsInvalidStringFormatComposite()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.BuildUserMessage("{", "arg"));

        Assert.IsNotNull(ex);
        Assert.AreEqual(typeof(FormatException), ex.GetType());
    }

    [TestMethod] // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        string message = TestFrameworkV2.Assert.BuildUserMessage("{");
        Assert.AreEqual("{", message);
    }
    #endregion
}
