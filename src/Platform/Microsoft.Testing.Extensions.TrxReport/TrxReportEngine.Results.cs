// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private SummaryCounts AddResults(TestNodeUpdateMessage[] testNodeUpdateMessages, string testAppModule, XElement testRun, string runDeploymentRoot, XElement testDefinitions, XElement testEntries)
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

        foreach (TestNodeUpdateMessage nodeMessage in testNodeUpdateMessages)
        {
            TestNode testNode = nodeMessage.TestNode;

            // If already a guid (it's the case for at least MSTest), use that guid directly.
            // Otherwise, convert the string to a guid.
            if (!Guid.TryParse(testNode.Uid.Value, out Guid guid))
            {
                guid = GuidFromString(testNode.Uid.Value);
            }

            // NOTE: In VSTest, MSTestDiscoverer.TmiTestId property is preferred if present.
            string id = guid.ToString();
            string testResultDisplayName = RemoveInvalidXmlChar(testNode.DisplayName)!;
            (string testDefinitionName, bool isExplicitlyProvided) = testNode.Properties.SingleOrDefault<TrxTestDefinitionName>() is { } trxTestDefinitionName
                ? (RemoveInvalidXmlChar(trxTestDefinitionName.TestDefinitionName), true)
                : (testResultDisplayName, false);

            string executionId = Guid.NewGuid().ToString();

            // Results
            var unitTestResult = new XElement(
                "UnitTestResult",
                new XAttribute("executionId", executionId),
                new XAttribute("testId", id),
                new XAttribute("testName", testResultDisplayName),
                new XAttribute("computerName", _environment.MachineName));

            TimingProperty? timing = testNode.Properties.SingleOrDefault<TimingProperty>();
            string testDuration = timing?.GlobalTiming.Duration is { } duration
                ? duration.ToString("hh\\:mm\\:ss\\.fffffff", CultureInfo.InvariantCulture)
                : "00:00:00";
            unitTestResult.SetAttributeValue("duration", testDuration);

            unitTestResult.SetAttributeValue(
                "startTime",
                timing?.GlobalTiming.StartTime.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));
            unitTestResult.SetAttributeValue(
                "endTime",
                timing?.GlobalTiming.EndTime.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));

            // In VSTest, other test types originate from adding TestProperty with
            // Id TestType (see Constants.TestTypePropertyIdentifier).
            // The property is only considered if it has value ec4800e8-40e5-4ab3-8510-b8bf29b1904d (OrderedTestType)
            // In the context of MTP, we don't care.
            unitTestResult.SetAttributeValue("testType", UnitTestTypeGuid);

            string currentTestOutcome = "Passed";

            // In TrxReportGenerator.ConsumeAsync, we already filtered to only the nodes that contain TestNodeStateProperty.
            // We also filtered out discovered and in-progress states.
            // So the call to Single here should never fail, and should never be discovered or in-progress.
            TestNodeStateProperty testState = testNode.Properties.Single<TestNodeStateProperty>();
            if (testState is DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
            {
                throw ApplicationStateGuard.Unreachable();
            }

            if (testState is SkippedTestNodeStateProperty)
            {
                currentTestOutcome = "NotExecuted";
                skipped++;
            }
            else if (testState is PassedTestNodeStateProperty)
            {
                passed++;
            }
            else if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, testState.GetType()) >= 0)
            {
                currentTestOutcome = "Failed";

                if (testState is TimeoutTestNodeStateProperty)
                {
                    timedout++;
                }
                else
                {
                    failed++;
                }
            }
            else
            {
                // Above conditions should have handled all state properties.
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

            TrxMessagesProperty? trxMessages = testNode.Properties.SingleOrDefault<TrxMessagesProperty>();
            IEnumerable<string?>? nonErrorMessages = trxMessages?.Messages.Where(x => x is not StandardErrorTrxMessage and not DebugOrTraceTrxMessage).Select(x => x.Message);
            if (nonErrorMessages?.Any() == true)
            {
                output.Add(new XElement("StdOut", RemoveInvalidXmlChar(string.Join(Environment.NewLine, nonErrorMessages))));
            }

            IEnumerable<string?>? errorMessages = trxMessages?.Messages.Where(x => x is StandardErrorTrxMessage).Select(x => x.Message);
            if (errorMessages?.Any() == true)
            {
                output.Add(new XElement("StdErr", RemoveInvalidXmlChar(string.Join(Environment.NewLine, errorMessages))));
            }

            IEnumerable<string?>? debugOrTraceMessages = trxMessages?.Messages.Where(x => x is DebugOrTraceTrxMessage).Select(x => x.Message);
            if (debugOrTraceMessages?.Any() == true)
            {
                output.Add(new XElement("DebugTrace", RemoveInvalidXmlChar(string.Join(Environment.NewLine, debugOrTraceMessages))));
            }

            TrxExceptionProperty? trxException = testNode.Properties.SingleOrDefault<TrxExceptionProperty>();
            if (trxException?.Message is not null || trxException?.StackTrace is not null)
            {
                XElement errorInfoElement = new("ErrorInfo");

                if (trxException.Message is not null)
                {
                    errorInfoElement.Add(new XElement("Message", RemoveInvalidXmlChar(trxException.Message)));
                }

                if (trxException.StackTrace is not null)
                {
                    errorInfoElement.Add(new XElement("StackTrace", RemoveInvalidXmlChar(trxException.StackTrace)));
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
            foreach (FileArtifactProperty testFileArtifact in testNode.Properties.OfType<FileArtifactProperty>())
            {
                resultFiles ??= new XElement("ResultFiles");

                string href = CopyArtifactIntoTrxDirectoryAndReturnHrefValue(testFileArtifact.FileInfo, runDeploymentRoot, executionId);
                resultFiles.Add(new XElement(
                    "ResultFile",
                    new XAttribute("path", href)));
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
                XElement unitTest = CreateUnitTestElementForTestDefinition(testDefinitionName, testAppModule, id, testNode, executionId);

                var testMethod = new XElement(
                    "TestMethod",
                    new XAttribute("codeBase", testAppModule),
                    new XAttribute("adapterTypeName", $"executor://{_testFrameworkAdapter.Uid}/{_testFrameworkAdapter.Version}"));

                // NOTE: className is required by TRX XSD.
                (string className, string? testMethodName) = GetClassAndMethodName(testNode);
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

    private (string ClassName, string? TestMethodName) GetClassAndMethodName(TestNode testNode)
    {
        TestMethodIdentifierProperty? testMethodIdentifierProperty = testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        if (testNode.Properties.SingleOrDefault<TrxFullyQualifiedTypeNameProperty>()?.FullyQualifiedTypeName is { } className)
        {
            return (className, testMethodIdentifierProperty?.MethodName);
        }

        _ = testMethodIdentifierProperty ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFrameworkAdapter.DisplayName, _testFrameworkAdapter.Uid));

        string classNameFromIdentifierProperty = RoslynString.IsNullOrEmpty(testMethodIdentifierProperty.Namespace)
            ? testMethodIdentifierProperty.TypeName
            : $"{testMethodIdentifierProperty.Namespace}.{testMethodIdentifierProperty.TypeName}";

        // TODO: Are we expected to append backtick and arity here for generic methods?
        return (classNameFromIdentifierProperty, testMethodIdentifierProperty.MethodName);
    }
}
