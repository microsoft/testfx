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
    /// Builds a discovered-state <see cref="TestNode"/> for a discovered test.
    /// </summary>
    public static TestNode ToDiscoveredTestNode(UnitTestElement element, bool isTrxEnabled)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, displayNameOverride: null);
        testNode.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
        return testNode;
    }

    /// <summary>
    /// Builds an in-progress-state <see cref="TestNode"/> reported when a test starts executing.
    /// </summary>
    public static TestNode ToInProgressTestNode(UnitTestElement element, bool isTrxEnabled)
    {
        // Skip AddTestMethodIdentifier: no consumer reads TestMethodIdentifierProperty from
        // in-progress nodes (IDEs already have the full identity from the discovery node).
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, displayNameOverride: null, addMethodIdentifier: false);
        testNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
        return testNode;
    }

    /// <summary>
    /// Builds a completed <see cref="TestNode"/> carrying the outcome, timing, output and (optionally) TRX
    /// properties for a single executed test result.
    /// </summary>
    public static TestNode ToResultTestNode(UnitTestElement element, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime, bool isTrxEnabled, MSTestSettings settings)
    {
        TestNode testNode = CreateBaseTestNode(element, isTrxEnabled, result.DisplayName);

        // Mirror TestResultExtensions.ToTestResult: the reported error message prefers the exception message and
        // falls back to the ignore reason; the stack trace comes straight from the framework result.
        string? errorMessage = result.ExceptionMessage ?? result.IgnoreReason;
        string? errorStackTrace = result.ExceptionStackTrace;
        var outcome = UnitTestOutcomeHelper.ToTestOutcome(result.Outcome, settings);

        AddOutcome(testNode, outcome, errorMessage, errorStackTrace);

        if (isTrxEnabled)
        {
            AddTrxResultProperties(testNode, element, errorMessage, errorStackTrace);
        }

        AddMessagesAndOutput(testNode, result, isTrxEnabled);

        testNode.Properties.Add(new TimingProperty(new(startTime, endTime, result.Duration), []));

        AddAttachments(testNode, result);

        return testNode;
    }

    private static TestNode CreateBaseTestNode(UnitTestElement element, bool isTrxEnabled, string? displayNameOverride, bool addMethodIdentifier = true)
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

        if (addMethodIdentifier)
        {
            AddTestMethodIdentifier(testNode, testMethod);
        }

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

    private static void AddTestMethodIdentifier(TestNode testNode, TestMethod testMethod)
    {
        // NOTE: ManagedMethodName, in case of MSTest, carries the parameter types, so we prefer it to display the
        // parameter types in Test Explorer. This mirrors what the VSTest bridge did in AddAdditionalProperties.
        if (!testMethod.HasManagedMethodAndTypeProperties)
        {
            return;
        }

        string? managedType = testMethod.ManagedTypeName;
        string? managedMethod = testMethod.ManagedMethodName;
        if (StringEx.IsNullOrEmpty(managedType) || StringEx.IsNullOrEmpty(managedMethod))
        {
            return;
        }

        ManagedNameParser.ParseManagedMethodName(managedMethod, out string methodName, out int arity, out string[]? parameterTypes);
        parameterTypes ??= [];

        int lastIndexOfDot = managedType.LastIndexOf('.');
        string @namespace = lastIndexOfDot == -1 ? string.Empty : managedType[..lastIndexOfDot];
        string typeName = lastIndexOfDot == -1 ? managedType : managedType[(lastIndexOfDot + 1)..];

        // AssemblyFullName and ReturnTypeFullName are not carried by the neutral model today; kept empty to match
        // the current (bridge) behavior. Populating them is a follow-up enabled by this native path.
        testNode.Properties.Add(new TestMethodIdentifierProperty(assemblyFullName: string.Empty, @namespace, typeName, methodName, arity, parameterTypes, returnTypeFullName: string.Empty));
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

    private static void AddTrxResultProperties(TestNode testNode, UnitTestElement element, string? errorMessage, string? errorStackTrace)
    {
        if (!StringEx.IsNullOrEmpty(errorMessage) || !StringEx.IsNullOrEmpty(errorStackTrace))
        {
            testNode.Properties.Add(new TrxExceptionProperty(errorMessage, errorStackTrace));
        }

        TestMethod testMethod = element.TestMethod;

        // TestMethod.DisplayName is always initialized (constructor sets it to displayName ?? name).
        testNode.Properties.Add(new TrxTestDefinitionName(testMethod.DisplayName));

        TestMethodIdentifierProperty? testMethodIdentifierProperty = testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
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
