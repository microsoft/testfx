// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

public class TestContextImplementationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod;

    private readonly IDictionary<string, object> _properties;

    private TestContextImplementation _testContextImplementation;

    public TestContextImplementationTests()
    {
        _testMethod = new Mock<ITestMethod>();
        _properties = new Dictionary<string, object>();
    }

    public void TestContextConstructorShouldInitializeProperties()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(_testContextImplementation.Properties is not null);
    }

    public void TestContextConstructorShouldInitializeDefaultProperties()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(_testContextImplementation.Properties is not null);

        Verify(_testContextImplementation.Properties["FullyQualifiedTestClassName"].Equals("A.C.M"));
        Verify(_testContextImplementation.Properties["TestName"].Equals("M"));
    }

    public void CurrentTestOutcomeShouldReturnDefaultOutcome()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(UnitTestOutcome.Failed == _testContextImplementation.CurrentTestOutcome);
    }

    public void CurrentTestOutcomeShouldReturnOutcomeSet()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

        Verify(UnitTestOutcome.InProgress == _testContextImplementation.CurrentTestOutcome);
    }

    public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify("A.C.M" == _testContextImplementation.FullyQualifiedTestClassName);
    }

    public void TestNameShouldReturnTestMethodsName()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify("M" == _testContextImplementation.TestName);
    }

    public void PropertiesShouldReturnPropertiesPassedToTestContext()
    {
        var property1 = new KeyValuePair<string, object>("IntProperty", 1);
        var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

        _properties.Add(property1);
        _properties.Add(property2);

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(_testContextImplementation.Properties[property1.Key] == property1.Value);
        Verify(_testContextImplementation.Properties[property2.Key] == property2.Value);
    }

    public void ContextShouldReturnTestContextObject()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(_testContextImplementation.Context is not null);
        Verify("M" == _testContextImplementation.Context.TestName);
    }

    public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);
        Verify(_testContextImplementation.TryGetPropertyValue("TestName", out object propValue));
        Verify("M".Equals(propValue));
    }

    public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);
        Verify(!_testContextImplementation.TryGetPropertyValue("Random", out object propValue));
        Verify(propValue is null);
    }

    public void AddPropertyShouldAddPropertiesToThePropertyBag()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

        Verify(_testContextImplementation.Properties["SomeNewProperty"].Equals("SomeValue"));
    }

    public void WriteShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("{0} Testing write", 1);
        Verify(stringWriter.ToString().Contains("1 Testing write"));
    }

    public void WriteShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("{0} Testing \0 write \0", 1);
        Verify(stringWriter.ToString().Contains("1 Testing \\0 write \\0"));
    }

    public void WriteShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        stringWriter.Dispose();
        _testContextImplementation.Write("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.Write("{0} Testing write", 1);
    }

    public void WriteWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("1 Testing write");
        Verify(stringWriter.ToString().Contains("1 Testing write"));
    }

    public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("1 Testing \0 write \0");
        Verify(stringWriter.ToString().Contains("1 Testing \\0 write \\0"));
    }

    public void WriteWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        stringWriter.Dispose();
        _testContextImplementation.Write("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.Write("1 Testing write");
    }

    public void WriteWithMessageShouldWriteToStringWriterForReturnCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);
        _testContextImplementation.Write("2 Testing write \n\r");
        _testContextImplementation.Write("3 Testing write\n\r");
        Equals(stringWriter.ToString(), "2 Testing write 3 Testing write");
    }

    public void WriteLineShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("{0} Testing write", 1);

        Verify(stringWriter.ToString().Contains("1 Testing write"));
    }

    public void WriteLineShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

        Verify(stringWriter.ToString().Contains("1 Testing \\0 write \\0"));
    }

    public void WriteLineShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        stringWriter.Dispose();

        _testContextImplementation.WriteLine("{0} Testing write", 1);

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.WriteLine("{0} Testing write", 1);
    }

    public void WriteLineWithMessageShouldWriteToStringWriter()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing write");

        Verify(stringWriter.ToString().Contains("1 Testing write"));
    }

    public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing \0 write \0");

        Verify(stringWriter.ToString().Contains("1 Testing \\0 write \\0"));
    }

    public void WriteLineWithMessageShouldNotThrowIfStringWriterIsDisposed()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        stringWriter.Dispose();

        _testContextImplementation.WriteLine("1 Testing write");

        // Calling it twice to cover the direct return when we know the object has been disposed.
        _testContextImplementation.WriteLine("1 Testing write");
    }

    public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        Verify(_testContextImplementation.GetDiagnosticMessages().Contains("1 Testing write"));
        Verify(_testContextImplementation.GetDiagnosticMessages().Contains("2 Its a happy day"));
    }

    public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        _testContextImplementation.ClearDiagnosticMessages();

        Verify(string.Empty == stringWriter.ToString());
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

