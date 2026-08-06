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
    /// Caches the parsed pieces of a <see cref="TestMethod"/>'s managed name.
    /// </summary>
    /// <remarks>
    /// The parse is a pure function of <see cref="TestMethod.ManagedTypeName"/> and
    /// <see cref="TestMethod.ManagedMethodName"/>, both immutable for the lifetime of the instance, and it scans the
    /// managed method signature and allocates the namespace/type substrings. The same <see cref="TestMethod"/> is
    /// converted several times per executed test (the in-progress node, then one result node per data row), so the
    /// parse is paid once per test method rather than once per node.
    /// <para>
    /// The resulting <see cref="TestMethodIdentifierProperty"/> is cached alongside the parse only for
    /// parameterless test methods. That type publicly exposes its
    /// <see cref="TestMethodIdentifierProperty.ParameterTypeFullNames"/> array, so sharing one instance is safe
    /// exactly when that array is empty and therefore cannot be mutated; every other field is an immutable string
    /// or int. A parameterized method must keep getting a fresh property carrying a fresh array copy, otherwise a
    /// consumer that writes to the array would corrupt every other node built from the same test method (see
    /// <see cref="ParsedManagedName.ToProperty"/>).
    /// </para>
    /// <para>
    /// The cache lives here rather than as a lazy property on <see cref="TestMethod"/> (the way
    /// <see cref="TestMethod.ManagedTypeName"/> caches itself) because <see cref="TestMethodIdentifierProperty"/>
    /// is a Microsoft.Testing.Platform type, and MSTestAdapter.PlatformServices - where <see cref="TestMethod"/>
    /// lives - does not reference the platform at all. Even within this assembly the platform is only reachable
    /// from this folder, through the file-level RS0030 suppression above (see this project's BannedSymbols.txt).
    /// </para>
    /// <para>
    /// The table holds only weak references to its keys, so entries disappear as soon as the
    /// <see cref="TestMethod"/> becomes unreachable. <c>GetValue</c> is thread-safe: concurrent misses may each run
    /// the factory, but a single value is published to every caller.
    /// </para>
    /// </remarks>
#pragma warning disable IDE0028 // ConditionalWeakTable is not collection-expression-constructible on .NET Framework (CS9174).
    private static readonly ConditionalWeakTable<TestMethod, ParsedManagedName> ParsedManagedNameCache = new();
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
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, result.DisplayName, out TestMethodIdentifierProperty? testMethodIdentifier);

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
            AddTrxResultProperties(testNode, element, errorMessage, errorStackTrace, testMethodIdentifier);
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

    private static TestNode CreateBaseTestNode(UnitTestElement element, bool isTrxEnabled, string? displayNameOverride, out TestMethodIdentifierProperty? testMethodIdentifier)
    {
        TestMethod testMethod = element.TestMethod;

        TestNode testNode = new()
        {
            Uid = new TestNodeUid(element.GetTestId().ToString()),

            // TestMethod.DisplayName is always initialized (the constructor sets it to displayName ?? name), so
            // displayNameOverride is the only real fallback needed here.
            DisplayName = displayNameOverride ?? testMethod.DisplayName,
        };

        AddCategoriesAndTraits(testNode, element, isTrxEnabled);

        if (element.DeclaringFilePath is not null)
        {
            var position = new LinePosition(element.DeclaringLineNumber ?? -1, -1);
            testNode.Properties.Add(new TestFileLocationProperty(element.DeclaringFilePath, new(position, position)));
        }

        testMethodIdentifier = AddTestMethodIdentifier(testNode, testMethod);

        return testNode;
    }

    private static void AddCategoriesAndTraits(TestNode testNode, UnitTestElement element, bool isTrxEnabled)
    {
        if (element.TestCategory is { Length: > 0 } categories)
        {
            if (isTrxEnabled)
            {
                testNode.Properties.Add(new TrxCategoriesProperty(categories));
            }

            foreach (string category in categories)
            {
                testNode.Properties.Add(new TestMetadataProperty(category, string.Empty));
            }
        }

        if (element.Traits is { Length: > 0 } traits)
        {
            foreach (TestTrait trait in traits)
            {
                testNode.Properties.Add(new TestMetadataProperty(trait.Name, trait.Value));
            }
        }
    }

    private static TestMethodIdentifierProperty? AddTestMethodIdentifier(TestNode testNode, TestMethod testMethod)
    {
        // NOTE: ManagedMethodName, in case of MSTest, carries the parameter types, so we prefer it to display the
        // parameter types in Test Explorer. This mirrors what the VSTest bridge did in AddAdditionalProperties.
        if (!testMethod.HasManagedMethodAndTypeProperties || StringEx.IsNullOrEmpty(testMethod.ManagedTypeName))
        {
            return null;
        }

        // The method group conversion is cached by the compiler, so the lookup does not allocate a delegate.
        TestMethodIdentifierProperty testMethodIdentifier = ParsedManagedNameCache.GetValue(testMethod, ParsedManagedName.Parse).ToProperty();
        testNode.Properties.Add(testMethodIdentifier);
        return testMethodIdentifier;
    }

    /// <summary>
    /// The parsed pieces of a <see cref="TestMethod"/>'s managed type and method names, cached by
    /// <see cref="ParsedManagedNameCache"/>.
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
            // ParsedManagedName is cached per TestMethod, so the several nodes one executed test produces (the
            // in-progress node, then one result node per data row and per in-process retry attempt) share it.
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

    private static void AddTrxResultProperties(TestNode testNode, UnitTestElement element, string? errorMessage, string? errorStackTrace, TestMethodIdentifierProperty? testMethodIdentifierProperty)
    {
        if (!StringEx.IsNullOrEmpty(errorMessage) || !StringEx.IsNullOrEmpty(errorStackTrace))
        {
            testNode.Properties.Add(new TrxExceptionProperty(errorMessage, errorStackTrace));
        }

        TestMethod testMethod = element.TestMethod;

        // TestMethod.DisplayName is always initialized (constructor sets it to displayName ?? name).
        testNode.Properties.Add(new TrxTestDefinitionName(testMethod.DisplayName));

        if (testMethodIdentifierProperty is not null)
        {
            testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(
                StringEx.IsNullOrEmpty(testMethodIdentifierProperty.Namespace)
                    ? testMethodIdentifierProperty.TypeName
                    : $"{testMethodIdentifierProperty.Namespace}.{testMethodIdentifierProperty.TypeName}"));
        }
        else if (!StringEx.IsNullOrEmpty(testMethod.FullClassName))
        {
            // FullClassName is the source of truth for the type name, so use it directly instead of re-parsing it out
            // of a "FullClassName.Name" string.
            testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(testMethod.FullClassName));
        }
        else
        {
            throw new InvalidOperationException($"The test method '{testMethod.Name}' does not have a fully qualified class name.");
        }
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
