// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LifecycleInheritance;

[TestClass]
public class TestClassBaseEndOfClass
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassInit(TestContext testContext) => Console.WriteLine("TestClassBaseEndOfClass: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void BaseClassCleanup() => Console.WriteLine("TestClassBaseEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassBaseEndOfAssembly
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassInit(TestContext testContext) => Console.WriteLine("TestClassBaseEndOfAssembly: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfAssembly)]
    public static void BaseClassCleanup() => Console.WriteLine("TestClassBaseEndOfAssembly: ClassCleanup");
}

[TestClass]
public class TestClassIntermediateEndOfClassBaseEndOfClass : TestClassBaseEndOfClass
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInit(TestContext testContext) => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void IntermediateClassCleanup() => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassIntermediateEndOfClassBaseEndOfAssembly : TestClassBaseEndOfAssembly
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInit(TestContext testContext) => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void IntermediateClassCleanup() => Console.WriteLine("TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassCleanup");
}

[TestClass]
public class TestClassIntermediateEndOfAssemblyBaseEndOfAssembly : TestClassBaseEndOfAssembly
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInit(TestContext testContext) => Console.WriteLine("TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void IntermediateClassCleanup() => Console.WriteLine("TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassCleanup");
}

[TestClass]
public class TestClassIntermediateEndOfAssemblyBaseEndOfClass : TestClassBaseEndOfClass
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInit(TestContext testContext) => Console.WriteLine("TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassInitialize");

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void IntermediateClassCleanup() => Console.WriteLine("TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfClass : TestClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfClass: TestMethod");
}

[TestClass]
public class TestClassDerived_EndOfAssembly : TestClassBaseEndOfAssembly
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfAssembly: TestMethod");
}

[TestClass]
public class TestClassDerivedEndOfClass_EndOfClassEndOfClass : TestClassIntermediateEndOfClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfClass: TestMethod");

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfClassEndOfClass : TestClassIntermediateEndOfClassBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfClassEndOfClass: TestMethod");
}

[TestClass]
public class TestClassDerivedEndOfClass_EndOfClassEndOfAssembly : TestClassIntermediateEndOfClassBaseEndOfAssembly
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfAssembly: TestMethod");

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfClassEndOfAssembly: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfClassEndOfAssembly : TestClassIntermediateEndOfClassBaseEndOfAssembly
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfClassEndOfAssembly: TestMethod");
}

[TestClass]
public class TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass : TestClassIntermediateEndOfAssemblyBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass: TestMethod");

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfAssemblyEndOfClass : TestClassIntermediateEndOfAssemblyBaseEndOfClass
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfAssemblyEndOfClass: TestMethod");
}

[TestClass]
public class TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly : TestClassIntermediateEndOfAssemblyBaseEndOfAssembly
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly: TestMethod");

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup() => Console.WriteLine("TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly: ClassCleanup");
}

[TestClass]
public class TestClassDerived_EndOfAssemblyEndOfAssembly : TestClassIntermediateEndOfAssemblyBaseEndOfAssembly
{
    [TestMethod]
    public void TestMethod() => Console.WriteLine("TestClassDerived_EndOfAssemblyEndOfAssembly: TestMethod");
}
