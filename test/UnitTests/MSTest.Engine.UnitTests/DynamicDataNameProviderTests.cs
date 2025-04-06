// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Framework.UnitTests;

[TestClass]
public class DynamicDataNameProviderTests
{
    [TestMethod]
    public void NullTranslatesToNullString()
    {
        // Comment in DynamicDataAttribute says:
        //  We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
        //  so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
        //  you will get empty string while with the call you will get "null,a".
        //
        // check that this is still true:
        string fragment = DynamicDataNameProvider.GetUidFragment(["parameter1", "parameter2"], [null, "a"], 0);
        Assert.AreEqual("(parameter1: null, parameter2: a)[0]", fragment);
    }

    [TestMethod]
    public void ParameterMismatchShowsDataInMessage()
    {
        // Comment in DynamicDataAttribute says:
        //  We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
        //  so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
        //  you will get empty string while with the call you will get "null,a".
        //
        // check that this is still true:
        ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => DynamicDataNameProvider.GetUidFragment(["parameter1"], [null, "a"], 0));
        Assert.AreEqual("Parameter count mismatch. The provided data (null, a) have 2 items, but there are 1 parameters.", exception.Message);
    }
}
