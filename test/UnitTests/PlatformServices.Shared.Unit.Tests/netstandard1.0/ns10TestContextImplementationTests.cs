// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

#if NETCOREAPP3_1
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
    private Mock<ITestMethod> _testMethod;

    private IDictionary<string, object> _properties;

    private TestContextImplementation _testContextImplementation;

    [TestInitialize]
    public void TestInit()
    {
        _testMethod = new Mock<ITestMethod>();
        _properties = new Dictionary<string, object>();
    }

    [TestMethod]
    public void TestContextConstructorShouldInitializeProperties()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.IsNotNull(_testContextImplementation.Properties);
    }

    [TestMethod]
    public void TestContextConstructorShouldInitializeDefaultProperties()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.IsNotNull(_testContextImplementation.Properties);

        CollectionAssert.Contains(
            _testContextImplementation.Properties,
            new KeyValuePair<string, object>("FullyQualifiedTestClassName", "A.C.M"));
        CollectionAssert.Contains(
            _testContextImplementation.Properties,
            new KeyValuePair<string, object>("TestName", "M"));
    }

    [TestMethod]
    public void CurrentTestOutcomeShouldReturnDefaultOutcome()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.AreEqual(UnitTestOutcome.Failed, _testContextImplementation.CurrentTestOutcome);
    }

    [TestMethod]
    public void CurrentTestOutcomeShouldReturnOutcomeSet()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

        Assert.AreEqual(UnitTestOutcome.InProgress, _testContextImplementation.CurrentTestOutcome);
    }

    [TestMethod]
    public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.AreEqual("A.C.M", _testContextImplementation.FullyQualifiedTestClassName);
    }

    [TestMethod]
    public void TestNameShouldReturnTestMethodsName()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.AreEqual("M", _testContextImplementation.TestName);
    }

    [TestMethod]
    public void PropertiesShouldReturnPropertiesPassedToTestContext()
    {
        var property1 = new KeyValuePair<string, object>("IntProperty", 1);
        var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

        _properties.Add(property1);
        _properties.Add(property2);

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        CollectionAssert.Contains(_testContextImplementation.Properties, property1);
        CollectionAssert.Contains(_testContextImplementation.Properties, property2);
    }

    [TestMethod]
    public void ContextShouldReturnTestContextObject()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Assert.IsNotNull(_testContextImplementation.Context);
        Assert.AreEqual("M", _testContextImplementation.Context.TestName);
    }

    [TestMethod]
    public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);
        Assert.IsTrue(_testContextImplementation.TryGetPropertyValue("TestName", out object propValue));
        Assert.AreEqual("M", propValue);
    }

    [TestMethod]
    public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);
        Assert.IsFalse(_testContextImplementation.TryGetPropertyValue("Random", out object propValue));
        Assert.IsNull(propValue);
    }

    [TestMethod]
    public void AddPropertyShouldAddPropertiesToThePropertyBag()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

        CollectionAssert.Contains(
            _testContextImplementation.Properties,
            new KeyValuePair<string, object>("SomeNewProperty", "SomeValue"));
    }

    [TestMethod]
    public void WriteShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("{0} Testing write", 1);
        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("{0} Testing \0 write \0", 1);
        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        stringWriter.Dispose();
        _testContextImplementation.Write("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.Write("{0} Testing write", 1);
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("1 Testing write");
        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("1 Testing \0 write \0");
        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        stringWriter.Dispose();
        _testContextImplementation.Write("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.Write("1 Testing write");
    }

    [TestMethod]
    public void WriteWithMessageShouldWriteToStringWriterForReturnCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("2 Testing write \n\r");
        _testContextImplementation.Write("3 Testing write\n\r");
        Equals(stringWriter.ToString(), "2 Testing write 3 Testing write");
    }

    [TestMethod]
    public void WriteLineShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("{0} Testing write", 1);

        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteLineShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteLineShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        stringWriter.Dispose();

        _testContextImplementation.WriteLine("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.WriteLine("{0} Testing write", 1);
    }

    [TestMethod]
    public void WriteLineWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing write");

        StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
    }

    [TestMethod]
    public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing \0 write \0");

        StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
    }

    [TestMethod]
    public void WriteLineWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        stringWriter.Dispose();

        _testContextImplementation.WriteLine("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.WriteLine("1 Testing write");
    }

    [TestMethod]
    public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        StringAssert.Contains(_testContextImplementation.GetDiagnosticMessages(), "1 Testing write");
        StringAssert.Contains(_testContextImplementation.GetDiagnosticMessages(), "2 Its a happy day");
    }

    [TestMethod]
    public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        _testContextImplementation.ClearDiagnosticMessages();

        Assert.AreEqual(string.Empty, stringWriter.ToString());
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

