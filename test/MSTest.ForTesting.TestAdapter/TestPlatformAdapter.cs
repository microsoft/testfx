// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace MSTest.ForTesting.TestAdapter;
[DefaultExecutorUri(Constants.ExecutorUri)]
[ExtensionUri(Constants.ExecutorUri)]
internal sealed class TestPlatformAdapter : ITestDiscoverer, ITestExecutor
{
    private CancellationTokenSource _testRunCancellationTokenSource;

    /// <inheritdoc/>
    public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
    {
        foreach (var testCase in DiscoverTests(sources, logger))
        {
            discoverySink.SendTestCase(testCase);
        }
    }

    public void Cancel()
    {
        _testRunCancellationTokenSource.Cancel();
    }

    public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
    {
        if (!Debugger.IsAttached) Debugger.Launch();
        _testRunCancellationTokenSource = new CancellationTokenSource();

        // TODO: Group by assembly/type or use some dictionary for quick lookup.
        foreach (var testCase in tests)
        {
            try
            {
                var testResult = new TestResult(testCase);
                _testRunCancellationTokenSource.Token.ThrowIfCancellationRequested();
                if (!TryFindMethodsToRun(testCase, frameworkHandle, out var testContainerType, out var setupMethod, out var teardownMethod, out var testMethod))
                {
                    testResult.Outcome = TestOutcome.NotFound;
                    frameworkHandle.RecordResult(testResult);
                    continue;
                }

                _testRunCancellationTokenSource.Token.ThrowIfCancellationRequested();
                frameworkHandle.RecordStart(testCase);
                if (TryRunTestSetup(setupMethod, testCase.DisplayName, testContainerType.FullName, testResult, frameworkHandle, out var testClassInstance))
                {
                    // Only run test if test setup was successful.
                    TryRunTest(testMethod, testClassInstance, testCase.DisplayName, testResult, frameworkHandle);
                }

                // Always call teardown even if previous steps failed because we want to try to clean as much as we can.
                TryRunTestTeardown(teardownMethod, testClassInstance, testCase.DisplayName, testContainerType.FullName, testResult, frameworkHandle);

                testResult.EndTime = DateTimeOffset.UtcNow;
                testResult.Duration = testResult.EndTime - testResult.StartTime;
                if (testResult.Outcome != TestOutcome.Failed)
                {
                    testResult.Outcome = TestOutcome.Passed;
                }

                frameworkHandle.RecordEnd(testCase, testResult.Outcome);
                frameworkHandle.RecordResult(testResult);
            }
            catch (OperationCanceledException)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "Test run was canceled.");
                return;
            }
        }
    }

    public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
    {
        var testCases = DiscoverTests(sources, frameworkHandle);
        RunTests(testCases, runContext, frameworkHandle);
    }

    private static string MakeFullyQualifiedName(TypeInfo type, MethodInfo method)
        => $"{type.FullName}.{method.Name}";

    private static IEnumerable<TestCase> DiscoverTests(IEnumerable<string> sources, IMessageLogger logger)
    {
        // TODO: Fail if no sources?

        foreach (var assemblyName in sources)
        {
            logger.SendMessage(TestMessageLevel.Informational, $"Discovering tests in assembly '{assemblyName}'");

            var assembly = Assembly.LoadFrom(assemblyName);
            var assemblyTestContainerTypes = assembly.DefinedTypes.Where(typeInfo =>
                // TODO: Improve by looking up base types.
                typeInfo.BaseType == typeof(TestContainer));

            // TODO: Fail if no container?

            foreach (var testContainerType in assemblyTestContainerTypes)
            {
                logger.SendMessage(TestMessageLevel.Informational, $"Discovering tests for container '{testContainerType.FullName}'");

                var testContainerPublicMethods = testContainerType.DeclaredMethods.Where(memberInfo => memberInfo.IsPublic);

                // TODO: Fail if no public method?

                foreach (var publicMethod in testContainerPublicMethods)
                {
                    logger.SendMessage(TestMessageLevel.Informational, $"Found test '{publicMethod.Name}'");
                    yield return new(MakeFullyQualifiedName(testContainerType, publicMethod), new(Constants.ExecutorUri), assemblyName);
                }
            }
        }
    }

    private static bool TryFindMethodsToRun(TestCase testCase, IMessageLogger logger, out TypeInfo testContainerType,
        out ConstructorInfo setupMethod, out MethodInfo teardownMethod, out MethodInfo testMethod)
    {
        try
        {
            logger.SendMessage(TestMessageLevel.Informational, $"Trying to find test '{testCase.DisplayName}'");
            var assembly = Assembly.LoadFrom(testCase.Source);

            testContainerType = assembly.DefinedTypes.Single(typeInfo => testCase.FullyQualifiedName.StartsWith(typeInfo.FullName, StringComparison.Ordinal));
            // Is it better to use Activator.CreateInstance?
            setupMethod = testContainerType.DeclaredConstructors.Single(ctorInfo => ctorInfo.IsPublic && ctorInfo.GetParameters().Length == 0);
            teardownMethod = testContainerType.BaseType.GetMethod("Dispose");
            var type = testContainerType;
            testMethod = testContainerType.DeclaredMethods.Single(methodInfo => string.Equals(MakeFullyQualifiedName(type, methodInfo), testCase.FullyQualifiedName, StringComparison.Ordinal));

            return true;
        }
        catch (Exception ex)
        {
            logger.SendMessage(TestMessageLevel.Error, $"Error trying to find test case '{testCase.DisplayName}': {ex}");
            testContainerType = null;
            setupMethod = null;
            teardownMethod = null;
            testMethod = null;

            return false;
        }
    }

    private static bool TryRunTestSetup(ConstructorInfo setupMethod, string testCaseDisplayName, string testContainerTypeFullName, TestResult testResult, IMessageLogger logger, out object testCaseInstance)
    {
        try
        {
            logger.SendMessage(TestMessageLevel.Informational, $"Executing test '{testCaseDisplayName}' setup (ctor for '{testContainerTypeFullName}')");
            testCaseInstance = setupMethod.Invoke(null);

            return true;
        }
        catch (Exception ex)
        {
            logger.SendMessage(TestMessageLevel.Error, $"Error during test setup: {ex}");
            testResult.Outcome = TestOutcome.Failed;
            testResult.ErrorMessage = $"Error during test setup: {ex.Message}";
            testResult.ErrorStackTrace = ex.StackTrace;
            testCaseInstance = null;

            return false;
        }
    }

    private static bool TryRunTest(MethodInfo testMethod, object testClassInstance, string testDisplayName, TestResult testResult, IMessageLogger logger)
    {
        try
        {
            logger.SendMessage(TestMessageLevel.Informational, $"Executing test '{testDisplayName}'");
            testMethod.Invoke(testClassInstance, null);

            return true;
        }
        catch (Exception ex)
        {
            testResult.Outcome = TestOutcome.Failed;
            testResult.ErrorMessage = $"Error during test: {ex.Message}";
            testResult.ErrorStackTrace = ex.StackTrace;
            logger.SendMessage(TestMessageLevel.Error, $"Error during test: {ex}");

            return false;
        }
    }

    private static bool TryRunTestTeardown(MethodInfo teardownMethod, object testClassInstance, string testCaseDisplayName, string testContainerTypeFullName, TestResult testResult, IMessageLogger logger)
    {
        try
        {
            if (testClassInstance is not null)
            {
                logger.SendMessage(TestMessageLevel.Informational, $"Executing test '{testCaseDisplayName}' teardown (dispose for '{testContainerTypeFullName}')");
                teardownMethod.Invoke(testClassInstance, null);
            }

            return true;
        }
        catch (Exception ex)
        {
            testResult.Outcome = TestOutcome.Failed;
            // TODO: It's possible there is already some error message + stack trace. We should merge instead of override.
            testResult.ErrorMessage = $"Error during test teardown: {ex.Message}";
            testResult.ErrorStackTrace = ex.StackTrace;
            logger.SendMessage(TestMessageLevel.Error, $"Error during test teardown: {ex}");

            return false;
        }
    }
}
