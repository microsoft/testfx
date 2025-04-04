// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ClassLibForTestProjectWithBrokenTestClass;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProjectWithBrokenTestClass;

[TestClass]
// This test class references the base class which lives in a different assembly.
// We make sure the assembly is not available next to the test dll, and so when AssemblyEnumerator goes to
// to load the type, we will show a warning, that TestClass1 cannot be loaded.
//
// These warnings should be shown no matter if we run on .net framework in appdomain or not.
public class TestClass1 : BaseClass
{
    public void TestMethod1(string a)
    {
    }
}
