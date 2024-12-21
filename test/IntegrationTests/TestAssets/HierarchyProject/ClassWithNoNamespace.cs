// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[SuppressMessage("Design", "CA1050:Declare types in namespaces", Justification = "We want to test a class with no namespace")]
public class ClassWithNoNamespace
{
    [TestMethod]
    public void MyMethodUnderTest()
    {
    }
}
