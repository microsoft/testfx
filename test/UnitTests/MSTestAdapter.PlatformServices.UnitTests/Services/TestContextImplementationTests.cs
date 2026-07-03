// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class TestContextImplementationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod = new();

    private readonly IDictionary<string, object?> _properties = new Dictionary<string, object?>();

    private TestContextImplementation _testContextImplementation = null!;

    private TestContextImplementation CreateTestContextImplementation(IAdapterMessageLogger? messageLogger = null)
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

        _testContextImplementation.Properties["FullyQualifiedTestClassName"].Should().Be("A.C.M");
        _testContextImplementation.Properties["TestName"].Should().Be("M");
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

        _testContextImplementation.Properties[property.Key].Should().Be(property.Value);
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

        resultsFiles.Should().Contain("C:\\temp.txt");
    }

    public void AddResultFileShouldAddMultipleFilesToResultsFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.AddResultFile("C:\\files\\file1.txt");
        _testContextImplementation.AddResultFile("C:\\files\\files2.html");

        IList<string>? resultsFiles = _testContextImplementation.GetResultFiles();

        resultsFiles.Should().Contain("C:\\files\\file1.txt");
        resultsFiles.Should().Contain("C:\\files\\files2.html");
    }

    public void WriteShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("{0} Testing write", 1);
        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing write");
    }

    public void WriteShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("{0} Testing \0 write \0", 1);
        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing \\0 write \\0");
    }

    public void WriteWithMessageShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("1 Testing write");
        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing write");
    }

    public void WriteWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Write("1 Testing \0 write \0");
        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing \\0 write \\0");
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

        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing write");
    }

    public void WriteLineShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing \\0 write \\0");
    }

    public void WriteLineWithMessageShouldWriteToStringWriter()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing write");

        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing write");
    }

    public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
    {
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.WriteLine("1 Testing \0 write \0");

        _testContextImplementation.GetDiagnosticMessages().Should().Contain("1 Testing \\0 write \\0");
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

    public void CloneForDataDrivenIterationShouldPreserveDataConnection()
    {
        _testContextImplementation = CreateTestContextImplementation();

        DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.Odbc");
        DbConnection connection = factory.CreateConnection();
        connection.ConnectionString = @"Dsn=Excel Files;dbq=.\data.xls;defaultdir=.; driverid=790;maxbuffersize=2048;pagetimeout=5";
        _testContextImplementation.SetDataConnection(connection);

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        clone.DataConnection.Should().BeSameAs(connection);
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
        var messageLoggerMock = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);

        messageLoggerMock
            .Setup(l => l.SendMessage(It.IsAny<MessageLevel>(), It.IsAny<string>()));

        _testContextImplementation = CreateTestContextImplementation(messageLoggerMock.Object);
        _testContextImplementation.DisplayMessage(MessageLevel.Informational, "InfoMessage");
        _testContextImplementation.DisplayMessage(MessageLevel.Warning, "WarningMessage");
        _testContextImplementation.DisplayMessage(MessageLevel.Error, "ErrorMessage");

        messageLoggerMock.Verify(x => x.SendMessage(MessageLevel.Informational, "InfoMessage"), Times.Once);
        messageLoggerMock.Verify(x => x.SendMessage(MessageLevel.Warning, "WarningMessage"), Times.Once);
        messageLoggerMock.Verify(x => x.SendMessage(MessageLevel.Error, "ErrorMessage"), Times.Once);
    }

    public void GetAndClearOutput_ShouldReturnContentThenClearBuffer()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.WriteConsoleOut("hello");

        string? first = _testContextImplementation.GetAndClearOutput();
        string? second = _testContextImplementation.GetAndClearOutput();

        first.Should().Be("hello");
        second.Should().BeEmpty();
    }

    public void GetAndClearError_ShouldReturnContentThenClearBuffer()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.WriteConsoleErr("hello");

        string? first = _testContextImplementation.GetAndClearError();
        string? second = _testContextImplementation.GetAndClearError();

        first.Should().Be("hello");
        second.Should().BeEmpty();
    }

    public void GetAndClearTrace_ShouldReturnContentThenClearBuffer()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.WriteTrace("hello");

        string? first = _testContextImplementation.GetAndClearTrace();
        string? second = _testContextImplementation.GetAndClearTrace();

        first.Should().Be("hello");
        second.Should().BeEmpty();
    }

    public void WritesFromBackgroundThreadShouldNotThrow()
    {
        TestContextImplementation testContextImplementation = CreateTestContextImplementation(new Mock<IAdapterMessageLogger>().Object);
        var t = new Thread(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                testContextImplementation.WriteConsoleOut(new string('a', 1000000));
                testContextImplementation.WriteConsoleErr(new string('b', 1000000));
            }
        });

        t.Start();
        _ = testContextImplementation.GetAndClearOutput();
        _ = testContextImplementation.GetAndClearError();
        _ = testContextImplementation.GetAndClearTrace();
        t.Join();
    }

    public void MergePropertiesShouldAddNewKeysIntoThePropertyBag()
    {
        _testContextImplementation = CreateTestContextImplementation();
        IReadOnlyDictionary<string, object?> snapshot = new Dictionary<string, object?>
        {
            ["NewKey"] = "NewValue",
            ["AnotherKey"] = 42,
        };

        _testContextImplementation.MergeProperties(snapshot);

        _testContextImplementation.Properties["NewKey"].Should().Be("NewValue");
        _testContextImplementation.Properties["AnotherKey"].Should().Be(42);
    }

    public void MergePropertiesShouldOverwriteExistingKeys()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["Key"] = "Original";

        _testContextImplementation.MergeProperties(new Dictionary<string, object?> { ["Key"] = "Overwritten" });

        _testContextImplementation.Properties["Key"].Should().Be("Overwritten");
    }

    public void MergePropertiesShouldOverrideSeededSourceLevelParameters()
    {
        // Seeded source-level parameters (the bag the runner forwards from runsettings
        // TestRunParameters) sit in _properties at construction time; lifecycle snapshots
        // from AssemblyInitialize / ClassInitialize MUST override them on key collision so
        // a user's explicit assignment wins for the rest of the lifecycle.
        var seeded = new Dictionary<string, object?>
        {
            ["RunSettingsKey"] = "FromRunSettings",
        };
        _testContextImplementation = new TestContextImplementation(_testMethod.Object, null, seeded, null, null);

        _testContextImplementation.MergeProperties(new Dictionary<string, object?>
        {
            ["RunSettingsKey"] = "FromAssemblyInit",
        });

        _testContextImplementation.Properties["RunSettingsKey"].Should().Be("FromAssemblyInit");
    }

    public void MergePropertiesShouldIgnoreNull()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["Key"] = "Original";

        _testContextImplementation.MergeProperties(null);

        _testContextImplementation.Properties["Key"].Should().Be("Original");
    }

    public void MergePropertiesShouldIgnoreEmptyDictionary()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["Key"] = "Original";

        _testContextImplementation.MergeProperties(new Dictionary<string, object?>());

        _testContextImplementation.Properties["Key"].Should().Be("Original");
    }

    public void MergePropertiesShouldNotOverwritePerContextLabels()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.MergeProperties(new Dictionary<string, object?>
        {
            ["FullyQualifiedTestClassName"] = "Hacked.Class",
            ["TestName"] = "HackedTestName",
            ["LegitKey"] = "LegitValue",
        });

        _testContextImplementation.Properties["FullyQualifiedTestClassName"].Should().Be("A.C.M");
        _testContextImplementation.Properties["TestName"].Should().Be("M");
        _testContextImplementation.Properties["LegitKey"].Should().Be("LegitValue");
    }

    public void CaptureLifecyclePropertiesShouldReturnAllPropertiesExceptPerContextLabels()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["UserKey"] = "UserValue";
        _testContextImplementation.Properties["AnotherKey"] = 7;

        IReadOnlyDictionary<string, object?>? snapshot = _testContextImplementation.CaptureLifecycleProperties();

        snapshot.Should().NotBeNull();
        snapshot.Should().ContainKey("UserKey");
        snapshot!["UserKey"].Should().Be("UserValue");
        snapshot.Should().ContainKey("AnotherKey");
        snapshot!["AnotherKey"].Should().Be(7);
        snapshot.Should().NotContainKey("FullyQualifiedTestClassName");
        snapshot.Should().NotContainKey("TestName");
    }

    public void CaptureLifecyclePropertiesShouldReturnNullWhenNoNonLabelPropertiesExist()
    {
        _testContextImplementation = CreateTestContextImplementation();

        // Context has no properties at all; no labels were seeded because the ITestMethod mock is
        // unconfigured (FullClassName/Name return null) and testClassFullName is null.
        IReadOnlyDictionary<string, object?>? snapshot = _testContextImplementation.CaptureLifecycleProperties();

        snapshot.Should().BeNull();
    }

    public void CaptureLifecyclePropertiesShouldReturnSnapshotIndependentOfTheLiveBag()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["Key"] = "OriginalValue";

        IReadOnlyDictionary<string, object?>? snapshot = _testContextImplementation.CaptureLifecycleProperties();

        // Mutating the live bag must not affect the snapshot.
        _testContextImplementation.Properties["Key"] = "ChangedValue";
        _testContextImplementation.Properties["NewKey"] = "NewValue";

        snapshot.Should().NotBeNull();
        snapshot!["Key"].Should().Be("OriginalValue");
        snapshot.Should().NotContainKey("NewKey");
    }

    public void CaptureLifecyclePropertiesShouldAliasReferenceTypeValues()
    {
        _testContextImplementation = CreateTestContextImplementation();
        var bag = new List<int> { 1 };
        _testContextImplementation.Properties["RefKey"] = bag;

        IReadOnlyDictionary<string, object?>? snapshot = _testContextImplementation.CaptureLifecycleProperties();

        // The snapshot is shallow: the snapshot's value and the live bag share the same instance.
        // Mutating the instance must therefore be visible through both. This guards the documented
        // contract on CaptureLifecycleProperties from accidentally regressing to a deep copy.
        snapshot.Should().NotBeNull();
        bag.Add(2);
        ((List<int>)snapshot!["RefKey"]!).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    public void CaptureLifecyclePropertiesAndMergePropertiesShouldNotLockOnExposedPropertyBag()
    {
        _testContextImplementation = CreateTestContextImplementation();

        lock (_testContextImplementation.Properties)
        {
            Task.WhenAll(
                    Task.Run(() => _ = _testContextImplementation.CaptureLifecycleProperties()),
                    Task.Run(() => _testContextImplementation.MergeProperties(new Dictionary<string, object?>
                    {
                        ["Key"] = "Value",
                    })))
                .Wait(TimeSpan.FromSeconds(10))
                .Should().BeTrue();
        }

        _testContextImplementation.Properties["Key"].Should().Be("Value");
    }

    public void ConstructorShouldNotThrowWhenSeededPropertiesAlreadyContainFullyQualifiedTestClassName()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        var seeded = new Dictionary<string, object?>
        {
            ["FullyQualifiedTestClassName"] = "Old.Class.Name",
        };

        // Should not throw — the ctor now uses indexer assignment for labels.
        var ctx = new TestContextImplementation(_testMethod.Object, null, seeded, null, null);

        // The per-context value wins.
        ctx.Properties["FullyQualifiedTestClassName"].Should().Be("A.C.M");
    }

    public void CloneForDataDrivenIterationShouldCopyPropertyBagShallowly()
    {
        _testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        _testMethod.Setup(tm => tm.Name).Returns("M");
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["UserKey"] = "UserValue";

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        clone.Properties["FullyQualifiedTestClassName"].Should().Be("A.C.M");
        clone.Properties["TestName"].Should().Be("M");
        clone.Properties["UserKey"].Should().Be("UserValue");
    }

    public void CloneForDataDrivenIterationShouldIsolatePropertyBagFromOriginal()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Properties["Key"] = "Original";

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        // Mutations on the clone must not leak back to the original.
        clone.Properties["Key"] = "MutatedOnClone";
        clone.Properties["NewKey"] = "AddedOnClone";

        _testContextImplementation.Properties["Key"].Should().Be("Original");
        _testContextImplementation.Properties.Should().NotContainKey("NewKey");

        // And mutations on the original after the clone is created must not leak to the clone.
        _testContextImplementation.Properties["Key"] = "MutatedOnOriginal";
        clone.Properties["Key"].Should().Be("MutatedOnClone");
    }

    public void CloneForDataDrivenIterationShouldStartWithNoAccumulatedOutput()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.WriteConsoleOut("orig-out");
        _testContextImplementation.WriteConsoleErr("orig-err");
        _testContextImplementation.WriteTrace("orig-trace");
        _testContextImplementation.WriteLine("orig-diag");

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        // The clone has no captured output of its own yet.
        clone.GetAndClearOutput().Should().BeNullOrEmpty();
        clone.GetAndClearError().Should().BeNullOrEmpty();
        clone.GetAndClearTrace().Should().BeNullOrEmpty();
        clone.GetDiagnosticMessages().Should().BeNullOrEmpty();

        // The clone's output buffers are independent: writing to the clone does not flow back
        // to the original.
        clone.WriteConsoleOut("clone-only");
        _testContextImplementation.GetAndClearOutput().Should().Be("orig-out");
        clone.GetAndClearOutput().Should().Be("clone-only");
    }

    public void CloneForDataDrivenIterationShouldStartWithFreshOutcomeAndException()
    {
        _testContextImplementation = CreateTestContextImplementation();

        // Set outcome to a non-default value (Passed) on the original so the assertion below
        // actually verifies that the clone is reset to the default rather than inheriting
        // from the original. If we left the original at the default UnitTestOutcome.Failed,
        // a buggy clone that copied the outcome would still appear correct.
        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.SetException(new InvalidOperationException("boom"));

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        clone.CurrentTestOutcome.Should().Be(UnitTestOutcome.Failed); // default value of UnitTestOutcome
        clone.TestException.Should().BeNull();

        // Setting outcome on the clone does not leak back to the original.
        clone.SetOutcome(UnitTestOutcome.Inconclusive);
        _testContextImplementation.CurrentTestOutcome.Should().Be(UnitTestOutcome.Passed);
    }

    public void CloneForDataDrivenIterationShouldStartWithFreshResultFiles()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.AddResultFile("C:\\original.txt");

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        clone.GetResultFiles().Should().BeNull();

        clone.AddResultFile("C:\\clone.txt");
        IList<string>? originalResults = _testContextImplementation.GetResultFiles();
        originalResults.Should().NotBeNull();
        originalResults!.Should().Contain(s => s.EndsWith("original.txt", StringComparison.OrdinalIgnoreCase));
        originalResults.Should().NotContain(s => s.EndsWith("clone.txt", StringComparison.OrdinalIgnoreCase));
    }

    public void CloneForDataDrivenIterationShouldCopyTestRunCount()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.Context.TestRunCount = 7;

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        clone.Context.TestRunCount.Should().Be(7);

        // After clone creation, TestRunCount on the original and clone are independent.
        _testContextImplementation.Context.TestRunCount = 8;
        clone.Context.TestRunCount.Should().Be(7);
    }

    public void CloneForDataDrivenIterationShouldShareMessageLogger()
    {
        var messageLoggerMock = new Mock<IAdapterMessageLogger>();
        _testContextImplementation = CreateTestContextImplementation(messageLoggerMock.Object);

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();
        clone.DisplayMessage(MessageLevel.Informational, "from-clone");

        messageLoggerMock.Verify(x => x.SendMessage(MessageLevel.Informational, "from-clone"), Times.Once);
    }
}
