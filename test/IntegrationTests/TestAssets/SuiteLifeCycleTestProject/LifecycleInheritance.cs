// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LifecycleInheritance;

[TestClass]
public class TestClassBaseEndOfClass
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassInit(TestContext testContext) => Console.WriteLine("TestClassBaseEndOfClass: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassCleanup() => Console.WriteLine("TestClassBaseEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassIntermediateEndOfClassBaseEndOfClass : TestClassBaseEndOfClass
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInit(TestContext testContext) => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassCleanup() => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfClass : TestClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfClass: TestMethod");
}

[TestClass]
public class TestClassDerivedEndOfClass_EndOfClassEndOfClass : TestClassIntermediateEndOfClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfClass: TestMethod");

    [ClassCleanup]
    public static void ClassCleanup() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfClassEndOfClass : TestClassIntermediateEndOfClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfClassEndOfClass: TestMethod");
}
