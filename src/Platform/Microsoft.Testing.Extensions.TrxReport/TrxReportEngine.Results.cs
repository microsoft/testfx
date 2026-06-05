// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private SummaryCounts AddResults(IReadOnlyList<TrxTestResult> testResults, string testAppModule, XElement testRun, string runDeploymentRoot, XElement testDefinitions, XElement testEntries, List<string> attachmentWarnings)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedout = 0;
        var results = new XElement("Results");

        // Duplicate test ids are not allowed inside the TestDefinitions element.
        // We create a dictionary to map test id to test definition name.
        // It's not expected to get the same test id twice but with different test definition name.
        // However, due to backcompat concerns, we will disallow this only for frameworks that start using TrxTestDefinitionName property.
        var uniqueTestDefinitionTestIds = new Dictionary<string, (string TestDefinitionName, bool IsExplicitlyProvided)>();

        foreach (TrxTestResult testResult in testResults)
        {
            // If already a guid (it's the case for at least MSTest), use that guid directly.
            // Otherwise, convert the string to a guid.
            if (!Guid.TryParse(testResult.Uid, out Guid guid))
            {
                guid = GuidFromString(testResult.Uid);
            }

            // NOTE: In VSTest, MSTestDiscoverer.TmiTestId property is preferred if present.
            string id = guid.ToString();
            string testResultDisplayName = RemoveInvalidXmlChar(testResult.DisplayName)!;
            (string testDefinitionName, bool isExplicitlyProvided) = testResult.TrxTestDefinitionName is { } explicitName
                ? (RemoveInvalidXmlChar(explicitName)!, true)
                : (testResultDisplayName, false);

            string executionId = Guid.NewGuid().ToString();

            // Results
            var unitTestResult = new XElement(
                "UnitTestResult",
                new XAttribute("executionId", executionId),
                new XAttribute("testId", id),
                new XAttribute("testName", testResultDisplayName),
                new XAttribute("computerName", _environment.MachineName));

            string testDuration = testResult.Duration is { } duration
                ? duration.ToString("hh\\:mm\\:ss\\.fffffff", CultureInfo.InvariantCulture)
                : "00:00:00";
            unitTestResult.SetAttributeValue("duration", testDuration);

            unitTestResult.SetAttributeValue(
                "startTime",
                testResult.StartTime?.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));
            unitTestResult.SetAttributeValue(
                "endTime",
                testResult.EndTime?.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));

            // In VSTest, other test types originate from adding TestProperty with
            // Id TestType (see Constants.TestTypePropertyIdentifier).
            // The property is only considered if it has value ec4800e8-40e5-4ab3-8510-b8bf29b1904d (OrderedTestType)
            // In the context of MTP, we don't care.
            unitTestResult.SetAttributeValue("testType", UnitTestTypeGuid);

            string currentTestOutcome = testResult.Outcome switch
            {
                TrxTestOutcome.Skipped => "NotExecuted",
                TrxTestOutcome.Passed => "Passed",
                TrxTestOutcome.Failed or TrxTestOutcome.Timeout => "Failed",
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            switch (testResult.Outcome)
            {
                case TrxTestOutcome.Skipped:
                    skipped++;
                    break;
                case TrxTestOutcome.Passed:
                    passed++;
                    break;
                case TrxTestOutcome.Timeout:
                    timedout++;
                    break;
                case TrxTestOutcome.Failed:
                    failed++;
                    break;
                default:
                    throw ApplicationStateGuard.Unreachable();
            }

            unitTestResult.SetAttributeValue("outcome", currentTestOutcome);

            unitTestResult.SetAttributeValue("testListId", UncategorizedTestListId);

            // It has the same value as executionId
            unitTestResult.SetAttributeValue("relativeResultsDirectory", executionId);

            // Below we're escaping most "dynamic body" using .Replace("\0", ""), because this is an invalid xml character.
            // There are other invalid xml characters, but they're transformed inside the writer in a correct way so we try to
            // rely on the built-in escaping/conversion.
            // i.e. https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlEncodedRawTextWriter.cs#L890
            var output = new XElement("Output");

            IReadOnlyList<TrxStreamMessage>? trxMessages = testResult.Messages;
            if (trxMessages is not null)
            {
                // Single pass: partition messages by kind into separate StringBuilders,
                // avoiding the double-enumeration that .Any() + string.Join() would cause.
                StringBuilder? stdOut = null;
                StringBuilder? stdErr = null;
                StringBuilder? debugTrace = null;

                foreach (TrxStreamMessage msg in trxMessages)
                {
                    switch (msg.Kind)
                    {
                        case TrxStreamMessageKind.StandardOutput:
                            stdOut?.Append(Environment.NewLine);
                            (stdOut ??= new StringBuilder()).Append(msg.Message);
                            break;
                        case TrxStreamMessageKind.StandardError:
                            stdErr?.Append(Environment.NewLine);
                            (stdErr ??= new StringBuilder()).Append(msg.Message);
                            break;
                        case TrxStreamMessageKind.DebugOrTrace:
                            debugTrace?.Append(Environment.NewLine);
                            (debugTrace ??= new StringBuilder()).Append(msg.Message);
                            break;
                    }
                }

                if (stdOut is not null)
                {
                    output.Add(new XElement("StdOut", RemoveInvalidXmlChar(stdOut.ToString())));
                }

                if (stdErr is not null)
                {
                    output.Add(new XElement("StdErr", RemoveInvalidXmlChar(stdErr.ToString())));
                }

                if (debugTrace is not null)
                {
                    output.Add(new XElement("DebugTrace", RemoveInvalidXmlChar(debugTrace.ToString())));
                }
            }

            if (testResult.ExceptionMessage is not null || testResult.ExceptionStackTrace is not null)
            {
                XElement errorInfoElement = new("ErrorInfo");

                if (testResult.ExceptionMessage is not null)
                {
                    errorInfoElement.Add(new XElement("Message", RemoveInvalidXmlChar(testResult.ExceptionMessage)));
                }

                if (testResult.ExceptionStackTrace is not null)
                {
                    errorInfoElement.Add(new XElement("StackTrace", RemoveInvalidXmlChar(testResult.ExceptionStackTrace)));
                }

                output.Add(errorInfoElement);
            }

            if (output.HasElements)
            {
                unitTestResult.Add(output);
            }

            // TODO: VSTest used to store the relative paths in a sorted list (ignoring case).
            // TODO: VSTest is able to classify per-test attachments into two categories:
            // 1. ResultFiles
            // 2. CollectorDataEntries
            // So far, we only have "ResultFiles".
            XElement? resultFiles = null;
            if (testResult.FileArtifacts is not null)
            {
                foreach (TrxTestFileArtifact testFileArtifact in testResult.FileArtifacts)
                {
                    if (!TryCopyArtifactAndGetHref(new FileInfo(testFileArtifact.FullPath), runDeploymentRoot, executionId, attachmentWarnings, out string? href))
                    {
                        continue;
                    }

                    resultFiles ??= new XElement("ResultFiles");
                    resultFiles.Add(new XElement(
                        "ResultFile",
                        new XAttribute("path", href!)));
                }
            }

            if (resultFiles is not null)
            {
                unitTestResult.Add(resultFiles);
            }

            results.Add(unitTestResult);

            // TestDefinitions
            // Add the test method to the test definitions if it's not already there
            if (uniqueTestDefinitionTestIds.TryGetValue(id, out (string ExistingTestDefinitionName, bool ExistingIsExplicitlyProvided) existing))
            {
                // Value already exists. We only do a validation.
                // Owner, Description, Priority, and TestCategory are also part of the test definition.
                // Unfortunately, MSTest allows TestCategories via TestDataRow, which is one case where
                // we might receive the same test id and same test definition name, but different categories.
                // It's probably best if TRX is able to "merge" categories in this case (which we don't do yet).
                // For Owner, Description, Priority, this needs investigation whether or not it's expected to be different,
                // and what should we do in this case.
                if ((isExplicitlyProvided || existing.ExistingIsExplicitlyProvided) &&
                    existing.ExistingTestDefinitionName != testDefinitionName)
                {
                    throw new InvalidOperationException($"Received two different test definition names ('{existing.ExistingTestDefinitionName}' and '{testDefinitionName}') for the same test id '{id}'.");
                }

                if (!existing.ExistingIsExplicitlyProvided && isExplicitlyProvided)
                {
                    // We got a first result that didn't have explicit test definition name, but a second result that has an explicit test definition name.
                    uniqueTestDefinitionTestIds[id] = (testDefinitionName, true);
                }
            }
            else
            {
                uniqueTestDefinitionTestIds.Add(id, (testDefinitionName, isExplicitlyProvided));
                XElement unitTest = CreateUnitTestElementForTestDefinition(testDefinitionName, testAppModule, id, testResult, executionId);

                var testMethod = new XElement(
                    "TestMethod",
                    new XAttribute("codeBase", testAppModule),
                    new XAttribute("adapterTypeName", $"executor://{_testFrameworkAdapter.Uid}/{_testFrameworkAdapter.Version}"));

                // NOTE: className is required by TRX XSD.
                (string className, string? testMethodName) = GetClassAndMethodName(testResult);
                testMethod.SetAttributeValue("className", className);

                // NOTE: Historically, MTP used to always use testResultDisplayName here.
                // While VSTest never uses testResultDisplayName.
                // The use of testResultDisplayName here is very wrong.
                // We keep it as a fallback if we cannot determine the testMethodName (when TestMethodIdentifierProperty isn't present).
                // This will most likely be hit for NUnit.
                // However, this is very wrong and we probably should fail if TestMethodIdentifierProperty isn't present.
                testMethod.SetAttributeValue("name", testMethodName ?? testResultDisplayName);

                unitTest.Add(testMethod);

                testDefinitions.Add(unitTest);
            }

            // testEntry
            // NOTE: VSTest implementation ensures that we don't duplicate TestEntry elements with the same executionId.
            // However, our implementation always gets a fresh Guid so we don't need that special handling.
            // If we added the concept of "parent execution id" to MTP TRX and allow a way to
            // specify a parent-child relationship (e.g, for parameterized tests), we will need to
            // revise this.
            // The way VSTest does it, it allows test frameworks to set executionId and parentExecutionId on test results.
            var testEntry = new XElement(
                "TestEntry",
                new XAttribute("testId", id),
                new XAttribute("executionId", executionId),
                new XAttribute("testListId", UncategorizedTestListId));
            testEntries.Add(testEntry);
        }

        testRun.Add(results);

        return new SummaryCounts(passed, failed, skipped, timedout);
    }

    private (string ClassName, string? TestMethodName) GetClassAndMethodName(TrxTestResult testResult)
    {
        TrxTestMethodIdentifier? methodIdentifier = testResult.TestMethodIdentifier;

        if (testResult.TrxFullyQualifiedTypeName is { } className)
        {
            return (className, methodIdentifier?.MethodName);
        }

        _ = methodIdentifier ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFrameworkAdapter.DisplayName, _testFrameworkAdapter.Uid));

        string classNameFromIdentifierProperty = RoslynString.IsNullOrEmpty(methodIdentifier.Namespace)
            ? methodIdentifier.TypeName
            : $"{methodIdentifier.Namespace}.{methodIdentifier.TypeName}";

        // TODO: Are we expected to append backtick and arity here for generic methods?
        return (classNameFromIdentifierProperty, methodIdentifier.MethodName);
    }
}
