// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TestFramework.ForTestingMSTest;

[DefaultExecutorUri(Constants.ExecutorUri)]
[ExtensionUri(Constants.ExecutorUri)]
internal sealed class AdapterToTestPlatform : ITestDiscoverer, ITestExecutor, IDisposable
{
    private CancellationTokenSource? _testRunCancellationTokenSource;
    private bool _isDisposed;

    /// <inheritdoc/>
    public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_DEBUG_DISCOVERTESTS") == "1")
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }

        foreach (TestCase testCase in DiscoverTests(sources, logger))
        {
            discoverySink.SendTestCase(testCase);
        }
    }

    public void Cancel() => _testRunCancellationTokenSource?.Cancel();

    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_DEBUG_RUNTESTS") == "1")
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }

        _testRunCancellationTokenSource = new CancellationTokenSource();

        if (tests is null || !tests.Any())
        {
            LogMessage(frameworkHandle, TestMessageLevel.Error, "No test assemblies were provided");
            return;
        }

        // TODO: Group by assembly/type or use some dictionary for quick lookup.
        // TODO: Run in parallel?
        foreach (TestCase testCase in tests)
        {
            try
            {
                var testResult = new TestResult(testCase);
                _testRunCancellationTokenSource.Token.ThrowIfCancellationRequested();
                if (!TryFindMethodsToRun(testCase, frameworkHandle, out TypeInfo? testContainerType, out ConstructorInfo? setupMethod,
                    out MethodInfo? teardownMethod, out MethodInfo? testMethod))
                {
                    testResult.Outcome = TestOutcome.NotFound;
                    frameworkHandle?.RecordResult(testResult);
                    continue;
                }

                _testRunCancellationTokenSource.Token.ThrowIfCancellationRequested();
                frameworkHandle?.RecordStart(testCase);
                if (TryRunTestSetup(setupMethod, testCase.DisplayName, testContainerType.FullName, testResult, frameworkHandle,
                    out object? testClassInstance))
                {
                    // Only run test if test setup was successful.
                    TryRunTestAsync(testMethod, testClassInstance, testCase.DisplayName, testResult, frameworkHandle)
                        .GetAwaiter()
                        .GetResult();
                }

                // Always call teardown even if previous steps failed because we want to try to clean as much as we can.
                TryRunTestTeardown(teardownMethod, testClassInstance, testCase.DisplayName, testContainerType.FullName, testResult, frameworkHandle);

                testResult.EndTime = DateTimeOffset.UtcNow;
                testResult.Duration = testResult.EndTime - testResult.StartTime;
                if (testResult.Outcome != TestOutcome.Failed)
                {
                    testResult.Outcome = TestOutcome.Passed;
                }

                frameworkHandle?.RecordEnd(testCase, testResult.Outcome);
                frameworkHandle?.RecordResult(testResult);
            }
            catch (OperationCanceledException)
            {
                LogMessage(frameworkHandle, TestMessageLevel.Informational, "Test run was canceled.");
                return;
            }
        }
    }

    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1")
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }

        IEnumerable<TestCase> testCases = DiscoverTests(sources, frameworkHandle);
        RunTests(testCases, runContext, frameworkHandle);
    }

    private static string MakeFullyQualifiedName(TypeInfo type, MethodInfo method)
        => $"{type.FullName}.{method.Name}";

    private static void LogMessage(IMessageLogger? logger, TestMessageLevel level, string message)
    {
        if (logger is not null)
        {
            logger.SendMessage(level, message);
        }
        else
        {
            Console.WriteLine($"[{level}] {message}");
        }
    }

    // Looking up base types.
    private static bool IsTestContainer(Type typeInfo)
    {
        while (typeInfo != null)
        {
            if (typeInfo == typeof(TestContainer))
            {
                return true;
            }

            typeInfo = typeInfo.BaseType;
        }

        return false;
    }

    private static IEnumerable<TestCase> DiscoverTests(IEnumerable<string>? assemblies, IMessageLogger? logger)
    {
        if (assemblies is null || !assemblies.Any())
        {
            // TODO: Fail if no assemblies?
            LogMessage(logger, TestMessageLevel.Error, "No test assemblies were provided");
            yield break;
        }

        // TODO: Discover in parallel?
        foreach (string assemblyName in assemblies)
        {
            LogMessage(logger, TestMessageLevel.Informational, $"Discovering tests in assembly '{assemblyName}'");

            var assembly = Assembly.LoadFrom(assemblyName);
            IEnumerable<TypeInfo> assemblyTestContainerTypes = assembly.DefinedTypes.Where(IsTestContainer);

            // TODO: Fail if no container?
            foreach (TypeInfo? testContainerType in assemblyTestContainerTypes)
            {
                LogMessage(logger, TestMessageLevel.Informational,
                    $"Discovering tests for container '{testContainerType.FullName}'");

                IEnumerable<MethodInfo> testContainerPublicMethods = testContainerType.DeclaredMethods.Where(memberInfo =>
                    memberInfo.IsPublic
                    && (memberInfo.ReturnType == typeof(void) || memberInfo.ReturnType == typeof(Task))
                    && memberInfo.GetParameters().Length == 0);

                // TODO: Fail if no public method?
                foreach (MethodInfo? publicMethod in testContainerPublicMethods)
                {
                    LogMessage(logger, TestMessageLevel.Informational, $"Found test '{publicMethod.Name}'");
                    yield return new(MakeFullyQualifiedName(testContainerType, publicMethod), new(Constants.ExecutorUri), assemblyName);
                }
            }
        }
    }

    private static bool TryFindMethodsToRun(TestCase testCase, IMessageLogger? logger,
        [NotNullWhen(true)] out TypeInfo? testContainerType,
        [NotNullWhen(true)] out ConstructorInfo? setupMethod,
        [NotNullWhen(true)] out MethodInfo? teardownMethod,
        [NotNullWhen(true)] out MethodInfo? testMethod)
    {
        try
        {
            LogMessage(logger, TestMessageLevel.Informational, $"Trying to find test '{testCase.DisplayName}'");
            var assembly = Assembly.LoadFrom(testCase.Source);

            testContainerType = assembly.DefinedTypes.Single(typeInfo =>
                testCase.FullyQualifiedName.StartsWith(typeInfo.FullName, StringComparison.Ordinal));

            // Is it better to use Activator.CreateInstance?
            setupMethod = testContainerType.DeclaredConstructors.Single(ctorInfo =>
                ctorInfo.IsPublic
                && ctorInfo.GetParameters().Length == 0);
            teardownMethod = testContainerType.BaseType.GetMethod("Dispose");
            TypeInfo type = testContainerType;
            testMethod = testContainerType.DeclaredMethods.Single(methodInfo =>
                string.Equals(MakeFullyQualifiedName(type, methodInfo), testCase.FullyQualifiedName, StringComparison.Ordinal));

            return true;
        }
        catch (Exception ex)
        {
            LogMessage(logger, TestMessageLevel.Error,
                $"Error trying to find test case '{testCase.DisplayName}': {ex}");
            testContainerType = null;
            setupMethod = null;
            teardownMethod = null;
            testMethod = null;

            return false;
        }
    }

    private static bool TryRunTestSetup(ConstructorInfo setupMethod, string testCaseDisplayName,
        string testContainerTypeFullName, TestResult testResult, IMessageLogger? logger,
        [NotNullWhen(true)] out object? testCaseInstance)
    {
        try
        {
            LogMessage(logger, TestMessageLevel.Informational,
                $"Executing test '{testCaseDisplayName}' setup (ctor for '{testContainerTypeFullName}')");
            testCaseInstance = setupMethod.Invoke(null);

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.InnerException ?? ex;
            LogMessage(logger, TestMessageLevel.Error, $"Error during test setup: {realException}");
            testResult.Outcome = TestOutcome.Failed;
            testResult.ErrorMessage = $"Error during test setup: {realException.Message}";
            testResult.ErrorStackTrace = realException.StackTrace;
            testCaseInstance = null;

            return false;
        }
    }

    private static async Task<bool> TryRunTestAsync(MethodInfo testMethod, object testClassInstance, string testDisplayName,
        TestResult testResult, IMessageLogger? logger)
    {
        try
        {
            LogMessage(logger, TestMessageLevel.Informational, $"Executing test '{testDisplayName}'");
            if (testMethod.Invoke(testClassInstance, null) is Task task)
            {
                await task;
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.InnerException ?? ex;
            LogMessage(logger, TestMessageLevel.Error, $"Error during test: {realException}");
            testResult.Outcome = TestOutcome.Failed;
            testResult.ErrorMessage = $"Error during test: {realException.Message}";
            testResult.ErrorStackTrace = realException.StackTrace;

            return false;
        }
    }

    private static bool TryRunTestTeardown(MethodInfo teardownMethod, object? testClassInstance, string testCaseDisplayName,
        string testContainerTypeFullName, TestResult testResult, IMessageLogger? logger)
    {
        try
        {
            if (testClassInstance is not null)
            {
                LogMessage(logger, TestMessageLevel.Informational,
                    $"Executing test '{testCaseDisplayName}' teardown (dispose for '{testContainerTypeFullName}')");
                teardownMethod.Invoke(testClassInstance, null);
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.InnerException ?? ex;
            LogMessage(logger, TestMessageLevel.Error, $"Error during test teardown: {realException}");
            testResult.Outcome = TestOutcome.Failed;

            // TODO: It's possible there is already some error message + stack trace. We should merge instead of override.
            testResult.ErrorMessage = $"Error during test teardown: {realException.Message}";
            testResult.ErrorStackTrace = realException.StackTrace;

            return false;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _testRunCancellationTokenSource?.Dispose();
            _testRunCancellationTokenSource = null;
            _isDisposed = true;
        }
    }
}
