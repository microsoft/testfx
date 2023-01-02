// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowAttributeTestProject;

[TestCategory("GetCustomDisplayName")]
[TestClass]
public class DataRowTests_DisplayNemeMethodAndDeclaringType
{
    [TestMethod]
    [DataRow(1,DisplayNameMethod = "GetCustomDisplayName")]
    public void DataRowTest_WithDisplayNameMethod_passes(int number)
    {
        Assert.AreEqual(number,1);
    }

    [TestMethod]
    [DataRow(1, DisplayNameMethod = "GetCustomDisplayNameOtherType", DisplayNameMethodDeclaringType =typeof(Dummy))]
    public void DataRowTest_WithDisplayNameMethodOtherType_Passes(int number)
    {
        Assert.AreEqual(number, 1);
    }

    [TestMethod]
    [DataRow(1, DisplayNameMethod = "GetCustomDisplayNamePrivate", DisplayNameMethodDeclaringType = typeof(Dummy))]
    public void DataRowTest_WithDisplayNameMethodPrivate_Failes(int number)
    {
        Assert.AreEqual(number, 1);
    }

    public static string GetCustomDisplayName(MethodInfo methodInfo, object[] data)
    {
        return string.Format("Custom {0} with {1} parameters", methodInfo.Name, data.Length);
    }

    private static string GetCustomDisplayNamePrivate(MethodInfo methodInfo, object[] data)
    {
        return string.Format("Custom {0} with {1} parameters", methodInfo.Name, data.Length);
    }

    private class Dummy
    {
        public static string GetCustomDisplayNameOtherType(MethodInfo methodInfo, object[] data)
        {
            return string.Format("Custom {0} with {1} parameters", methodInfo.Name, data.Length);
        }
    }
}

