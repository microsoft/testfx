// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;
using TestFramework.ForTestingMSTest;

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

        Verify(_testContextImplementation.TryGetPropertyValue("TestName", out var propValue));
        Verify("M".Equals(propValue));
    }

    public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        Verify(!_testContextImplementation.TryGetPropertyValue("Random", out var propValue));
        Verify(propValue is null);
    }

    public void AddPropertyShouldAddPropertiesToThePropertyBag()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);
        var property = new KeyValuePair<string, string>("SomeNewProperty", "SomeValue");
        _testContextImplementation.AddProperty(property.Key, property.Value);

        Verify(_testContextImplementation.Properties[property.Key].Equals(property.Value));
    }

#if !WIN_UI && !WINDOWS_UWP
    public void AddResultFileShouldThrowIfFileNameIsNull()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        var exception =
            VerifyThrows(() => _testContextImplementation.AddResultFile(null));

        Verify(typeof(ArgumentException) == exception.GetType());
        Verify(exception.Message.Contains(Resource.Common_CannotBeNullOrEmpty));
    }

    public void AddResultFileShouldThrowIfFileNameIsEmpty()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        var exception =
            VerifyThrows(() => _testContextImplementation.AddResultFile(string.Empty));

        Verify(typeof(ArgumentException) == exception.GetType());
        Verify(exception.Message.Contains(Resource.Common_CannotBeNullOrEmpty));
    }

    public void AddResultFileShouldAddFileToResultsFiles()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddResultFile("C:\\temp.txt");

        var resultsFiles = _testContextImplementation.GetResultFiles();

        Verify(resultsFiles.Contains("C:\\temp.txt"));
    }

    public void AddResultFileShouldAddMultipleFilesToResultsFiles()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddResultFile("C:\\files\\file1.txt");
        _testContextImplementation.AddResultFile("C:\\files\\files2.html");

        var resultsFiles = _testContextImplementation.GetResultFiles();

        Verify(resultsFiles.Contains("C:\\files\\file1.txt"));
        Verify(resultsFiles.Contains("C:\\files\\files2.html"));
    }
#endif

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

#if NET462
    public void SetDataRowShouldSetDataRowObjectForCurrentRun()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        DataTable dataTable = new();

        // create the table with the appropriate column names
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        
        dataTable.LoadDataRow(new object[] { 2, "Hello" }, true);

        _testContextImplementation.SetDataRow(dataTable.Select()[0]);

        Verify(2.Equals(_testContextImplementation.DataRow.ItemArray[0]));
        Verify("Hello".Equals(_testContextImplementation.DataRow.ItemArray[1]));
    }

    public void SetDataConnectionShouldSetDbConnectionForFetchingData()
    {
        var stringWriter = new ThreadSafeStringWriter(null, "test");
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, stringWriter, _properties);

        DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.Odbc");
        DbConnection connection = factory.CreateConnection();
        connection.ConnectionString = @"Dsn=Excel Files;dbq=.\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5";

        _testContextImplementation.SetDataConnection(connection);

        Verify("Dsn=Excel Files;dbq=.\\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5"
            == _testContextImplementation.DataConnection.ConnectionString);
    }
#endif

#if NETCOREAPP
    public void GetResultFilesShouldReturnNullIfNoAddedResultFiles()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        var resultFiles = _testContextImplementation.GetResultFiles();

        Verify(resultFiles is null);
    }

#if WIN_UI
    public void GetResultFilesShouldAlwaysReturnNull()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddResultFile("C:\\files\\myfile.txt");
        _testContextImplementation.AddResultFile("C:\\files\\myfile2.txt");

        var resultFiles = _testContextImplementation.GetResultFiles();

        Verify(resultFiles is null);
    }
#else
    public void GetResultFilesShouldReturnListOfAddedResultFiles()
    {
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, new ThreadSafeStringWriter(null, "test"), _properties);

        _testContextImplementation.AddResultFile("C:\\files\\myfile.txt");
        _testContextImplementation.AddResultFile("C:\\files\\myfile2.txt");

        var resultFiles = _testContextImplementation.GetResultFiles();

        Verify(resultFiles.Count > 0, "GetResultFiles returned added elements");
        Verify(resultFiles.Contains("C:\\files\\myfile.txt"));
        Verify(resultFiles.Contains("C:\\files\\myfile2.txt"));
    }
#endif
#endif
}
