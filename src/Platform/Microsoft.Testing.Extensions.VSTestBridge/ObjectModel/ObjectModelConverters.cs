﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;
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

    private static readonly TestProperty TestCategoryProperty = TestProperty.Register(
        id: "MSTestDiscoverer.TestCategory",
        label: "TestCategory",
        valueType: typeof(string[]),
        owner: typeof(TestCase));

    private static readonly TestProperty TraitsProperty = TestProperty.Register(
        id: "TestObject.Traits",
        label: "Traits",
        valueType: typeof(KeyValuePair<string, string>[]),
        owner: typeof(TestObject));

    /// <summary>
    /// Converts a VSTest <see cref="TestCase"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(this TestCase testCase, bool isTrxEnabled, INamedFeatureCapability? namedFeatureCapability, ICommandLineOptions commandLineOptions, string? displayNameFromTestResult = null)
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

        CopyCategoryAndTraits(testCase, testNode, isTrxEnabled);

        if (ShouldAddVSTestProviderProperties(namedFeatureCapability, commandLineOptions))
        {
            CopyVSTestProviderProperties(testNode, testCase);
        }

        if (testCase.CodeFilePath is not null)
        {
            testNode.Properties.Add(new TestFileLocationProperty(testCase.CodeFilePath, new(new(testCase.LineNumber, -1), new(testCase.LineNumber, -1))));
        }

        return testNode;
    }

    private static void CopyCategoryAndTraits(TestObject testCaseOrResult, TestNode testNode, bool isTrxEnabled)
    {
        // TPv2 is doing some special handling for MSTest... we should probably do the same.
        // See https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Extensions.TrxLogger/Utility/Converter.cs#L66-L70
        if (testCaseOrResult.GetPropertyValue<string[]>(TestCategoryProperty, defaultValue: null) is string[] mstestCategories)
        {
            if (isTrxEnabled)
            {
                testNode.Properties.Add(new TrxCategoriesProperty(mstestCategories));
            }

            foreach (string category in mstestCategories)
            {
                testNode.Properties.Add(new TestMetadataProperty(category, string.Empty));
            }
        }

        if (testCaseOrResult.GetPropertyValue<KeyValuePair<string, string>[]>(TraitsProperty, defaultValue: null) is KeyValuePair<string, string>[] traits &&
            traits.Length > 0)
        {
            foreach (KeyValuePair<string, string> trait in traits)
            {
                testNode.Properties.Add(new TestMetadataProperty(trait.Key, trait.Value));
            }
        }
    }

    private static void CopyVSTestProviderProperties(TestNode testNode, TestCase testCase)
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
    }

    /// <summary>
    /// Converts a VSTest <see cref="TestResult"/> to a Microsoft Testing Platform <see cref="TestNode"/>.
    /// </summary>
    public static TestNode ToTestNode(this TestResult testResult, bool isTrxEnabled, INamedFeatureCapability? namedFeatureCapability, ICommandLineOptions commandLineOptions)
    {
        var testNode = testResult.TestCase.ToTestNode(isTrxEnabled, namedFeatureCapability, commandLineOptions, testResult.DisplayName);

        CopyCategoryAndTraits(testResult, testNode, isTrxEnabled);

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
        bool addVSTestProviderProperties = ShouldAddVSTestProviderProperties(namedFeatureCapability, commandLineOptions);
        foreach (TestResultMessage testResultMessage in testResult.Messages)
        {
            if (testResultMessage.Category == TestResultMessage.StandardErrorCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                if (addVSTestProviderProperties)
                {
                    testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.StandardError", message));
                }

                standardErrorMessages.Add(message);
            }

            if (testResultMessage.Category == TestResultMessage.StandardOutCategory)
            {
                string message = testResultMessage.Text ?? string.Empty;
                if (addVSTestProviderProperties)
                {
                    testNode.Properties.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.StandardOutput", message));
                }

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

        methodIdentifierProperty = GetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(managedType, managedMethod);
        return true;
    }

    private static TestMethodIdentifierProperty GetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(
        string managedType,
        string managedMethod)
    {
        ManagedNameParser.ParseManagedMethodName(managedMethod, out string methodName, out int arity, out string[]? parameterTypes);
        if (arity != 0)
        {
            methodName = $"{methodName}`{arity.ToString(CultureInfo.InvariantCulture)}";
        }

        parameterTypes ??= [];

        ManagedNameParser.ParseManagedTypeName(managedType, out string @namespace, out string typeName);

        // In the context of the VSTestBridge where we only have access to VSTest object model, we cannot determine ReturnTypeFullName.
        // For now, we lose this bit of information.
        // If really needed in the future, we can introduce a VSTest property to hold this info.
        // But the eventual goal should be to stop using the VSTestBridge altogether.
        // TODO: For AssemblyFullName, can we use Assembly.GetEntryAssembly().FullName?
        // Or alternatively, does VSTest object model expose the assembly full name somewhere?
        return new TestMethodIdentifierProperty(AssemblyFullName: string.Empty, @namespace, typeName, methodName, parameterTypes, ReturnTypeFullName: string.Empty);
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
