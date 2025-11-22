// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Test method attribute that runs tests in a Task.Run with non-cooperative timeout handling.
/// </summary>
/// <remarks>
/// <para>
/// This attribute runs test methods in <see cref="Task.Run"/> and implements non-cooperative timeout handling.
/// When a timeout occurs, the test is marked as timed out and the test runner stops awaiting the task,
/// allowing it to complete in the background. This prevents blocking but may lead to dangling tasks.
/// </para>
/// <para>
/// For cooperative timeout handling where tests are awaited until completion, use <see cref="TimeoutAttribute"/>
/// with <c>CooperativeCancellation = true</c> instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TaskRunTestMethodAttribute : TestMethodAttribute
{
    private readonly TestMethodAttribute? _testMethodAttribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRunTestMethodAttribute"/> class.
    /// </summary>
    /// <param name="timeout">The timeout in milliseconds. If not specified or 0, no timeout is applied.</param>
    public TaskRunTestMethodAttribute(int timeout = 0, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRunTestMethodAttribute"/> class.
    /// This constructor is intended to be called by test class attributes to wrap an existing test method attribute.
    /// </summary>
    /// <param name="testMethodAttribute">The wrapped test method.</param>
    /// <param name="timeout">The timeout in milliseconds. If not specified or 0, no timeout is applied.</param>
    public TaskRunTestMethodAttribute(TestMethodAttribute testMethodAttribute, int timeout = 0)
        : base(testMethodAttribute.DeclaringFilePath, testMethodAttribute.DeclaringLineNumber ?? -1)
    {
        _testMethodAttribute = testMethodAttribute;
        Timeout = timeout;
    }

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }

    /// <summary>
    /// Executes a test method with non-cooperative timeout handling.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (_testMethodAttribute is not null)
        {
            return await ExecuteWithTimeoutAsync(() => _testMethodAttribute.ExecuteAsync(testMethod), testMethod).ConfigureAwait(false);
        }

        return await ExecuteWithTimeoutAsync(async () =>
        {
            TestResult result = await testMethod.InvokeAsync(null).ConfigureAwait(false);
            return new[] { result };
        }, testMethod).ConfigureAwait(false);
    }

    private async Task<TestResult[]> ExecuteWithTimeoutAsync(Func<Task<TestResult[]>> executeFunc, ITestMethod testMethod)
    {
        if (Timeout <= 0)
        {
            // No timeout, run directly with Task.Run
            return await RunOnThreadPoolOrCustomThreadAsync(executeFunc).ConfigureAwait(false);
        }

        // Run with timeout
        Task<TestResult[]> testTask = RunOnThreadPoolOrCustomThreadAsync(executeFunc);
        Task completedTask = await Task.WhenAny(testTask, Task.Delay(Timeout)).ConfigureAwait(false);

        if (completedTask == testTask)
        {
            // Test completed before timeout
            return await testTask.ConfigureAwait(false);
        }

        // Timeout occurred - return timeout result and let task continue in background
        return
        [
            new TestResult
            {
                Outcome = UnitTestOutcome.Timeout,
                TestFailureException = new TestFailedException(
                    UnitTestOutcome.Timeout,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Test '{0}.{1}' exceeded timeout of {2}ms.",
                        testMethod.TestClassName,
                        testMethod.TestMethodName,
                        Timeout)),
            },
        ];
    }

    private static Task<TestResult[]> RunOnThreadPoolOrCustomThreadAsync(Func<Task<TestResult[]>> executeFunc)
    {
        // Check if we need to handle STA threading
        // If current thread is STA and we're on Windows, create a new STA thread
        // Otherwise, use Task.Run (thread pool)
#if NETFRAMEWORK
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return RunOnSTAThreadAsync(executeFunc);
        }
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return RunOnSTAThreadAsync(executeFunc);
        }
#endif

        // Use thread pool for non-STA scenarios
        return Task.Run(executeFunc);
    }

    private static Task<TestResult[]> RunOnSTAThreadAsync(Func<Task<TestResult[]>> executeFunc)
    {
        var tcs = new TaskCompletionSource<TestResult[]>();
        var thread = new Thread(() =>
        {
            try
            {
                TestResult[] result = executeFunc().GetAwaiter().GetResult();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            Name = "TaskRunTestMethodAttribute STA thread",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
