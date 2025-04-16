// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

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

    private static readonly TestProperty ManagedTypeProperty = TestProperty.Register(
        id: "TestCase.ManagedType",
        label: "TestCase.ManagedType",
        valueType: typeof(string),
        owner: typeof(TestCase));

    private static readonly TestProperty ManagedMethodProperty = TestProperty.Register(
        id: "TestCase.ManagedMethod",
        label: "TestCase.ManagedMethod",
        valueType: typeof(string),
        owner: typeof(TestCase));

    /// <summary>
    /// Converts a VSTest <see cref="TestCase"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(this TestCase testCase, bool isTrxEnabled, IServiceProvider serviceProvider, string? displayNameFromTestResult = null)
    {
        string testNodeUid = testCase.Id.ToString();

        TestNode testNode = new()
        {
            Uid = new TestNodeUid(testNodeUid),
            DisplayName = displayNameFromTestResult ?? testCase.DisplayName ?? testCase.FullyQualifiedName,
        };

        // This will be false for Expecto and NUnit currently, as they don't provide ManagedType/ManagedMethod.
        if (TryGetMethodIdentifierProperty(testCase, out TestMethodIdentifierProperty? methodIdentifierProperty))
        {
            testNode.Properties.Add(methodIdentifierProperty);
        }

        CopyVSTestProperties(testCase.Properties, testNode, testCase, testCase.GetPropertyValue, isTrxEnabled, serviceProvider);
        if (testCase.CodeFilePath is not null)
        {
            testNode.Properties.Add(new TestFileLocationProperty(testCase.CodeFilePath, new(new(testCase.LineNumber, -1), new(testCase.LineNumber, -1))));
        }

        return testNode;
    }

    private static void CopyVSTestProperties(IEnumerable<TestProperty> testProperties, TestNode testNode, TestCase testCase, Func<TestProperty, object?> getPropertyValue,
        bool isTrxEnabled, IServiceProvider serviceProvider)
    {
        foreach (TestProperty property in testProperties)
        {
            testNode.Properties.Add(new VSTestProperty(property, testCase));

            if (isTrxEnabled)
            {
                // TPv2 is doing some special handling for MSTest... we should probably do the same.
                // See https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Extensions.TrxLogger/Utility/Converter.cs#L66-L70
                if (property.Id == "MSTestDiscoverer.TestCategory"
                    && getPropertyValue(property) is string[] mstestCategories)
                {
                    testNode.Properties.Add(new TrxCategoriesProperty(mstestCategories));
                }
            }

            // If vstestProvider is enabled (only known to be true for NUnit and Expecto so far), and we are running server mode in IDE (not dotnet test),
            // we add these stuff.
            // Once NUnit and Expecto allow us to move forward and remove vstestProvider, we can remove this logic and get rid of the whole vstestProvider capability.
            if (serviceProvider.GetService<INamedFeatureCapability>()?.IsSupported(JsonRpcStrings.VSTestProviderSupport) == true &&
                serviceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) &&
                !serviceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey))
            {
                if (property.Id == TestCaseProperties.Id.Id
                        && getPropertyValue(property) is Guid testCaseId)
                {
                    testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.Id", testCaseId.ToString()));
                }
                else if (property.Id == TestCaseProperties.FullyQualifiedName.Id
                    && getPropertyValue(property) is string testCaseFqn)
                {
                    testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", testCaseFqn));
                }
                else if (property.Id == OriginalExecutorUriProperty.Id
                    && getPropertyValue(property) is Uri originalExecutorUri)
                {
                    testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.original-executor-uri", originalExecutorUri.AbsoluteUri));
                }

                // The TP object holding the hierarchy property is defined on adapter utilities and we don't want to enforce that dependency
                // so instead I use the string ID copied from TP.
                else if (property.Id == "TestCase.Hierarchy"
                    && getPropertyValue(property) is string[] testCaseHierarchy
                    && testCaseHierarchy.Length == 4)
                {
                    testNode.Properties.Add(new SerializableNamedArrayStringProperty("vstest.TestCase.Hierarchy", testCaseHierarchy));
                }
            }

            // ID is defined on TraitCollection but is internal so again we copy the string here.
            if (property.Id == "TestObject.Traits"
                && getPropertyValue(property) is KeyValuePair<string, string>[] traits && traits.Length > 0)
            {
                foreach (KeyValuePair<string, string> trait in traits)
                {
                    testNode.Properties.Add(new TestMetadataProperty(trait.Key, trait.Value));
                }
            }

            // TPv2 is doing some special handling for MSTest... we should probably do the same.
            // See https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Extensions.TrxLogger/Utility/Converter.cs#L66-L70
            else if (property.Id == "MSTestDiscoverer.TestCategory"
                && getPropertyValue(property) is string[] mstestCategories && mstestCategories.Length > 0)
            {
                foreach (string category in mstestCategories)
                {
                    testNode.Properties.Add(new TestMetadataProperty(category, string.Empty));
                }
            }
        }
    }

    /// <summary>
    /// Converts a VSTest <see cref="TestResult"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(this TestResult testResult, bool isTrxEnabled, IServiceProvider serviceProvider)
    {
        var testNode = testResult.TestCase.ToTestNode(isTrxEnabled, serviceProvider, testResult.DisplayName);
        CopyVSTestProperties(testResult.Properties, testNode, testResult.TestCase, testResult.GetPropertyValue, isTrxEnabled, serviceProvider);
        testNode.AddOutcome(testResult);

        if (isTrxEnabled)
        {
            testNode.Properties.Add(new TrxExceptionProperty(testResult.ErrorMessage, testResult.ErrorStackTrace));

            if (TryParseFullyQualifiedType(testResult.TestCase.FullyQualifiedName, out string? fullyQualifiedType))
            {
                testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(fullyQualifiedType));
            }
            else
            {
                throw new InvalidOperationException("Unable to parse fully qualified type name from test case: " + testResult.TestCase.FullyQualifiedName);
            }

            testNode.Properties.Add(new TrxMessagesProperty(testResult.Messages
                .Select(msg =>
                    msg.Category switch
                    {
                        string x when x == TestResultMessage.StandardErrorCategory => new StandardErrorTrxMessage(msg.Text),
                        string x when x == TestResultMessage.StandardOutCategory => new StandardOutputTrxMessage(msg.Text),
                        string x when x == TestResultMessage.DebugTraceCategory => new DebugOrTraceTrxMessage(msg.Text),
                        _ => new TrxMessage(msg.Text),
                    })
                .ToArray()));
        }

        testNode.Properties.Add(new TimingProperty(new(testResult.StartTime, testResult.EndTime, testResult.Duration), []));

        var standardErrorMessages = new List<string>();
        var standardOutputMessages = new List<string>();
        foreach (TestResultMessage testResultMessage in testResult.Messages)
        {
            if (testResultMessage.Category == TestResultMessage.StandardErrorCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                standardErrorMessages.Add(message);
            }

            if (testResultMessage.Category == TestResultMessage.StandardOutCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                standardOutputMessages.Add(message);
            }
        }

        foreach (AttachmentSet attachmentSet in testResult.Attachments)
        {
            foreach (UriDataAttachment attachment in attachmentSet.Attachments)
            {
                testNode.Properties.Add(new FileArtifactProperty(new FileInfo(attachment.Uri.LocalPath), attachmentSet.DisplayName, attachment.Description));
            }
        }

        if (standardErrorMessages.Count > 0)
        {
            testNode.Properties.Add(new StandardErrorProperty(string.Join(Environment.NewLine, standardErrorMessages)));
        }

        if (standardOutputMessages.Count > 0)
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

    internal static void FixUpTestCase(this TestCase testCase, string? testAssemblyPath = null)
    {
        // To help framework authors using code generator, we replace the Source property of the test case with the
        // test assembly path.
        if (RoslynString.IsNullOrEmpty(testCase.Source) && !RoslynString.IsNullOrEmpty(testAssemblyPath))
        {
            testCase.Source = testAssemblyPath;
        }

        // Because this project is the actually registered test adapter, we need to replace test framework executor
        // URI by ours.
        if (!testCase.Properties.Any(x => x.Id == OriginalExecutorUriProperty.Id))
        {
            testCase.SetPropertyValue(OriginalExecutorUriProperty, testCase.ExecutorUri);
        }

        testCase.ExecutorUri = new(Constants.ExecutorUri);
    }

    private static bool TryGetMethodIdentifierProperty(TestCase testCase, [NotNullWhen(true)] out TestMethodIdentifierProperty? methodIdentifierProperty)
    {
        string? managedType = testCase.GetPropertyValue<string>(ManagedTypeProperty, defaultValue: null);
        string? managedMethod = testCase.GetPropertyValue<string>(ManagedMethodProperty, defaultValue: null);
        // NOTE: ManagedMethod, in case of MSTest, will have the parameter types.
        // So, we prefer using it to display the parameter types in Test Explorer.
        if (RoslynString.IsNullOrEmpty(managedType) || RoslynString.IsNullOrEmpty(managedMethod))
        {
            methodIdentifierProperty = null;
            return false;
        }

        return TryGetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(managedType, managedMethod, out methodIdentifierProperty);
    }

    private static bool TryGetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(
        ReadOnlySpan<char> managedType,
        ReadOnlySpan<char> managedMethod,
        [NotNullWhen(true)] out TestMethodIdentifierProperty? methodIdentifierProperty)
    {
        string assemblyFullName = string.Empty;
        string @namespace;
        string typeName;
        string methodName;
        string[] parameterTypeFullNames = Array.Empty<string>();
        string returnTypeFullName = string.Empty;

        int indexOfParen = managedMethod.IndexOf('(');
        if (indexOfParen != -1)
        {
            parameterTypeFullNames = GetParameterTypes(managedMethod.Slice(indexOfParen + 1));
            methodName = managedMethod.Slice(0, indexOfParen).ToString();
        }
        else
        {
            methodName = managedMethod.ToString();
        }

        // Get type name
        int lastIndexOfDot = managedType.LastIndexOf('.');
        if (lastIndexOfDot == -1)
        {
            typeName = managedType.ToString();
            @namespace = string.Empty;
        }
        else
        {
            typeName = managedType.Slice(lastIndexOfDot + 1).ToString();
            @namespace = managedType.Slice(0, lastIndexOfDot).ToString();
        }

        methodIdentifierProperty = new TestMethodIdentifierProperty(assemblyFullName, @namespace, typeName, methodName, parameterTypeFullNames, returnTypeFullName);
        return true;
    }

    private static string[] GetParameterTypes(ReadOnlySpan<char> afterOpenParen)
    {
        if (afterOpenParen[afterOpenParen.Length - 1] != ')')
        {
            // TODO: Maybe better to throw?
            return Array.Empty<string>();
        }

        afterOpenParen = afterOpenParen.Slice(0, afterOpenParen.Length - 1).Trim();
        return afterOpenParen.Length == 0
            ? Array.Empty<string>()
            : afterOpenParen.ToString().Split(',', StringSplitOptions.None);
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
}
