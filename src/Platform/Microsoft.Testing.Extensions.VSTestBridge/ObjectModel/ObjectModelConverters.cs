// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
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
    public static TestNode ToTestNode(this TestCase testCase, bool isTrxEnabled, string? displayNameFromTestResult = null)
    {
        string testNodeUid = testCase.Id.ToString();

        TestNode testNode = new()
        {
            Uid = new TestNodeUid(testNodeUid),
            DisplayName = displayNameFromTestResult ?? testCase.DisplayName ?? testCase.FullyQualifiedName,
        };

        if (TryGetMethodIdentifierProperty(testCase, out TestMethodIdentifierProperty? methodIdentifierProperty))
        {
            testNode.Properties.Add(methodIdentifierProperty);
        }
        else
        {
            throw new InvalidOperationException("Unable to parse fully qualified type name from test case: " + testCase.FullyQualifiedName);
        }

        CopyVSTestProperties(testCase.Properties, testNode, testCase, testCase.GetPropertyValue, isTrxEnabled);
        if (testCase.CodeFilePath is not null)
        {
            testNode.Properties.Add(new TestFileLocationProperty(testCase.CodeFilePath, new(new(testCase.LineNumber, -1), new(testCase.LineNumber, -1))));
        }

        return testNode;
    }

    private static void CopyVSTestProperties(IEnumerable<TestProperty> testProperties, TestNode testNode, TestCase testCase, Func<TestProperty, object?> getPropertyValue,
        bool isTrxEnabled)
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
    public static TestNode ToTestNode(this TestResult testResult, bool isTrxEnabled)
    {
        var testNode = testResult.TestCase.ToTestNode(isTrxEnabled, testResult.DisplayName);
        CopyVSTestProperties(testResult.Properties, testNode, testResult.TestCase, testResult.GetPropertyValue, isTrxEnabled);
        testNode.AddOutcome(testResult);

        if (isTrxEnabled)
        {
            testNode.Properties.Add(new TrxExceptionProperty(testResult.ErrorMessage, testResult.ErrorStackTrace));
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
        return !RoslynString.IsNullOrEmpty(managedType) && !RoslynString.IsNullOrEmpty(managedMethod)
            ? TryGetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(managedType, managedMethod, out methodIdentifierProperty)
            : TryGetMethodIdentifierPropertyFromFullyQualifiedName(testCase.FullyQualifiedName, out methodIdentifierProperty);
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

    private static bool TryGetMethodIdentifierPropertyFromFullyQualifiedName(ReadOnlySpan<char> fullyQualifiedName, [NotNullWhen(true)] out TestMethodIdentifierProperty? methodIdentifierProperty)
    {
        int indexOfParen = fullyQualifiedName.IndexOf('(');

        int lastIndexOfDotBeforeParen = indexOfParen == -1
            ? fullyQualifiedName.LastIndexOf('.')
            : fullyQualifiedName.Slice(0, indexOfParen).LastIndexOf('.');

        if (lastIndexOfDotBeforeParen == -1)
        {
            methodIdentifierProperty = null;
            return false;
        }

        return TryGetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(
            managedType: fullyQualifiedName.Slice(0, lastIndexOfDotBeforeParen),
            managedMethod: fullyQualifiedName.Slice(lastIndexOfDotBeforeParen + 1),
            out methodIdentifierProperty);
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
}
