// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;
using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class TestContextImplementationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod;

    private readonly IDictionary<string, object?> _properties;

    private TestContextImplementation _testContextImplementation = null!;

    public TestContextImplementationTests()
    {
        _testMethod = new Mock<ITestMethod>();
        _properties = new Dictionary<string, object?>();
    }

    private TestContextImplementation CreateTestContextImplementation(IMessageLogger? messageLogger = null)
        => new(_testMethod.Object, null, _properties, messageLogger, null);

    public void TestContextConstructorShouldInitializeProperties()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.Properties.Should().NotBeNull();
    }

    public void TestContextConstructorShouldInitializeDefaultProperties()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.Properties.Should().NotBeNull();

        _testContextImplementation.Properties["FullyQualifiedTestClassName"]!.Should().Be("A.C.M");
        _testContextImplementation.Properties["TestName"]!.Should().Be("M");
    }

    public void CurrentTestOutcomeShouldReturnDefaultOutcome()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.CurrentTestOutcome.Should().Be(UnitTestOutcome.Failed);
    }

    public void CurrentTestOutcomeShouldReturnOutcomeSet()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

        _testContextImplementation.CurrentTestOutcome.Should().Be(UnitTestOutcome.InProgress);
    }

    public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.FullyQualifiedTestClassName.Should().Be("A.C.M");
    }

    public void TestNameShouldReturnTestMethodsName()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.TestName.Should().Be("M");
    }

    public void PropertiesShouldReturnPropertiesPassedToTestContext()
    {
        var property1 = new KeyValuePair<string, object?>("IntProperty", 1);
        var property2 = new KeyValuePair<string, object?>("DoubleProperty", 2.023);

        _properties.Add(property1);
        _properties.Add(property2);

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.Properties[property1.Key].Should().Be(property1.Value);
        _testContextImplementation.Properties[property2.Key].Should().Be(property2.Value);
    }

    public void ContextShouldReturnTestContextObject()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.Context.Should().NotBeNull();
        _testContextImplementation.Context.TestName.Should().Be("M");
    }

    public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
    {
        _testMethod.Setup(tm => tm.Name).Returns("M");

        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.TryGetPropertyValue("TestName", out object? propValue).Should().BeTrue();
        propValue.Should().Be("M");
    }

    public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.TryGetPropertyValue("Random", out object? propValue).Should().BeFalse();
        propValue.Should().BeNull();
    }

    public void AddPropertyShouldAddPropertiesToThePropertyBag()
    {
        _testContextImplementation = CreateTestContextImplementation();
        var property = new KeyValuePair<string, string>("SomeNewProperty", "SomeValue");
        _testContextImplementation.AddProperty(property.Key, property.Value);

        _testContextImplementation.Properties[property.Key]!.Should().Be(property.Value);
    }

    public void AddResultFileShouldThrowIfFileNameIsNull()
    {
        _testContextImplementation = CreateTestContextImplementation();

        Action action = () => _testContextImplementation.AddResultFile(null!);
        action.Should().Throw<ArgumentException>().WithMessage("*" + Resource.Common_CannotBeNullOrEmpty + "*");
    }

    public void AddResultFileShouldThrowIfFileNameIsEmpty()
    {
        _testContextImplementation = CreateTestContextImplementation();

        Action action = () => _testContextImplementation.AddResultFile(string.Empty);
        action.Should().Throw<ArgumentException>().WithMessage("*" + Resource.Common_CannotBeNullOrEmpty + "*");
    }

    public void AddResultFileShouldAddFileToResultsFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.AddResultFile("C:\\temp.txt");

        IList<string>? resultsFiles = _testContextImplementation.GetResultFiles();

        resultsFiles!.Should().Contain("C:\\temp.txt");
    }

    public void AddResultFileShouldAddMultipleFilesToResultsFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.AddResultFile("C:\\files\\file1.txt");
        _testContextImplementation.AddResultFile("C:\\files\\files2.html");

        IList<string>? resultsFiles = _testContextImplementation.GetResultFiles();

        resultsFiles!.Should().Contain("C:\\files\\file1.txt");
        resultsFiles.Should().Contain("C:\\files\\files2.html");
    }

    public void WriteShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("{0} Testing write", 1);
        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing write");
    }

    public void WriteShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("{0} Testing \0 write \0", 1);
        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing \\0 write \\0");
    }

    public void WriteWithMessageShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("1 Testing write");
        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing write");
    }

    public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("1 Testing \0 write \0");
        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing \\0 write \\0");
    }

    public void WriteWithMessageShouldWriteToStringWriterForReturnCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("2 Testing write \n\r");
        _testContextImplementation.Write("3 Testing write\n\r");
        _testContextImplementation.GetDiagnosticMessages().Should().Be("2 Testing write \n\r3 Testing write\n\r");
    }

    public void WriteLineShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("{0} Testing write", 1);

        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing write");
    }

    public void WriteLineShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing \\0 write \\0");
    }

    public void WriteLineWithMessageShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing write");

        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing write");
    }

    public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing \0 write \0");

        _testContextImplementation.GetDiagnosticMessages()!.Should().Contain("1 Testing \\0 write \\0");
    }

    public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing write");
        _testContextImplementation.GetDiagnosticMessages().Should().Contain("2 Its a happy day");
    }

    public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing write");
        _testContextImplementation.WriteLine("2 Its a happy day");

        _testContextImplementation.ClearDiagnosticMessages();

        _testContextImplementation.GetDiagnosticMessages().Should().Be(string.Empty);
    }

#if NETFRAMEWORK
    public void SetDataRowShouldSetDataRowObjectForCurrentRun()
    {
        _testContextImplementation = CreateTestContextImplementation();

        DataTable dataTable = new();

        // create the table with the appropriate column names
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));

        dataTable.LoadDataRow([2, "Hello"], true);

        _testContextImplementation.SetDataRow(dataTable.Select()[0]);

        _testContextImplementation.DataRow!.ItemArray[0].Should().Be(2);
        _testContextImplementation.DataRow.ItemArray[1].Should().Be("Hello");
    }

    public void SetDataConnectionShouldSetDbConnectionForFetchingData()
    {
        _testContextImplementation = CreateTestContextImplementation();

        DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.Odbc");
        DbConnection connection = factory.CreateConnection();
        connection.ConnectionString = @"Dsn=Excel Files;dbq=.\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5";

        _testContextImplementation.SetDataConnection(connection);

        _testContextImplementation.DataConnection!.ConnectionString
            .Should().Be("Dsn=Excel Files;dbq=.\\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5");
    }
#endif

#if NETCOREAPP
    public void GetResultFilesShouldReturnNullIfNoAddedResultFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();

        IList<string>? resultFiles = _testContextImplementation.GetResultFiles();

        resultFiles.Should().BeNull();
    }

    public void GetResultFilesShouldReturnListOfAddedResultFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.AddResultFile("C:\\files\\myfile.txt");
        _testContextImplementation.AddResultFile("C:\\files\\myfile2.txt");

        IList<string>? resultFiles = _testContextImplementation.GetResultFiles();

        resultFiles!.Count.Should().BeGreaterThan(0, "GetResultFiles returned added elements");
        resultFiles.Should().Contain("C:\\files\\myfile.txt");
        resultFiles.Should().Contain("C:\\files\\myfile2.txt");
    }
#endif

    public void DisplayMessageShouldForwardToIMessageLogger()
    {
        var messageLoggerMock = new Mock<IMessageLogger>(MockBehavior.Strict);

        messageLoggerMock
            .Setup(l => l.SendMessage(It.IsAny<TestMessageLevel>(), It.IsAny<string>()));

        _testContextImplementation = CreateTestContextImplementation(messageLoggerMock.Object);
        _testContextImplementation.DisplayMessage(MessageLevel.Informational, "InfoMessage");
        _testContextImplementation.DisplayMessage(MessageLevel.Warning, "WarningMessage");
        _testContextImplementation.DisplayMessage(MessageLevel.Error, "ErrorMessage");

        messageLoggerMock.Verify(x => x.SendMessage(TestMessageLevel.Informational, "InfoMessage"), Times.Once);
        messageLoggerMock.Verify(x => x.SendMessage(TestMessageLevel.Warning, "WarningMessage"), Times.Once);
        messageLoggerMock.Verify(x => x.SendMessage(TestMessageLevel.Error, "ErrorMessage"), Times.Once);
    }

    public void WritesFromBackgroundThreadShouldNotThrow()
    {
        TestContextImplementation testContextImplementation = CreateTestContextImplementation(new Mock<IMessageLogger>().Object);
        var t = new Thread(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                testContextImplementation.WriteConsoleOut(new string('a', 1000000));
                testContextImplementation.WriteConsoleErr(new string('b', 1000000));
            }
        });

        t.Start();
        _ = testContextImplementation.GetOut();
        _ = testContextImplementation.GetErr();
        t.Join();
    }
}
