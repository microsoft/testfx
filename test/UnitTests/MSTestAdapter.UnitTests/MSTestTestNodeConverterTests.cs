// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

/// <summary>
/// Unit tests for <see cref="MSTestTestNodeConverter"/> and the Microsoft.Testing.Platform-native
/// discovery/result seams (<see cref="MtpUnitTestElementSink"/>, <see cref="MtpTestResultRecorder"/>).
/// </summary>
public sealed class MSTestTestNodeConverterTests : TestContainer
{
    private static UnitTestElement CreateElement(string? managedMethodName = "MyMethod", string fullClassName = "MyNamespace.MyClass", string name = "MyMethod")
    {
        var testMethod = new TestMethod(managedMethodName, hierarchyValues: null, name, fullClassName, "MyAssembly.dll", displayName: null, parameterTypes: null);
        return new UnitTestElement(testMethod);
    }

    // --- Discovery node ---------------------------------------------------------------------------------------
    public void ToDiscoveredTestNode_SetsUidDisplayNameAndDiscoveredState()
    {
        UnitTestElement element = CreateElement();

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.Uid.Value.Should().Be(element.GetTestId().ToString());
        node.DisplayName.Should().Be("MyMethod");
        node.Properties.Any<DiscoveredTestNodeStateProperty>().Should().BeTrue();
        node.Properties.Any<InProgressTestNodeStateProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_UsesExplicitDisplayName_WhenProvided()
    {
        var testMethod = new TestMethod("MyMethod", hierarchyValues: null, "MyMethod", "MyNamespace.MyClass", "MyAssembly.dll", displayName: "Friendly name", parameterTypes: null);
        var element = new UnitTestElement(testMethod);

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.DisplayName.Should().Be("Friendly name");
    }

    public void ToInProgressTestNode_AddsInProgressState()
    {
        TestNode node = MSTestTestNodeConverter.ToInProgressTestNode(CreateElement(), isTrxEnabled: false);

        node.Properties.Any<InProgressTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToInProgressTestNode_AddsTestMethodIdentifier_FromManagedNames()
    {
        // In-progress nodes must carry TestMethodIdentifierProperty: it is consumed by
        // OpenTelemetryResultHandler.NotifyInProgress (test.method/class/namespace/assembly tags) and by the
        // slow-test reporters via TestNodeIdentity, which otherwise fall back to the display name.
        TestNode node = MSTestTestNodeConverter.ToInProgressTestNode(CreateElement(), isTrxEnabled: false);

        node.Properties.Any<TestMethodIdentifierProperty>().Should().BeTrue();
    }

    public void ToDiscoveredTestNode_AddsTestMethodIdentifier_FromManagedNames()
    {
        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(), isTrxEnabled: false);

        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        identifier.Should().NotBeNull();
        identifier!.Namespace.Should().Be("MyNamespace");
        identifier.TypeName.Should().Be("MyClass");
        identifier.MethodName.Should().Be("MyMethod");
        identifier.AssemblyFullName.Should().BeEmpty();
        identifier.ReturnTypeFullName.Should().BeEmpty();
    }

    public void ToDiscoveredTestNode_DoesNotAddTestMethodIdentifier_WhenNoManagedMethodName()
    {
        UnitTestElement element = CreateElement(managedMethodName: null);

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.Properties.Any<TestMethodIdentifierProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_DoesNotAddTestMethodIdentifier_WhenManagedTypeNameIsEmpty()
    {
        // ManagedTypeName is derived from FullClassName, so an empty class name leaves no usable type identity.
        UnitTestElement element = CreateElement(fullClassName: string.Empty);

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.Properties.Any<TestMethodIdentifierProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_AddsFileLocation_WhenDeclaringFileKnown()
    {
        UnitTestElement element = CreateElement();
        element.DeclaringFilePath = "C:\\src\\MyClass.cs";
        element.DeclaringLineNumber = 42;

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        TestFileLocationProperty? location = node.Properties.SingleOrDefault<TestFileLocationProperty>();
        location.Should().NotBeNull();
        location!.FilePath.Should().Be("C:\\src\\MyClass.cs");
        location.LineSpan.Start.Line.Should().Be(42);
    }

    public void ToDiscoveredTestNode_DoesNotAddFileLocation_WhenDeclaringFileUnknown()
    {
        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(), isTrxEnabled: false);

        node.Properties.Any<TestFileLocationProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_AddsCategoriesAndTraitsAsMetadata()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["CategoryA", "CategoryB"];
        element.Traits = [new TestTrait("Owner", "Alice")];

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        TestMetadataProperty[] metadata = node.Properties.OfType<TestMetadataProperty>();
        metadata.Should().Contain(p => p.Key == "CategoryA" && p.Value == string.Empty);
        metadata.Should().Contain(p => p.Key == "CategoryB" && p.Value == string.Empty);
        metadata.Should().Contain(p => p.Key == "Owner" && p.Value == "Alice");
    }

    public void ToDiscoveredTestNode_AddsTrxCategories_OnlyWhenTrxEnabled()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["CategoryA"];

        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false)
            .Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>().Should().BeFalse();

        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: true)
            .Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>().Should().BeTrue();
    }

    public void ToDiscoveredTestNode_AddsTrxWorkItems_OnlyWhenTrxEnabled()
    {
        UnitTestElement element = CreateElement();
        element.WorkItemIds = ["123", "456"];

        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false)
            .Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty>().Should().BeFalse();

        Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty property = MSTestTestNodeConverter
            .ToDiscoveredTestNode(element, isTrxEnabled: true)
            .Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty>();
        property.WorkItemIds.Should().Equal("123", "456");
        property.WorkItemIds.Should().NotBeSameAs(element.WorkItemIds);
    }

    public void RepeatedConversions_ReuseImmutableBaseProperties_WithoutSharingNodesOrPropertyBags()
    {
        UnitTestElement element = CreateElement();
        element.DeclaringFilePath = "C:\\src\\MyClass.cs";
        element.DeclaringLineNumber = 42;
        element.TestCategory = ["CategoryA"];
        element.Traits = [new TestTrait("Owner", "Alice")];

        TestNode discovered = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);
        TestNode inProgress = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: false);
        TestNode result = MSTestTestNodeConverter.ToResultTestNode(
            element,
            new FrameworkTestResult { Outcome = UnitTestOutcome.Passed },
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            isTrxEnabled: false,
            new MSTestSettings());

        discovered.Should().NotBeSameAs(inProgress);
        inProgress.Should().NotBeSameAs(result);
        discovered.Properties.Should().NotBeSameAs(inProgress.Properties);
        inProgress.Properties.Should().NotBeSameAs(result.Properties);

        inProgress.Uid.Should().BeSameAs(discovered.Uid);
        result.Uid.Should().BeSameAs(discovered.Uid);
        inProgress.Properties.Single<TestFileLocationProperty>().Should().BeSameAs(discovered.Properties.Single<TestFileLocationProperty>());
        result.Properties.Single<TestFileLocationProperty>().Should().BeSameAs(discovered.Properties.Single<TestFileLocationProperty>());
        TestMetadataProperty[] discoveredMetadata = discovered.Properties.OfType<TestMetadataProperty>();
        TestMetadataProperty[] inProgressMetadata = inProgress.Properties.OfType<TestMetadataProperty>();
        TestMetadataProperty[] resultMetadata = result.Properties.OfType<TestMetadataProperty>();
        inProgressMetadata.Should().BeEquivalentTo(discoveredMetadata);
        resultMetadata.Should().BeEquivalentTo(discoveredMetadata);
        foreach (TestMetadataProperty metadata in discoveredMetadata)
        {
            inProgressMetadata.Single(candidate => candidate.Equals(metadata)).Should().BeSameAs(metadata);
            resultMetadata.Single(candidate => candidate.Equals(metadata)).Should().BeSameAs(metadata);
        }

        inProgress.Properties.Single<TestMethodIdentifierProperty>().Should().BeSameAs(discovered.Properties.Single<TestMethodIdentifierProperty>());
        result.Properties.Single<TestMethodIdentifierProperty>().Should().BeSameAs(discovered.Properties.Single<TestMethodIdentifierProperty>());

        discovered.Properties.Add(new TestMetadataProperty("MessageOnly", "discovered"));
        inProgress.Properties.Any<TestMetadataProperty>().Should().BeTrue();
        inProgress.Properties.OfType<TestMetadataProperty>().Should().NotContain(p => p.Key == "MessageOnly");
    }

    public void TrxCategories_AreCopiedPerNode_AndCannotCrossMutate()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["CategoryA", "CategoryB"];
        element.Traits = [new TestTrait("Owner", "Alice")];

        TestNode first = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: true);
        TestNode second = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: true);

        Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty firstCategories =
            first.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>();
        Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty secondCategories =
            second.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>();

        firstCategories.Should().NotBeSameAs(secondCategories);
        firstCategories.Categories.Should().NotBeSameAs(secondCategories.Categories);
        firstCategories.Categories[0] = "Mutated";
        secondCategories.Categories.Should().Equal("CategoryA", "CategoryB");

        TestMetadataProperty[] firstMetadata = first.Properties.OfType<TestMetadataProperty>();
        TestMetadataProperty[] secondMetadata = second.Properties.OfType<TestMetadataProperty>();
        secondMetadata.Should().BeEquivalentTo(firstMetadata);
        foreach (TestMetadataProperty metadata in firstMetadata)
        {
            secondMetadata.Single(candidate => candidate.Equals(metadata)).Should().BeSameAs(metadata);
        }
    }

    public void TrxWorkItems_AreCopiedPerNode_AndCannotCrossMutate()
    {
        UnitTestElement element = CreateElement();
        element.WorkItemIds = ["123", "456"];

        TestNode first = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: true);
        TestNode second = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: true);

        Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty firstWorkItems =
            first.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty>();
        Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty secondWorkItems =
            second.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxWorkItemsProperty>();

        firstWorkItems.Should().NotBeSameAs(secondWorkItems);
        firstWorkItems.WorkItemIds.Should().NotBeSameAs(secondWorkItems.WorkItemIds);
        firstWorkItems.WorkItemIds[0] = "Mutated";
        secondWorkItems.WorkItemIds.Should().Equal("123", "456");
    }

    public void ResultDisplayNameOverride_RemainsPerResult_WhileTrxDefinitionUsesTestDefinitionName()
    {
        UnitTestElement element = CreateElement();
        element.TestMethod.DisplayName = "Test definition";

        TestNode first = MSTestTestNodeConverter.ToResultTestNode(
            element,
            new FrameworkTestResult { Outcome = UnitTestOutcome.Passed, DisplayName = "Data row 1" },
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            isTrxEnabled: true,
            new MSTestSettings());
        TestNode second = MSTestTestNodeConverter.ToResultTestNode(
            element,
            new FrameworkTestResult { Outcome = UnitTestOutcome.Passed, DisplayName = "Data row 2" },
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            isTrxEnabled: true,
            new MSTestSettings());

        first.DisplayName.Should().Be("Data row 1");
        second.DisplayName.Should().Be("Data row 2");
        first.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxTestDefinitionName>().TestDefinitionName.Should().Be("Test definition");
        second.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxTestDefinitionName>().TestDefinitionName.Should().Be("Test definition");
    }

    public void CloneSpecializedAsDataRow_DoesNotReuseParentCachedIdentityOrMetadata()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["Parent"];
        Guid parentId = element.GetTestId();
        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        UnitTestElement dataRow = element.Clone();
        dataRow.TestMethod.DisplayName = "Data row";
        dataRow.TestMethod.DataType = DynamicDataType.ITestDataSource;
        dataRow.TestMethod.TestCaseIndex = 1;
        dataRow.TestCategory = ["Row"];

        TestNode dataRowNode = MSTestTestNodeConverter.ToDiscoveredTestNode(dataRow, isTrxEnabled: false);

        dataRowNode.Uid.Value.Should().NotBe(parentId.ToString());
        dataRowNode.DisplayName.Should().Be("Data row");
        dataRowNode.Properties.OfType<TestMetadataProperty>().Should().ContainSingle(p => p.Key == "Row");
        dataRowNode.Properties.OfType<TestMetadataProperty>().Should().NotContain(p => p.Key == "Parent");
    }

    public void SourceUpdatedClone_DoesNotReuseOriginalCachedBaseData()
    {
        UnitTestElement element = CreateElement();
        TestNode original = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        UnitTestElement relocated = element.CloneWithSource("OtherAssembly.dll");
        relocated.DeclaringFilePath = "C:\\deployed\\MyClass.cs";
        TestNode updated = MSTestTestNodeConverter.ToDiscoveredTestNode(relocated, isTrxEnabled: false);

        updated.Uid.Value.Should().NotBe(original.Uid.Value);
        updated.Properties.Single<TestFileLocationProperty>().FilePath.Should().Be("C:\\deployed\\MyClass.cs");
    }

    public void ConcurrentConversions_AreSafeAndPreserveIndependentMutableProperties()
    {
        UnitTestElement element = CreateElement(managedMethodName: "MyMethod(System.String)");
        element.TestCategory = ["CategoryA"];
        var nodes = new TestNode[128];

        Parallel.For(0, nodes.Length, i => nodes[i] = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: true));

        nodes.Select(node => node.Uid.Value).Should().OnlyContain(uid => uid == nodes[0].Uid.Value);
        nodes.Select(node => node.Properties).Distinct().Should().HaveCount(nodes.Length);

        string[][] parameterTypes = [.. nodes.Select(node => node.Properties.Single<TestMethodIdentifierProperty>().ParameterTypeFullNames)];
        parameterTypes.Distinct().Should().HaveCount(nodes.Length);

        string[][] categories = [.. nodes.Select(node => node.Properties.Single<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>().Categories)];
        categories.Distinct().Should().HaveCount(nodes.Length);
    }

    public void BaseDataCache_DoesNotKeepElementAlive()
    {
        WeakReference elementReference = CreateWeakReferenceToConvertedElement();

        for (int i = 0; elementReference.IsAlive && i < 10; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        elementReference.IsAlive.Should().BeFalse();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakReferenceToConvertedElement()
    {
        UnitTestElement element = CreateElement();
        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: true);
        return new WeakReference(element);
    }

    // --- Result node: outcomes --------------------------------------------------------------------------------
    public void ToResultTestNode_MapsPassedOutcome()
    {
        TestNode node = ResultNode(UnitTestOutcome.Passed);

        node.Properties.Any<PassedTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsFailedOutcome_WithMessageAndStackTrace()
    {
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom", ExceptionStackTrace = "at Some.Method()" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        FailedTestNodeStateProperty? failed = node.Properties.SingleOrDefault<FailedTestNodeStateProperty>();
        failed.Should().NotBeNull();
        failed!.Exception!.Message.Should().Be("boom");
        failed.Exception.StackTrace.Should().Be("at Some.Method()");
    }

    public void ToResultTestNode_AddsAssertionFailureProperty_WhenResultCarriesAssertionTexts()
    {
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Failed,
            ExceptionMessage = "Assert.AreEqual failed.",
            ExceptionExpectedText = "5",
            ExceptionActualText = "2",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        AssertionFailureProperty? assertionFailure = node.Properties.SingleOrDefault<AssertionFailureProperty>();
        assertionFailure.Should().NotBeNull();
        assertionFailure!.Expected.Should().Be("5");
        assertionFailure.Actual.Should().Be("2");
    }

    public void ToResultTestNode_AddsAssertionFailureProperty_WhenOnlyExpectedTextIsKnown()
    {
        // Some assertions (e.g. CollectionAssert.Contains) only have a natural expected value.
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Failed,
            ExceptionMessage = "CollectionAssert.Contains failed.",
            ExceptionExpectedText = "\"c\"",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        AssertionFailureProperty? assertionFailure = node.Properties.SingleOrDefault<AssertionFailureProperty>();
        assertionFailure.Should().NotBeNull();
        assertionFailure!.Expected.Should().Be("\"c\"");
        assertionFailure.Actual.Should().BeNull();
    }

    public void ToResultTestNode_DoesNotAddAssertionFailureProperty_WhenFailureIsNotAnAssertion()
    {
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        node.Properties.Any<AssertionFailureProperty>().Should().BeFalse();
    }

    public void ToResultTestNode_MapsIgnoredOutcomeToSkipped()
    {
        TestNode node = ResultNode(UnitTestOutcome.Ignored);

        node.Properties.Any<SkippedTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsNotFoundOutcomeToError()
    {
        TestNode node = ResultNode(UnitTestOutcome.NotFound);

        node.Properties.Any<ErrorTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsInconclusiveToSkipped_ByDefault()
    {
        // Default MSTestSettings has MapInconclusiveToFailed = false.
        TestNode node = ResultNode(UnitTestOutcome.Inconclusive);

        node.Properties.Any<SkippedTestNodeStateProperty>().Should().BeTrue();
    }

    // --- Result node: timing, output, attachments -------------------------------------------------------------
    public void ToResultTestNode_AddsTimingProperty()
    {
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        DateTimeOffset end = start.AddSeconds(2);
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Passed, Duration = TimeSpan.FromSeconds(2) };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, start, end, isTrxEnabled: false, new MSTestSettings());

        TimingProperty? timing = node.Properties.SingleOrDefault<TimingProperty>();
        timing.Should().NotBeNull();
        timing!.GlobalTiming.Duration.Should().Be(TimeSpan.FromSeconds(2));
    }

    public void ToResultTestNode_MapsLogOutputAndLogErrorToStandardStreams()
    {
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Passed,
            LogOutput = "hello out",
            LogError = "hello err",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        node.Properties.SingleOrDefault<StandardOutputProperty>()!.StandardOutput.Should().Contain("hello out");
        node.Properties.SingleOrDefault<StandardErrorProperty>()!.StandardError.Should().Contain("hello err");
    }

    public void ToResultTestNode_IncludesDebugTraceAndTestContextBannersInStandardOutput()
    {
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Passed,
            DebugTrace = "some trace",
            TestContextMessages = "some context",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        string standardOutput = node.Properties.SingleOrDefault<StandardOutputProperty>()!.StandardOutput;
        standardOutput.Should().Contain("Debug Trace:").And.Contain("some trace");
        standardOutput.Should().Contain("TestContext Messages:").And.Contain("some context");
    }

    // --- Result node: TRX properties --------------------------------------------------------------------------
    public void ToResultTestNode_AddsTrxProperties_WhenTrxEnabled()
    {
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom", ExceptionStackTrace = "at X()" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: true, new MSTestSettings());

        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxExceptionProperty>().Should().BeTrue();
        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxMessagesProperty>().Should().BeTrue();
        Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty? typeName =
            node.Properties.SingleOrDefault<Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty>();
        typeName.Should().NotBeNull();
        typeName!.FullyQualifiedTypeName.Should().Be("MyNamespace.MyClass");
    }

    public void ToResultTestNode_AddsTestMethodIdentifier_FromManagedNames()
    {
        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        identifier.Should().NotBeNull();
        identifier!.Namespace.Should().Be("MyNamespace");
        identifier.TypeName.Should().Be("MyClass");
        identifier.MethodName.Should().Be("MyMethod");
    }

    public void ToResultTestNode_UsesFullClassNameForTrxTypeName_WhenNoManagedMethodName()
    {
        // Without a managed method name, no TestMethodIdentifierProperty is added, so the TRX fully-qualified type
        // name falls back to TestMethod.FullClassName directly (the behavior that replaced TryParseFullyQualifiedType).
        UnitTestElement element = CreateElement(managedMethodName: null);
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom", ExceptionStackTrace = "at X()" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(element, result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: true, new MSTestSettings());

        node.Properties.Any<TestMethodIdentifierProperty>().Should().BeFalse();
        Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty? typeName =
            node.Properties.SingleOrDefault<Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty>();
        typeName.Should().NotBeNull();
        typeName!.FullyQualifiedTypeName.Should().Be("MyNamespace.MyClass");
    }

    public void ToResultTestNode_DoesNotAddTrxProperties_WhenTrxDisabled()
    {
        TestNode node = ResultNode(UnitTestOutcome.Passed);

        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxExceptionProperty>().Should().BeFalse();
        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxMessagesProperty>().Should().BeFalse();
    }

    // --- Base-property caching ---------------------------------------------------------------------------------
    public void ToResultTestNode_ReusesCachedManagedNameParse_FromInProgressNode()
    {
        // The managed-name parse is retained by the element's base descriptor, so the in-progress node and every
        // result node must still agree on every field of the identifier.
        UnitTestElement element = CreateElement();

        TestMethodIdentifierProperty inProgress = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: false)
            .Properties.Single<TestMethodIdentifierProperty>();
        TestMethodIdentifierProperty result = MSTestTestNodeConverter.ToResultTestNode(element, new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings())
            .Properties.Single<TestMethodIdentifierProperty>();

        result.Should().Be(inProgress);

        // Namespace and TypeName are partial Substring results of ManagedTypeName, so re-running the parse would
        // hand back fresh string instances. Reference equality is therefore what proves the cached parse was
        // reused rather than redone - without the cache these assertions fail while the value equality above
        // still passes.
        result.Namespace.Should().BeSameAs(inProgress.Namespace);
        result.TypeName.Should().BeSameAs(inProgress.TypeName);
    }

    public void ToResultTestNode_ReusesTestMethodIdentifierInstance_ForParameterlessTestMethod()
    {
        // A parameterless identifier is fully immutable (readonly strings plus an empty parameter array, which
        // cannot be mutated), so the cached descriptor hands back the very same property instance instead of
        // allocating a new one for every node built from the same test method.
        UnitTestElement element = CreateElement();

        TestMethodIdentifierProperty inProgress = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: false)
            .Properties.Single<TestMethodIdentifierProperty>();
        TestMethodIdentifierProperty result = MSTestTestNodeConverter.ToResultTestNode(element, new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings())
            .Properties.Single<TestMethodIdentifierProperty>();

        result.Should().BeSameAs(inProgress);
    }

    public void ToResultTestNode_DoesNotReuseTestMethodIdentifierInstance_ForParameterizedTestMethod()
    {
        // Parameterized identifiers expose a mutable array, so each node must keep getting its own property.
        UnitTestElement element = CreateElement(managedMethodName: "MyMethod(System.String)");

        TestMethodIdentifierProperty inProgress = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: false)
            .Properties.Single<TestMethodIdentifierProperty>();
        TestMethodIdentifierProperty result = MSTestTestNodeConverter.ToResultTestNode(element, new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings())
            .Properties.Single<TestMethodIdentifierProperty>();

        result.Should().NotBeSameAs(inProgress);
    }

    public void ToResultTestNode_DoesNotShareParameterTypeArray_WithInProgressNode()
    {
        // TestMethodIdentifierProperty exposes ParameterTypeFullNames publicly, so nodes must never alias one
        // array: a consumer that writes to it would otherwise corrupt every other node of the same test method.
        UnitTestElement element = CreateElement(managedMethodName: "MyMethod(System.String)");

        TestMethodIdentifierProperty inProgress = MSTestTestNodeConverter.ToInProgressTestNode(element, isTrxEnabled: false)
            .Properties.Single<TestMethodIdentifierProperty>();
        TestMethodIdentifierProperty result = MSTestTestNodeConverter.ToResultTestNode(element, new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings())
            .Properties.Single<TestMethodIdentifierProperty>();

        inProgress.ParameterTypeFullNames.Should().Equal("System.String");
        result.ParameterTypeFullNames.Should().Equal("System.String");
        result.ParameterTypeFullNames.Should().NotBeSameAs(inProgress.ParameterTypeFullNames);
    }

    public void ToDiscoveredTestNode_DoesNotShareTestMethodIdentifier_AcrossDistinctTestMethods()
    {
        // The cache is keyed on the TestMethod instance, so two different test methods must never be conflated.
        TestMethodIdentifierProperty? first = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(managedMethodName: "MethodA", name: "MethodA"), isTrxEnabled: false)
            .Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        TestMethodIdentifierProperty? second = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(managedMethodName: "MethodB", name: "MethodB"), isTrxEnabled: false)
            .Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second.Should().NotBeSameAs(first);
        first!.MethodName.Should().Be("MethodA");
        second!.MethodName.Should().Be("MethodB");
    }

    // --- GetTestId caching ------------------------------------------------------------------------------------
    public void GetTestId_CachesComputedId_AndReturnsSameValueOnSubsequentCalls()
    {
        UnitTestElement element = CreateElement();

        element.CachedTestNodeUid.Should().BeNull();

        Guid first = element.GetTestId();

        element.CachedTestNodeUid.Should().Be(first);

        Guid second = element.GetTestId();

        second.Should().Be(first);
    }

    public void CloneWithSource_InvalidatesCachedTestId()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();
        element.CachedTestNodeUid.Should().Be(original);

        UnitTestElement clone = element.CloneWithSource("OtherAssembly.dll");

        // The clone must start with a cleared cache and recompute a distinct id for the new source.
        clone.CachedTestNodeUid.Should().BeNull();
        clone.GetTestId().Should().NotBe(original);
        // The original element keeps its cached id untouched.
        element.CachedTestNodeUid.Should().Be(original);
    }

    public void WithUpdatedSource_InvalidatesCachedTestId_WhenSourceChanges()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();

        UnitTestElement result = element.WithUpdatedSource("OtherAssembly.dll");

        result.Should().NotBeSameAs(element);
        result.CachedTestNodeUid.Should().BeNull();
        result.GetTestId().Should().NotBe(original);
    }

    public void CloneWithUpdatedSource_InvalidatesCachedTestId()
    {
        UnitTestElement element = CreateElement();
        element.GetTestId();
        element.CachedTestNodeUid.Should().NotBeNull();

        UnitTestElement clone = element.CloneWithUpdatedSource("OtherAssembly.dll");

        clone.CachedTestNodeUid.Should().BeNull();
    }

    public void Clone_InvalidatesCachedTestId_SoTheCloneCanBeSpecializedAsADataRow()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();

        UnitTestElement clone = element.Clone();

        clone.CachedTestNodeUid.Should().BeNull();
        element.CachedTestNodeUid.Should().Be(original);
    }

    // --- Seams ------------------------------------------------------------------------------------------------
    public async Task MtpUnitTestElementSink_PublishesDiscoveredTestNode()
    {
        var messageBus = new CapturingMessageBus();
        var sessionUid = new SessionUid("session-1");
        var sink = new MtpUnitTestElementSink(messageBus, new StubDataProducer(), sessionUid, isTrxEnabled: false);

        await sink.SendTestElementAsync(CreateElement());

        messageBus.Published.Should().ContainSingle();
        var message = (TestNodeUpdateMessage)messageBus.Published[0];
        message.SessionUid.Value.Should().Be("session-1");
        message.TestNode.Properties.Any<DiscoveredTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordStart_PublishesInProgressNode()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        await recorder.RecordStartAsync(CreateElement());

        var message = (TestNodeUpdateMessage)messageBus.Published.Single();
        message.TestNode.Properties.Any<InProgressTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordEmptyResult_PublishesExecutionCompletedNode()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        await recorder.RecordEmptyResultAsync(CreateElement());

        var message = (TestNodeUpdateMessage)messageBus.Published.Single();
        message.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>().Should().BeTrue();
        message.TestNode.Properties.Any<TestNodeStateProperty>().Should().BeFalse();
    }

    public async Task MtpTestResultRecorder_RecordResult_PublishesResultNodeAndReturnsFailedFlag()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "nope" };

        bool isFailed = await recorder.RecordResultAsync(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now);

        isFailed.Should().BeTrue();
        var message = (TestNodeUpdateMessage)messageBus.Published.Single();
        message.TestNode.Properties.Any<FailedTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordResult_ReturnsFalse_ForPassedTest()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        bool isFailed = await recorder.RecordResultAsync(CreateElement(), new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now);

        isFailed.Should().BeFalse();
    }

    private static TestNode ResultNode(UnitTestOutcome outcome)
        => MSTestTestNodeConverter.ToResultTestNode(CreateElement(), new FrameworkTestResult { Outcome = outcome }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

    private sealed class CapturingMessageBus : IMessageBus
    {
        public List<IData> Published { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            Published.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class StubDataProducer : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

        public string Uid => "stub";

        public string Version => "1.0.0";

        public string DisplayName => "Stub";

        public string Description => "Stub data producer for tests.";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
