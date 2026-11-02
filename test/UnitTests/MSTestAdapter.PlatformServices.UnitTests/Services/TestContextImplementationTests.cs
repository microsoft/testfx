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
        _testContextImplementation.StandardOutputBuilder.Append("hello");

        string? first = _testContextImplementation.GetAndClearOutput();
        string? second = _testContextImplementation.GetAndClearOutput();

        first.Should().Be("hello");
        second.Should().BeEmpty();
    }

    public void GetAndClearError_ShouldReturnContentThenClearBuffer()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.StandardErrorBuilder.Append("hello");

        string? first = _testContextImplementation.GetAndClearError();
        string? second = _testContextImplementation.GetAndClearError();

        first.Should().Be("hello");
        second.Should().BeEmpty();
    }

    public void GetAndClearTrace_ShouldReturnContentThenClearBuffer()
    {
        _testContextImplementation = CreateTestContextImplementation();
        _testContextImplementation.TraceBuilder.Append("hello");

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
                testContextImplementation.StandardOutputBuilder.Append(new string('a', 1000000));
                testContextImplementation.StandardErrorBuilder.Append(new string('b', 1000000));
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
        _testContextImplementation.StandardOutputBuilder.Append("orig-out");
        _testContextImplementation.StandardErrorBuilder.Append("orig-err");
        _testContextImplementation.TraceBuilder.Append("orig-trace");
        _testContextImplementation.WriteLine("orig-diag");

        TestContextImplementation clone = _testContextImplementation.CloneForDataDrivenIteration();

        // The clone has no captured output of its own yet.
        clone.GetAndClearOutput().Should().BeNullOrEmpty();
        clone.GetAndClearError().Should().BeNullOrEmpty();
        clone.GetAndClearTrace().Should().BeNullOrEmpty();
        clone.GetDiagnosticMessages().Should().BeNullOrEmpty();

        // The clone's output buffers are independent: writing to the clone does not flow back
        // to the original.
        clone.StandardOutputBuilder.Append("clone-only");
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

#if !WINDOWS_UWP && !WIN_UI
    public void TestTempDirectoryShouldNotCreateDirectoryWhenNeverAccessed()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        // Never touch TestTempDirectory, then dispose.
        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        // Lazy creation: nothing should have been created under the results directory.
        Directory.GetDirectories(resultsDirectory.Path).Should().BeEmpty();
    }

    public void TestTempDirectoryShouldReturnNullForNonTestContexts()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;

        // A fixture (assembly/class initialize or cleanup) context is created with a null test
        // method. It is not per-test and may never be disposed, so it must not create a directory.
        using TestContextImplementation fixtureContext = new(null, "SomeClass", _properties, null, null);

        fixtureContext.TestTempDirectory.Should().BeNull();
        Directory.GetDirectories(resultsDirectory.Path).Should().BeEmpty();
    }

    public void TestTempDirectoryShouldNotCreateDirectoryAfterDispose()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        // A late first access (e.g. from a background thread the test spawned that outlives the test
        // body) must not create a directory after cleanup already ran, which would leak it. The
        // getter returns null and creates nothing once cleanup has started.
        string? afterDispose = _testContextImplementation.TestTempDirectory;

        afterDispose.Should().BeNull();
        Directory.GetDirectories(resultsDirectory.Path).Should().BeEmpty();
    }

    public void TestTempDirectoryShouldCreateDirectoryUnderResultsDirectoryOnFirstAccess()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testMethod.Setup(tm => tm.Name).Returns("MyTest");
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();
        Path.GetDirectoryName(tempDirectory).Should().Be(resultsDirectory.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Path.GetFileName(tempDirectory!).Should().StartWith("MyTest_");

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldSanitizeAndBoundLongNameWithInvalidCharacters()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;

        // A long, data-driven-style display name full of characters that are invalid in a path.
        string hostileName = "My/Test\\With:Illegal*Chars?\"<>|And " + new string('x', 200);
        _testMethod.Setup(tm => tm.Name).Returns(hostileName);
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();

        string fileName = Path.GetFileName(tempDirectory!);
        fileName.Should().NotContain("/").And.NotContain("\\").And.NotContain(":")
            .And.NotContain("*").And.NotContain("?").And.NotContain("\"")
            .And.NotContain("<").And.NotContain(">").And.NotContain("|");
        fileName.Should().NotContainAny(Array.ConvertAll(Path.GetInvalidFileNameChars(), c => c.ToString()));

        // 50-char sanitized cap + '_' + 32-char GUID suffix = a bounded, MAX_PATH-friendly length.
        fileName.Length.Should().BeLessThanOrEqualTo(50 + 1 + 32);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldFallBackToTempPathWhenResultsDirectoryTooDeep()
    {
        using TempDirectoryScope scope = new();

        // A results directory long enough that, on Windows, even a minimal readable name plus the
        // reserved MAX_PATH headroom cannot fit — the implementation must fall back to system temp.
        // It is nested under a TempDirectoryScope so that on non-Windows (where no fallback occurs
        // and this parent is actually created) it is reclaimed instead of leaking into the temp
        // folder. On Windows the path is only used for its length; the directory is never created.
        string deepResults = Path.Combine(scope.Path, new string('d', 200));
        _properties["TestResultsDirectory"] = deepResults;
        _testMethod.Setup(tm => tm.Name).Returns("MyTest");
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();

        // The adaptive budget / fallback is a Windows MAX_PATH concern only.
        bool onWindows = Path.DirectorySeparatorChar == '\\';
        if (onWindows)
        {
            // Fallback engaged: created directly under the short system temp directory, and the
            // total path stays within MAX_PATH minus the reserved headroom for the test's files.
            string tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Path.GetDirectoryName(tempDirectory).Should().Be(tempRoot);
            tempDirectory!.Length.Should().BeLessThanOrEqualTo(260 - 80);
        }
        else
        {
            Path.GetDirectoryName(tempDirectory).Should().Be(deepResults);
        }

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldFallBackToTempPathWhenResultsDirectoryIsNotWritable()
    {
        // TestResultsDirectory is rarely empty in the normal .NET path (it maps to the test
        // assembly's output directory when no results directory is configured), so the "unavailable"
        // fallback rarely fires. This covers the more realistic case: the base directory exists but
        // cannot be written to. It is simulated by pointing at a *file*, so creating a subdirectory
        // under it throws — the implementation must fall back to the system temp directory rather
        // than surface a directory-creation error from the property getter.
        using TempDirectoryScope scope = new();
        string filePath = Path.Combine(scope.Path, "not_a_directory");
        File.WriteAllText(filePath, "x");
        _properties["TestResultsDirectory"] = filePath;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();

        // Fell back to the system temp root, not under the (unwritable) results path.
        string tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Path.GetDirectoryName(tempDirectory).Should().Be(tempRoot);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldNotSplitSurrogatePairsWhenTruncatingName()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;

        // A long display name of non-BMP characters (emoji are surrogate pairs). A leading ASCII
        // char biases the truncation boundary onto a high surrogate, exercising the guard.
        string emojiName = "a" + string.Concat(Enumerable.Repeat("\U0001F600", 60));
        _testMethod.Setup(tm => tm.Name).Returns(emojiName);
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();

        // The resulting segment must contain no unpaired surrogate (truncation split a pair).
        string fileName = Path.GetFileName(tempDirectory!);
        for (int i = 0; i < fileName.Length; i++)
        {
            if (char.IsHighSurrogate(fileName[i]))
            {
                (i + 1 < fileName.Length && char.IsLowSurrogate(fileName[i + 1]))
                    .Should().BeTrue("a high surrogate must be followed by a low surrogate");
                i++;
            }
            else
            {
                char.IsLowSurrogate(fileName[i]).Should().BeFalse("no unpaired low surrogate is allowed");
            }
        }

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldReturnSamePathOnRepeatedAccess()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? first = _testContextImplementation.TestTempDirectory;
        string? second = _testContextImplementation.TestTempDirectory;

        second.Should().Be(first);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
    }

    public void TestTempDirectoryShouldBeRetainedWhenOutcomeChangesToFailedAfterCreation()
    {
        // Regression for the folded data-driven path: the framework sets the context outcome to
        // Passed *before* running [TestCleanup]/Dispose, then re-syncs it to the post-cleanup
        // outcome before the (cloned) context is disposed. The final outcome before disposal must
        // win, otherwise a row whose body passes but whose cleanup fails would have its directory
        // deleted.
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed); // pre-cleanup
        _testContextImplementation.SetOutcome(UnitTestOutcome.Failed);  // post-cleanup re-sync
        _testContextImplementation.Dispose();

        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    public void TestTempDirectoryShouldBeUniqueAcrossContexts()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testMethod.Setup(tm => tm.Name).Returns("MyTest");

        using TestContextImplementation context1 = CreateTestContextImplementation();
        using TestContextImplementation context2 = CreateTestContextImplementation();

        string? path1 = context1.TestTempDirectory;
        string? path2 = context2.TestTempDirectory;

        path1.Should().NotBe(path2);
        Directory.Exists(path1).Should().BeTrue();
        Directory.Exists(path2).Should().BeTrue();

        context1.SetOutcome(UnitTestOutcome.Passed);
        context2.SetOutcome(UnitTestOutcome.Passed);
    }

    public void TestTempDirectoryShouldBeUniqueAcrossDataDrivenClones()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testMethod.Setup(tm => tm.Name).Returns("MyTest");
        _testContextImplementation = CreateTestContextImplementation();

        using TestContextImplementation clone1 = _testContextImplementation.CloneForDataDrivenIteration();
        using TestContextImplementation clone2 = _testContextImplementation.CloneForDataDrivenIteration();

        string? path1 = clone1.TestTempDirectory;
        string? path2 = clone2.TestTempDirectory;

        path1.Should().NotBe(path2);
        Directory.Exists(path1).Should().BeTrue();
        Directory.Exists(path2).Should().BeTrue();

        clone1.SetOutcome(UnitTestOutcome.Passed);
        clone2.SetOutcome(UnitTestOutcome.Passed);
    }

    public void TestTempDirectoryShouldBeDeletedOnPass()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        File.WriteAllText(Path.Combine(tempDirectory!, "artifact.txt"), "data");

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    public void TestTempDirectoryShouldBeRetainedOnPassWhenResultFileRegisteredUnderIt()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        string resultFile = Path.Combine(tempDirectory!, "attachment.txt");
        File.WriteAllText(resultFile, "data");
        _testContextImplementation.AddResultFile(resultFile);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        // Even on pass, the directory is retained because it holds a registered result file that
        // the test host collects as an attachment after this context is disposed.
        Directory.Exists(tempDirectory).Should().BeTrue();
        File.Exists(resultFile).Should().BeTrue();
    }

    public void TestTempDirectoryShouldBeDeletedOnPassWhenResultFileIsOutsideIt()
    {
        using TempDirectoryScope resultsDirectory = new();
        using TempDirectoryScope elsewhere = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        // A registered result file that does NOT live under the temp directory must not keep the
        // temp directory alive on pass.
        string outsideResultFile = Path.Combine(elsewhere.Path, "attachment.txt");
        File.WriteAllText(outsideResultFile, "data");
        _testContextImplementation.AddResultFile(outsideResultFile);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    public void TestTempDirectoryShouldBeDeletedOnPassWhenOnlyEarlierRetryAttemptRegisteredResultFile()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        // Attempt 1 (e.g. a failed retry attempt) registers a result file under the temp directory,
        // then the framework consumes the list via GetResultFiles (as it does once per attempt).
        string attempt1File = Path.Combine(tempDirectory!, "attempt1.txt");
        File.WriteAllText(attempt1File, "data");
        _testContextImplementation.AddResultFile(attempt1File);
        _testContextImplementation.GetResultFiles();

        // Attempt 2 (the passing, reported attempt) registers nothing; the framework consumes again.
        _testContextImplementation.GetResultFiles();

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();

        // The reported (last) attempt registered no in-directory result file, so the sticky marker
        // from the earlier attempt must not keep the passing test's directory alive.
        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    public void TestTempDirectoryShouldBeRetainedOnFailure()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        File.WriteAllText(Path.Combine(tempDirectory!, "artifact.txt"), "data");

        _testContextImplementation.SetOutcome(UnitTestOutcome.Failed);
        _testContextImplementation.Dispose();

        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    public void TestTempDirectoryShouldBeRetainedWhenEnvironmentVariableIsSet()
    {
        using TempDirectoryScope resultsDirectory = new();
        string? original = Environment.GetEnvironmentVariable("MSTEST_TEST_TEMP_DIRECTORY_RETAIN");
        try
        {
            Environment.SetEnvironmentVariable("MSTEST_TEST_TEMP_DIRECTORY_RETAIN", "1");
            _properties["TestResultsDirectory"] = resultsDirectory.Path;
            _testContextImplementation = CreateTestContextImplementation();

            string? tempDirectory = _testContextImplementation.TestTempDirectory;

            _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
            _testContextImplementation.Dispose();

            Directory.Exists(tempDirectory).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSTEST_TEST_TEMP_DIRECTORY_RETAIN", original);
        }
    }

    public void TestTempDirectoryCleanupShouldSwallowErrors()
    {
        using TempDirectoryScope resultsDirectory = new();
        _properties["TestResultsDirectory"] = resultsDirectory.Path;
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;
        string lockedFilePath = Path.Combine(tempDirectory!, "locked.txt");

        // Hold an exclusive handle so recursive delete throws; Dispose must not propagate it.
        using FileStream lockStream = new(lockedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        Action dispose = () => _testContextImplementation.Dispose();
        dispose.Should().NotThrow();
    }

    public void TestTempDirectoryShouldFallBackToTempPathWhenNoResultsDirectory()
    {
        _testContextImplementation = CreateTestContextImplementation();

        string? tempDirectory = _testContextImplementation.TestTempDirectory;

        tempDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(tempDirectory).Should().BeTrue();

        // Pin the fallback location: with no results directory, it is created directly under the
        // system temp root.
        string tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Path.GetDirectoryName(tempDirectory).Should().Be(tempRoot);

        _testContextImplementation.SetOutcome(UnitTestOutcome.Passed);
        _testContextImplementation.Dispose();
        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mstest_ut_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup of the test's own scratch directory.
            }
        }
    }
#endif
}
