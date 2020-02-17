// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
    using Moq;
    using MSTestAdapter.TestUtilities;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UnitTestOutcome = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

    [TestClass]
    public class DesktopTestContextImplTests
    {
        private Mock<ITestMethod> testMethod;

        private IDictionary<string, object> properties;

        private TestContextImplementation testContextImplementation;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<ITestMethod>();
            this.properties = new Dictionary<string, object>();
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeProperties()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeDefaultProperties()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);

            CollectionAssert.Contains(
                this.testContextImplementation.Properties,
                new KeyValuePair<string, object>("FullyQualifiedTestClassName", "A.C.M"));
            CollectionAssert.Contains(
                this.testContextImplementation.Properties,
                new KeyValuePair<string, object>("TestName", "M"));
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnDefaultOutcome()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual(UnitTestOutcome.Failed, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnOutcomeSet()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

            Assert.AreEqual(UnitTestOutcome.InProgress, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("A.C.M", this.testContextImplementation.FullyQualifiedTestClassName);
        }

        [TestMethod]
        public void TestNameShouldReturnTestMethodsName()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("M", this.testContextImplementation.TestName);
        }

        [TestMethod]
        public void PropertiesShouldReturnPropertiesPassedToTestContext()
        {
            var property1 = new KeyValuePair<string, object>("IntProperty", 1);
            var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

            this.properties.Add(property1);
            this.properties.Add(property2);

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            CollectionAssert.Contains(this.testContextImplementation.Properties, property1);
            CollectionAssert.Contains(this.testContextImplementation.Properties, property2);
        }

        [TestMethod]
        public void ContextShouldReturnTestContextObject()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Context);
            Assert.AreEqual("M", this.testContextImplementation.Context.TestName);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;

            Assert.IsTrue(this.testContextImplementation.TryGetPropertyValue("TestName", out propValue));
            Assert.AreEqual("M", propValue);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;

            Assert.IsFalse(this.testContextImplementation.TryGetPropertyValue("Random", out propValue));
            Assert.IsNull(propValue);
        }

        [TestMethod]
        public void AddPropertyShouldAddPropertiesToThePropertyBag()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);
            this.testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

            CollectionAssert.Contains(
                this.testContextImplementation.Properties,
                new KeyValuePair<string, object>("SomeNewProperty", "SomeValue"));
        }

        [TestMethod]
        public void AddResultFileShouldThrowIfFileNameIsNull()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            var exception =
                ActionUtility.PerformActionAndReturnException(() => this.testContextImplementation.AddResultFile(null));

            Assert.AreEqual(typeof(ArgumentException), exception.GetType());
            StringAssert.Contains(exception.Message, Resource.Common_CannotBeNullOrEmpty);
        }

        [TestMethod]
        public void AddResultFileShouldThrowIfFileNameIsEmpty()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            var exception =
                ActionUtility.PerformActionAndReturnException(() => this.testContextImplementation.AddResultFile(string.Empty));

            Assert.AreEqual(typeof(ArgumentException), exception.GetType());
            StringAssert.Contains(exception.Message, Resource.Common_CannotBeNullOrEmpty);
        }

        [TestMethod]
        public void AddResultFileShouldAddFiletoResultsFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.AddResultFile("C:\\temp.txt");

            var resultsFiles = this.testContextImplementation.GetResultFiles();

            CollectionAssert.Contains(resultsFiles.ToList(), "C:\\temp.txt");
        }

        [TestMethod]
        public void AddResultFileShouldAddMultipleFilestoResultsFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.AddResultFile("C:\\temp.txt");
            this.testContextImplementation.AddResultFile("C:\\temp2.txt");

            var resultsFiles = this.testContextImplementation.GetResultFiles();

            CollectionAssert.Contains(resultsFiles.ToList(), "C:\\temp.txt");
            CollectionAssert.Contains(resultsFiles.ToList(), "C:\\temp2.txt");
        }

        [TestMethod]
        public void WriteShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            this.testContextImplementation.Write("{0} Testing write", 1);
            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            this.testContextImplementation.Write("{0} Testing \0 write \0", 1);
            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            stringWriter.Dispose();
            this.testContextImplementation.Write("{0} Testing write", 1);

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.Write("{0} Testing write", 1);
        }

        [TestMethod]
        public void WriteWithMessageShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            this.testContextImplementation.Write("1 Testing write");
            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            this.testContextImplementation.Write("1 Testing \0 write \0");
            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteWithMessageShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            stringWriter.Dispose();
            this.testContextImplementation.Write("1 Testing write");

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.Write("1 Testing write");
        }

        [TestMethod]
        public void WriteWithMessageShouldWriteToStringWriterForReturnCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);
            this.testContextImplementation.Write("2 Testing write \n\r");
            this.testContextImplementation.Write("3 Testing write\n\r");
            StringAssert.Equals(stringWriter.ToString(), "2 Testing write 3 Testing write");
        }

        [TestMethod]
        public void WriteLineShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("{0} Testing write", 1);

            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteLineShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteLineShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            stringWriter.Dispose();

            this.testContextImplementation.WriteLine("{0} Testing write", 1);

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.WriteLine("{0} Testing write", 1);
        }

        [TestMethod]
        public void WriteLineWithMessageShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");

            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing \0 write \0");

            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteLineWithMessageShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            stringWriter.Dispose();

            this.testContextImplementation.WriteLine("1 Testing write");

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.WriteLine("1 Testing write");
        }

        [TestMethod]
        public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");
            this.testContextImplementation.WriteLine("2 Its a happy day");

            StringAssert.Contains(this.testContextImplementation.GetDiagnosticMessages(), "1 Testing write");
            StringAssert.Contains(this.testContextImplementation.GetDiagnosticMessages(), "2 Its a happy day");
        }

        [TestMethod]
        public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");
            this.testContextImplementation.WriteLine("2 Its a happy day");

            this.testContextImplementation.ClearDiagnosticMessages();

            Assert.AreEqual(string.Empty, stringWriter.ToString());
        }

        [TestMethod]
        public void SetDataRowShouldSetDataRowObjectForCurrentRun()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            DataTable dataTable = new DataTable();

            // create the table with the appropriate column names
            dataTable.Columns.Add("Id", typeof(int));
            dataTable.Columns.Add("Name", typeof(string));

            dataTable.LoadDataRow(new object[] { 2, "Hello" }, true);

            this.testContextImplementation.SetDataRow(dataTable.Select()[0]);

            Assert.AreEqual(2, this.testContextImplementation.DataRow.ItemArray[0]);
            Assert.AreEqual("Hello", this.testContextImplementation.DataRow.ItemArray[1]);
        }

        [TestMethod]
        public void SetDataConnectionShouldSetDbConnectionForFetchingData()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.Odbc");
            DbConnection connection = factory.CreateConnection();
            connection.ConnectionString = @"Dsn=Excel Files;dbq=.\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5";

            this.testContextImplementation.SetDataConnection(connection);

            Assert.AreEqual("Dsn=Excel Files;dbq=.\\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5", this.testContextImplementation.DataConnection.ConnectionString);
        }
    }
}
