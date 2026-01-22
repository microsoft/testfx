// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A set of extension methods to convert between the Microsoft Testing Platform and VSTest object models.
/// </summary>
internal static class ObjectModelConverters
{
    private static readonly TestProperty OriginalExecutorUriProperty = TestProperty.Register(
        VSTestTestNodeProperties.OriginalExecutorUriPropertyName, VSTestTestNodeProperties.OriginalExecutorUriPropertyName,
        typeof(Uri), typeof(TestNode));

    private static readonly Uri ExecutorUri = new(Constants.ExecutorUri);

    /// <summary>
    /// Converts a VSTest <see cref="TestCase"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(
        this TestCase testCase,
        bool isTrxEnabled,
        bool useFullyQualifiedNameAsUid,
        Action<TestCase, TestNode> addAdditionalProperties,
        INamedFeatureCapability? namedFeatureCapability,
        ICommandLineOptions commandLineOptions,
        IClientInfo clientInfo,
        string? displayNameFromTestResult = null)
    {
        string testNodeUid = useFullyQualifiedNameAsUid ? testCase.FullyQualifiedName : testCase.Id.ToString();

        TestNode testNode = new()
        {
            Uid = new TestNodeUid(testNodeUid),
            DisplayName = displayNameFromTestResult ?? testCase.DisplayName ?? testCase.FullyQualifiedName,
        };

        CopyCategoryAndTraits(testCase, testNode, isTrxEnabled);

        if (ShouldAddVSTestProviderProperties(namedFeatureCapability, commandLineOptions))
        {
            CopyVSTestProviderProperties(testNode, testCase, new(clientInfo));
        }

        if (testCase.CodeFilePath is not null)
        {
            var position = new LinePosition(testCase.LineNumber, -1);
            testNode.Properties.Add(
                new TestFileLocationProperty(testCase.CodeFilePath, lineSpan: new(position, position)));
        }

        addAdditionalProperties(testCase, testNode);
        return testNode;
    }

    private static void CopyCategoryAndTraits(TestObject testCaseOrResult, TestNode testNode, bool isTrxEnabled)
    {
        foreach (KeyValuePair<TestProperty, object?> property in testCaseOrResult.GetProperties())
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if ((property.Key.Attributes & TestPropertyAttributes.Trait) == 0)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                continue;
            }

            if (property.Value is string[] categories)
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
            else if (property.Value is KeyValuePair<string, string>[] traits)
            {
                foreach (KeyValuePair<string, string> trait in traits)
                {
                    testNode.Properties.Add(new TestMetadataProperty(trait.Key, trait.Value));
                }
            }
        }
    }

    private static void CopyVSTestProviderProperties(TestNode testNode, TestCase testCase, ClientCompatibilityService compatibilityService)
    {
        if (testCase.Id is Guid testCaseId)
        {
            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.Id", testCaseId.ToString()));
        }

        if (testCase.FullyQualifiedName is string testCaseFqn)
        {
            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", testCaseFqn));
        }

        if (testCase.GetPropertyValue(OriginalExecutorUriProperty) is Uri originalExecutorUri)
        {
            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.original-executor-uri", originalExecutorUri.AbsoluteUri));
        }

        if (!RoslynString.IsNullOrEmpty(testCase.CodeFilePath) &&
            compatibilityService.UseVSTestTestCaseLocationProperties)
        {
            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.CodeFilePath", testCase.CodeFilePath));
            testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.LineNumber", testCase.LineNumber.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// Converts a VSTest <see cref="TestResult"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(
        this TestResult testResult,
        bool isTrxEnabled,
        bool useFullyQualifiedNameAsUid,
        Action<TestCase, TestNode> addAdditionalProperties,
        INamedFeatureCapability? namedFeatureCapability,
        ICommandLineOptions commandLineOptions,
        IClientInfo clientInfo)
    {
        var testNode = testResult.TestCase.ToTestNode(isTrxEnabled, useFullyQualifiedNameAsUid, addAdditionalProperties, namedFeatureCapability, commandLineOptions, clientInfo, testResult.DisplayName);

        CopyCategoryAndTraits(testResult, testNode, isTrxEnabled);

        testNode.AddOutcome(testResult);

        if (isTrxEnabled)
        {
            if (!RoslynString.IsNullOrEmpty(testResult.ErrorMessage) || !RoslynString.IsNullOrEmpty(testResult.ErrorStackTrace))
            {
                testNode.Properties.Add(new TrxExceptionProperty(testResult.ErrorMessage, testResult.ErrorStackTrace));
            }

            // TODO: Consider retrieving TestMethodIdentifierProperty first (which could have been added through addAdditionalProperties.
            // VSTest's TestCase.FQN is very non-standard.
            // We should avoid using it if we can.
            if (TryParseFullyQualifiedType(testResult.TestCase.FullyQualifiedName, out string? fullyQualifiedType))
            {
                testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(fullyQualifiedType));
            }
            else
            {
                throw new InvalidOperationException("Unable to parse fully qualified type name from test case: " + testResult.TestCase.FullyQualifiedName);
            }

            testNode.Properties.Add(new TrxMessagesProperty([.. testResult.Messages
                .Select(msg =>
                    msg.Category switch
                    {
                        string x when x == TestResultMessage.StandardErrorCategory => (TrxMessage)new StandardErrorTrxMessage(msg.Text),
                        string x when x == TestResultMessage.StandardOutCategory => new StandardOutputTrxMessage(msg.Text),
                        string x when x == TestResultMessage.DebugTraceCategory => new DebugOrTraceTrxMessage(msg.Text),
                        _ => throw new UnreachableException(),
                    })]));
        }

        testNode.Properties.Add(new TimingProperty(new(testResult.StartTime, testResult.EndTime, testResult.Duration), []));

        List<string>? standardErrorMessages = null;
        List<string>? standardOutputMessages = null;
        bool addVSTestProviderProperties = ShouldAddVSTestProviderProperties(namedFeatureCapability, commandLineOptions);
        foreach (TestResultMessage testResultMessage in testResult.Messages)
        {
            if (testResultMessage.Category == TestResultMessage.StandardErrorCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                (standardErrorMessages ??= []).Add(message);
            }

            if (testResultMessage.Category == TestResultMessage.StandardOutCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                (standardOutputMessages ??= []).Add(message);
            }
        }

        foreach (AttachmentSet attachmentSet in testResult.Attachments)
        {
            foreach (UriDataAttachment attachment in attachmentSet.Attachments)
            {
                testNode.Properties.Add(new FileArtifactProperty(new FileInfo(attachment.Uri.LocalPath), attachmentSet.DisplayName, attachment.Description));
            }
        }

        if (standardErrorMessages is { Count: > 0 })
        {
            testNode.Properties.Add(new StandardErrorProperty(string.Join(Environment.NewLine, standardErrorMessages)));
        }

        if (standardOutputMessages is { Count: > 0 })
        {
            testNode.Properties.Add(new StandardOutputProperty(string.Join(Environment.NewLine, standardOutputMessages)));
        }

        return testNode;
    }

    private static void AddOutcome(this TestNode testNode, TestResult testResult)
    {
        switch (testResult.Outcome)
        {
            case TestOutcome.Passed:
                testNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
                break;

            case TestOutcome.NotFound:
                testNode.Properties.Add(new ErrorTestNodeStateProperty(new VSTestException(testResult.ErrorMessage ?? "Not found", testResult.ErrorStackTrace)));
                break;

            case TestOutcome.Failed:
                testNode.Properties.Add(new FailedTestNodeStateProperty(new VSTestException(testResult.ErrorMessage, testResult.ErrorStackTrace)));
                break;

            // It seems that NUnit inconclusive tests are reported as None which should be considered as Skipped.
            case TestOutcome.None:
            case TestOutcome.Skipped:
                testNode.Properties.Add(testResult.ErrorMessage is null
                    ? SkippedTestNodeStateProperty.CachedInstance
                    : new SkippedTestNodeStateProperty(testResult.ErrorMessage));
                break;

            default:
                throw new NotSupportedException($"Unsupported test outcome value '{testResult.Outcome}'");
        }
    }

    internal static void FixUpTestCase(this TestCase testCase)
    {
        // Because this project is the actually registered test adapter, we need to replace test framework executor
        // URI by ours.
        if (!testCase.Properties.Any(x => x.Id == OriginalExecutorUriProperty.Id))
        {
            testCase.SetPropertyValue(OriginalExecutorUriProperty, testCase.ExecutorUri);
        }

        testCase.ExecutorUri = ExecutorUri;
    }

    private static bool TryParseFullyQualifiedType(string fullyQualifiedName, [NotNullWhen(true)] out string? fullyQualifiedType)
    {
        fullyQualifiedType = null;

        // Some test frameworks display arguments in the fully qualified type name, so we need to exclude them
        // before looking at the last dot.
        int openBracketIndex = fullyQualifiedName.IndexOf('(');
        int lastDotIndexBeforeOpenBracket = openBracketIndex <= 0
            ? fullyQualifiedName.LastIndexOf('.')
            : fullyQualifiedName.LastIndexOf('.', openBracketIndex - 1);
        if (lastDotIndexBeforeOpenBracket <= 0)
        {
            return false;
        }

        fullyQualifiedType = fullyQualifiedName[..lastDotIndexBeforeOpenBracket];
        return true;
    }

    private static bool ShouldAddVSTestProviderProperties(INamedFeatureCapability? namedFeatureCapability, ICommandLineOptions commandLineOptions)
        => namedFeatureCapability?.IsSupported(JsonRpcStrings.VSTestProviderSupport) == true &&
           commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) &&
           !commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey);
}
