// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Builds Microsoft.Testing.Platform <see cref="TestNode"/>s directly from MSTest's neutral execution model
/// (<see cref="UnitTestElement"/> and the framework <see cref="FrameworkTestResult"/>), without going through the
/// VSTest object model (<c>TestCase</c>/<c>TestResult</c>) or the VSTest bridge.
/// </summary>
/// <remarks>
/// This mirrors, field-for-field, the mapping that the VSTest bridge performed in
/// <c>ObjectModelConverters.ToTestNode</c> (combined with <c>UnitTestElementExtensions.ToTestCase</c>,
/// <c>TestResultExtensions.ToTestResult</c> and the bridge's <c>AddAdditionalProperties</c>), so
/// switching MSTest to a native Microsoft.Testing.Platform integration produces identical <see cref="TestNode"/>s.
/// MSTest does not use the <c>vstestProvider</c> named-feature capability, so the VSTest provider properties the
/// bridge conditionally emits are intentionally not reproduced here.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal static class MSTestTestNodeConverter
{
    /// <summary>
    /// Caches the immutable base representation used to create every node for a test element.
    /// </summary>
    /// <remarks>
    /// A conversion still creates a distinct <see cref="TestNode"/> and <see cref="PropertyBag"/> for every message.
    /// Only immutable properties are shared. Properties that expose mutable arrays are materialized from private
    /// snapshots for each node. The weak key naturally bounds the cache to the lifetime of the element.
    /// <para>
    /// Although <see cref="UnitTestElement"/> is mutable while discovery constructs it, the element is fully
    /// specialized before its first node is published. Data-row and source transformations create distinct element
    /// clones, so later lifecycle messages for one element observe the same base metadata captured here.
    /// </para>
    /// </remarks>
#pragma warning disable IDE0028 // ConditionalWeakTable is not collection-expression-constructible on .NET Framework (CS9174).
    private static readonly ConditionalWeakTable<UnitTestElement, BaseTestNodeData> BaseTestNodeDataCache = new();
#pragma warning restore IDE0028

    /// <summary>
    /// Builds a discovered-state <see cref="TestNode"/> for a discovered test.
    /// </summary>
    public static TestNode ToDiscoveredTestNode(UnitTestElement element, bool isTrxEnabled)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, displayNameOverride: null, out _);
        testNode.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
        return testNode;
    }

    /// <summary>
    /// Builds an in-progress-state <see cref="TestNode"/> reported when a test starts executing.
    /// </summary>
    public static TestNode ToInProgressTestNode(UnitTestElement element, bool isTrxEnabled)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, displayNameOverride: null, out _);
        testNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
        return testNode;
    }

    /// <summary>
    /// Builds a state-neutral node update that signals execution completed without producing a result.
    /// </summary>
    public static TestNode ToEmptyResultTestNode(UnitTestElement element, bool isTrxEnabled)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, displayNameOverride: null, out _);
        testNode.Properties.Add(TestNodeExecutionCompletedProperty.CachedInstance);
        return testNode;
    }

    /// <summary>
    /// Builds a completed <see cref="TestNode"/> carrying the outcome, timing, output and (optionally) TRX
    /// properties for a single executed test result.
    /// </summary>
    public static TestNode ToResultTestNode(UnitTestElement element, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime, bool isTrxEnabled, MSTestSettings settings)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, result.DisplayName, out BaseTestNodeData baseData);

        // Mirror TestResultExtensions.ToTestResult: the reported error message prefers the exception message and
        // falls back to the ignore reason; the stack trace comes straight from the framework result.
        string? errorMessage = result.ExceptionMessage ?? result.IgnoreReason;
        string? errorStackTrace = result.ExceptionStackTrace;
        var outcome = UnitTestOutcomeHelper.ToTestOutcome(result.Outcome, settings);

        AddOutcome(testNode, outcome, errorMessage, errorStackTrace);

        // Surface the structured assertion values so consumers can render an expected-vs-actual diff. They
        // cannot be read back from the reported exception: AddOutcome reports a synthetic
        // MSTestTestNodeException built from the message and stack trace strings, not the original
        // AssertFailedException.
        if (result.ExceptionExpectedText is not null || result.ExceptionActualText is not null)
        {
            testNode.Properties.Add(new AssertionFailureProperty(result.ExceptionExpectedText, result.ExceptionActualText));
        }

        if (isTrxEnabled)
        {
            AddTrxResultProperties(testNode, baseData, errorMessage, errorStackTrace);
        }

        AddMessagesAndOutput(testNode, result, isTrxEnabled);

        testNode.Properties.Add(new TimingProperty(new(startTime, endTime, result.Duration), []));

        // Surface an in-process retry (MSTest's [Retry]) so the platform can tell the attempts of one test apart
        // instead of seeing repeated results for the same uid. Only added when a retry actually happened, so a
        // regular test node is byte-identical to before.
        if (result.RetryAttemptNumber > 1 || result.IsSupersededRetryAttempt)
        {
            testNode.Properties.Add(new RetryAttemptProperty(result.RetryAttemptNumber, result.IsSupersededRetryAttempt));
        }

        AddAttachments(testNode, result);

        return testNode;
    }

    private static TestNode CreateBaseTestNode(UnitTestElement element, bool isTrxEnabled, string? displayNameOverride, out BaseTestNodeData baseData)
    {
        baseData = BaseTestNodeDataCache.GetValue(element, BaseTestNodeData.Create);

        TestNode testNode = new()
        {
            Uid = baseData.Uid,
            DisplayName = displayNameOverride ?? baseData.DisplayName,
        };

        baseData.AddProperties(testNode.Properties, isTrxEnabled);

        return testNode;
    }

    private sealed class BaseTestNodeData
    {
        private readonly TestMetadataProperty[] _categoryMetadata;
        private readonly TestMetadataProperty[] _traitMetadata;
        private readonly string[]? _trxCategories;
        private readonly string[]? _trxWorkItemIds;
        private readonly TestFileLocationProperty? _fileLocation;
        private readonly ParsedManagedName? _parsedManagedName;
        private readonly string _fullClassName;
        private readonly string _testMethodName;
        private TrxFullyQualifiedTypeNameProperty? _trxFullyQualifiedTypeNameProperty;
        private TrxTestDefinitionName? _trxTestDefinitionName;

        private BaseTestNodeData(
            TestNodeUid uid,
            string displayName,
            TestMetadataProperty[] categoryMetadata,
            TestMetadataProperty[] traitMetadata,
            string[]? trxCategories,
            string[]? trxWorkItemIds,
            TestFileLocationProperty? fileLocation,
            ParsedManagedName? parsedManagedName,
            string fullClassName,
            string testMethodName)
        {
            Uid = uid;
            DisplayName = displayName;
            _categoryMetadata = categoryMetadata;
            _traitMetadata = traitMetadata;
            _trxCategories = trxCategories;
            _trxWorkItemIds = trxWorkItemIds;
            _fileLocation = fileLocation;
            _parsedManagedName = parsedManagedName;
            _fullClassName = fullClassName;
            _testMethodName = testMethodName;
        }

        public TestNodeUid Uid { get; }

        public string DisplayName { get; }

        public static BaseTestNodeData Create(UnitTestElement element)
        {
            TestMethod testMethod = element.TestMethod;

            string[]? categories = element.TestCategory is { Length: > 0 } testCategories
                ? [.. testCategories]
                : null;
            TestMetadataProperty[] categoryMetadata;
            if (categories is null)
            {
                categoryMetadata = [];
            }
            else
            {
                categoryMetadata = new TestMetadataProperty[categories.Length];
                for (int i = 0; i < categoryMetadata.Length; i++)
                {
                    categoryMetadata[i] = new TestMetadataProperty(categories[i], string.Empty);
                }
            }

            TestTrait[]? traits = element.Traits;
            TestMetadataProperty[] traitMetadata;
            if (traits is null)
            {
                traitMetadata = [];
            }
            else
            {
                traitMetadata = new TestMetadataProperty[traits.Length];
                for (int i = 0; i < traitMetadata.Length; i++)
                {
                    traitMetadata[i] = new TestMetadataProperty(traits[i].Name, traits[i].Value);
                }
            }

            TestFileLocationProperty? fileLocation = null;
            if (element.DeclaringFilePath is not null)
            {
                var position = new LinePosition(element.DeclaringLineNumber ?? -1, -1);
                fileLocation = new TestFileLocationProperty(element.DeclaringFilePath, new(position, position));
            }

            ParsedManagedName? parsedManagedName = GetParsedManagedName(testMethod);
            return new BaseTestNodeData(
                new TestNodeUid(element.GetTestId().ToString()),
                testMethod.DisplayName,
                categoryMetadata,
                traitMetadata,
                categories,
                element.WorkItemIds is { Length: > 0 } workItemIds ? [.. workItemIds] : null,
                fileLocation,
                parsedManagedName,
                testMethod.FullClassName,
                testMethod.Name);
        }

        public void AddProperties(PropertyBag properties, bool isTrxEnabled)
        {
            if (isTrxEnabled && _trxCategories is not null)
            {
                properties.Add(new TrxCategoriesProperty([.. _trxCategories]));
            }

            if (isTrxEnabled && _trxWorkItemIds is not null)
            {
                properties.Add(new TrxWorkItemsProperty([.. _trxWorkItemIds]));
            }

            for (int i = 0; i < _categoryMetadata.Length; i++)
            {
                properties.Add(_categoryMetadata[i]);
            }

            for (int i = 0; i < _traitMetadata.Length; i++)
            {
                properties.Add(_traitMetadata[i]);
            }

            if (_fileLocation is not null)
            {
                properties.Add(_fileLocation);
            }

            if (_parsedManagedName is not null)
            {
                properties.Add(_parsedManagedName.ToProperty());
            }
        }

        public TrxTestDefinitionName GetTrxTestDefinitionName()
        {
            if (_trxTestDefinitionName is { } cached)
            {
                return cached;
            }

            var created = new TrxTestDefinitionName(DisplayName);
            return Interlocked.CompareExchange(ref _trxTestDefinitionName, created, null) ?? created;
        }

        public TrxFullyQualifiedTypeNameProperty GetTrxFullyQualifiedTypeNameProperty()
        {
            if (_trxFullyQualifiedTypeNameProperty is { } cached)
            {
                return cached;
            }

            string fullyQualifiedTypeName = _parsedManagedName is not null
                ? _parsedManagedName.FullyQualifiedTypeName
                : !StringEx.IsNullOrEmpty(_fullClassName)
                    ? _fullClassName
                    : throw new InvalidOperationException($"The test method '{_testMethodName}' does not have a fully qualified class name.");

            var created = new TrxFullyQualifiedTypeNameProperty(fullyQualifiedTypeName);
            return Interlocked.CompareExchange(ref _trxFullyQualifiedTypeNameProperty, created, null) ?? created;
        }
    }

    // ManagedMethodName carries the parameter types, so prefer it to match the VSTest bridge's test identity.
    private static ParsedManagedName? GetParsedManagedName(TestMethod testMethod)
        => !testMethod.HasManagedMethodAndTypeProperties || StringEx.IsNullOrEmpty(testMethod.ManagedTypeName)
            ? null
            : ParsedManagedName.Parse(testMethod);

    /// <summary>
    /// The parsed pieces of a <see cref="TestMethod"/>'s managed type and method names, retained by
    /// <see cref="BaseTestNodeData"/>.
    /// </summary>
    private sealed class ParsedManagedName
    {
        private readonly string _namespace;
        private readonly string _typeName;
        private readonly string _methodName;
        private readonly int _arity;
        private readonly string[] _parameterTypeFullNames;

        // Non-null exactly for parameterless methods, where the property is fully immutable and can therefore be
        // handed to every node. Built eagerly so the field can stay readonly: ToProperty() is called immediately
        // after every parse, so nothing is built that would not have been built anyway, and a lazy `??=` would
        // make the shared instance depend on call ordering. See ToProperty().
        private readonly TestMethodIdentifierProperty? _parameterlessProperty;

        private ParsedManagedName(string @namespace, string typeName, string methodName, int arity, string[] parameterTypeFullNames)
        {
            _namespace = @namespace;
            _typeName = typeName;
            _methodName = methodName;
            _arity = arity;
            _parameterTypeFullNames = parameterTypeFullNames;

            // AssemblyFullName and ReturnTypeFullName are not carried by the neutral model today; kept empty to
            // match the current (bridge) behavior. Populating them is a follow-up enabled by this native path.
            if (parameterTypeFullNames.Length == 0)
            {
                _parameterlessProperty = new TestMethodIdentifierProperty(
                    assemblyFullName: string.Empty, @namespace, typeName, methodName, arity, parameterTypeFullNames, returnTypeFullName: string.Empty);
            }
        }

        public static ParsedManagedName Parse(TestMethod testMethod)
        {
            // AddTestMethodIdentifier is the only caller and has already validated both managed names.
            string managedType = testMethod.ManagedTypeName!;
            string managedMethod = testMethod.ManagedMethodName!;

            ManagedNameParser.ParseManagedMethodName(managedMethod, out string methodName, out int arity, out string[]? parameterTypes);

            int lastIndexOfDot = managedType.LastIndexOf('.');
            string @namespace = lastIndexOfDot == -1 ? string.Empty : managedType[..lastIndexOfDot];
            string typeName = lastIndexOfDot == -1 ? managedType : managedType[(lastIndexOfDot + 1)..];

            return new ParsedManagedName(@namespace, typeName, methodName, arity, parameterTypes ?? []);
        }

        public TestMethodIdentifierProperty ToProperty()
        {
            // A parameterless property is fully immutable - every field is a string or int, and the empty array
            // cannot be mutated - so the same instance is handed to every node. This is the common case, and the
            // ParsedManagedName is retained by the element's base descriptor, so the several nodes one executed
            // test produces (the in-progress node, then one result node per data row and retry attempt) share it.
            if (_parameterlessProperty is not null)
            {
                return _parameterlessProperty;
            }

            // A parameterized method must keep getting its own parameter array. TestMethodIdentifierProperty
            // exposes it publicly, so aliasing one array across nodes would let a consumer that writes to it
            // corrupt every other node built from the same test method.
            return new TestMethodIdentifierProperty(
                assemblyFullName: string.Empty, _namespace, _typeName, _methodName, _arity, [.. _parameterTypeFullNames], returnTypeFullName: string.Empty);
        }

        public string FullyQualifiedTypeName
            => StringEx.IsNullOrEmpty(_namespace) ? _typeName : $"{_namespace}.{_typeName}";
    }

    private static void AddOutcome(TestNode testNode, TestOutcome outcome, string? errorMessage, string? errorStackTrace)
    {
        switch (outcome)
        {
            case TestOutcome.Passed:
                testNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
                break;

            case TestOutcome.NotFound:
                testNode.Properties.Add(new ErrorTestNodeStateProperty(new MSTestTestNodeException(errorMessage ?? "Not found", errorStackTrace)));
                break;

            case TestOutcome.Failed:
                testNode.Properties.Add(new FailedTestNodeStateProperty(new MSTestTestNodeException(errorMessage, errorStackTrace)));
                break;

            case TestOutcome.None:
            case TestOutcome.Skipped:
                testNode.Properties.Add(errorMessage is null
                    ? SkippedTestNodeStateProperty.CachedInstance
                    : new SkippedTestNodeStateProperty(errorMessage));
                break;

            default:
                throw new NotSupportedException($"Unsupported test outcome value '{outcome}'");
        }
    }

    private static void AddTrxResultProperties(TestNode testNode, BaseTestNodeData baseData, string? errorMessage, string? errorStackTrace)
    {
        if (!StringEx.IsNullOrEmpty(errorMessage) || !StringEx.IsNullOrEmpty(errorStackTrace))
        {
            testNode.Properties.Add(new TrxExceptionProperty(errorMessage, errorStackTrace));
        }

        testNode.Properties.Add(baseData.GetTrxTestDefinitionName());
        testNode.Properties.Add(baseData.GetTrxFullyQualifiedTypeNameProperty());
    }

    private static void AddMessagesAndOutput(TestNode testNode, FrameworkTestResult result, bool isTrxEnabled)
    {
        // Reproduce, in order, the standard-out / standard-error messages that TestResultExtensions.ToTestResult
        // pushes onto the VSTest result and that ObjectModelConverters.ToTestNode then re-groups: LogOutput and
        // (banner-prefixed) DebugTrace / TestContextMessages are standard-out; LogError is standard-error.
        List<string>? standardOutputMessages = null;
        List<string>? standardErrorMessages = null;
        List<TrxMessage>? trxMessages = isTrxEnabled ? [] : null;

        if (!StringEx.IsNullOrEmpty(result.LogOutput))
        {
            (standardOutputMessages ??= []).Add(result.LogOutput!);
            trxMessages?.Add(new StandardOutputTrxMessage(result.LogOutput));
        }

        if (!StringEx.IsNullOrEmpty(result.LogError))
        {
            (standardErrorMessages ??= []).Add(result.LogError!);
            trxMessages?.Add(new StandardErrorTrxMessage(result.LogError));
        }

        if (!StringEx.IsNullOrEmpty(result.DebugTrace))
        {
            string debugTraceMessagesInStdOut =
                $"""


                {Resource.DebugTraceBanner}
                {result.DebugTrace}
                """;
            (standardOutputMessages ??= []).Add(debugTraceMessagesInStdOut);
            trxMessages?.Add(new StandardOutputTrxMessage(debugTraceMessagesInStdOut));
        }

        if (!StringEx.IsNullOrEmpty(result.TestContextMessages))
        {
            string testContextMessagesInStdOut =
                $"""


                {Resource.TestContextMessageBanner}
                {result.TestContextMessages}
                """;
            (standardOutputMessages ??= []).Add(testContextMessagesInStdOut);
            trxMessages?.Add(new StandardOutputTrxMessage(testContextMessagesInStdOut));
        }

        if (isTrxEnabled)
        {
            testNode.Properties.Add(new TrxMessagesProperty(trxMessages is { Count: > 0 } ? [.. trxMessages] : []));
        }

        if (standardErrorMessages is { Count: > 0 })
        {
            testNode.Properties.Add(new StandardErrorProperty(string.Join(Environment.NewLine, standardErrorMessages)));
        }

        if (standardOutputMessages is { Count: > 0 })
        {
            testNode.Properties.Add(new StandardOutputProperty(string.Join(Environment.NewLine, standardOutputMessages)));
        }
    }

    private static void AddAttachments(TestNode testNode, FrameworkTestResult result)
    {
        if (result.ResultFiles is not { Count: > 0 })
        {
            return;
        }

        foreach (string resultFile in result.ResultFiles)
        {
            string pathToResultFile = PlatformServiceProvider.Instance.FileOperations.GetFullFilePath(resultFile);
            testNode.Properties.Add(new FileArtifactProperty(new FileInfo(pathToResultFile), Resource.AttachmentSetDisplayName, resultFile));
        }
    }
}
#endif
