// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

#if NETCOREAPP1_1
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UnitTestOutcome = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
#endif

using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Moq;
using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

[TestClass]
public class TestContextImplementationTests
{
    private Mock<ITestMethod> testMethod;

    private IDictionary<string, object> properties;

    private TestContextImplementation testContextImplementation;

    [TestInitialize]
    public void TestInit()
    {
        testMethod = new Mock<ITestMethod>();
        properties = new Dictionary<string, object>();
    }

    [TestMethod]
    public void TestContextConstructorShouldInitializeProperties()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.IsNotNull(testContextImplementation.Properties);
    }

    [TestMethod]
    public void TestContextConstructorShouldInitializeDefaultProperties()
    {
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        testMethod.Setup(tm => tm.Name).Returns("M");

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.IsNotNull(testContextImplementation.Properties);

        CollectionAssert.Contains(
            testContextImplementation.Properties,
            new KeyValuePair<string, object>("FullyQualifiedTestClassName", "A.C.M"));
        CollectionAssert.Contains(
            testContextImplementation.Properties,
            new KeyValuePair<string, object>("TestName", "M"));
    }

    [TestMethod]
    public void CurrentTestOutcomeShouldReturnDefaultOutcome()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.AreEqual(UnitTestOutcome.Failed, testContextImplementation.CurrentTestOutcome);
    }

    [TestMethod]
    public void CurrentTestOutcomeShouldReturnOutcomeSet()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

        Assert.AreEqual(UnitTestOutcome.InProgress, testContextImplementation.CurrentTestOutcome);
    }

    [TestMethod]
    public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
    {
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.AreEqual("A.C.M", testContextImplementation.FullyQualifiedTestClassName);
    }

    [TestMethod]
    public void TestNameShouldReturnTestMethodsName()
    {
        testMethod.Setup(tm => tm.Name).Returns("M");

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.AreEqual("M", testContextImplementation.TestName);
    }

    [TestMethod]
    public void PropertiesShouldReturnPropertiesPassedToTestContext()
    {
        var property1 = new KeyValuePair<string, object>("IntProperty", 1);
        var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

        properties.Add(property1);
        properties.Add(property2);

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        CollectionAssert.Contains(testContextImplementation.Properties, property1);
        CollectionAssert.Contains(testContextImplementation.Properties, property2);
    }

    [TestMethod]
    public void ContextShouldReturnTestContextObject()
    {
        testMethod.Setup(tm => tm.Name).Returns("M");

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        Assert.IsNotNull(testContextImplementation.Context);
        Assert.AreEqual("M", testContextImplementation.Context.TestName);
    }

    [TestMethod]
    public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
    {
        testMethod.Setup(tm => tm.Name).Returns("M");

        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);
        Assert.IsTrue(testContextImplementation.TryGetPropertyValue("TestName", out object propValue));
        Assert.AreEqual("M", propValue);
    }

    [TestMethod]
    public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);
        Assert.IsFalse(testContextImplementation.TryGetPropertyValue("Random", out object propValue));
        Assert.IsNull(propValue);
    }

    [TestMethod]
    public void AddPropertyShouldAddPropertiesToThePropertyBag()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

        CollectionAssert.Contains(
            testContextImplementation.Properties,
            new KeyValuePair<string, object>("SomeNewProperty", "SomeValue"));
    }

    [TestMethod]
    public void WriteShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        testContextImplementation.Write("{0} Testing write", 1);
        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        testContextImplementation.Write("{0} Testing \0 write \0", 1);
        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        stringWriter.Dispose();
        testContextImplementation.Write("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        testContextImplementation.Write("{0} Testing write", 1);
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        testContextImplementation.Write("1 Testing write");
        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        testContextImplementation.Write("1 Testing \0 write \0");
        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        stringWriter.Dispose();
        testContextImplementation.Write("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        testContextImplementation.Write("1 Testing write");
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriterForReturnCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);
        testContextImplementation.Write("2 Testing write \n\r");
        testContextImplementation.Write("3 Testing write\n\r");
        StringAssert.Equals(stringWriter.ToString(), "2 Testing write 3 Testing write");
    }

    [TestMethod]
    public void WriteLineShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        testContextImplementation.WriteLine("{0} Testing write", 1);

        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteLineShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteLineShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        stringWriter.Dispose();

        testContextImplementation.WriteLine("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        testContextImplementation.WriteLine("{0} Testing write", 1);
    }

    [TestMethod]
    public void WriteLineWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        testContextImplementation.WriteLine("1 Testing write");

        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        testContextImplementation.WriteLine("1 Testing \0 write \0");

        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteLineWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        stringWriter.Dispose();

        testContextImplementation.WriteLine("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        testContextImplementation.WriteLine("1 Testing write");
    }

    [TestMethod]
    public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
    {
        testContextImplementation = new TestContextImplementation(testMethod.Object, new ThreadSafeStringWriter(null, "test"), properties);

        testContextImplementation.WriteLine("1 Testing write");
        testContextImplementation.WriteLine("2 Its a happy day");

        StringAssert.Contains(testContextImplementation.GetDiagnosticMessages(), "1 Testing write");
        StringAssert.Contains(testContextImplementation.GetDiagnosticMessages(), "2 Its a happy day");
    }

    [TestMethod]
    public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        testContextImplementation = new TestContextImplementation(testMethod.Object, stringWriter, properties);

        testContextImplementation.WriteLine("1 Testing write");
        testContextImplementation.WriteLine("2 Its a happy day");

        testContextImplementation.ClearDiagnosticMessages();

        Assert.AreEqual(string.Empty, stringWriter.ToString());
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

