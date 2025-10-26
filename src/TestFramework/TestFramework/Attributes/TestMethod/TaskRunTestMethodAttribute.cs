// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The TaskRun test method attribute.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is designed to handle test method execution with timeout by running the test code within a <see cref="Task.Run"/>.
/// This allows the test runner to stop watching the task in case of timeout, preventing dangling tasks that can lead to
/// confusion or errors because the test method is still running in the background.
/// </para>
/// <para>
/// When a timeout occurs:
/// <list type="bullet">
/// <item><description>The test is marked as timed out.</description></item>
/// <item><description>The cancellation token from <see cref="TestContext.CancellationToken"/> is canceled.</description></item>
/// <item><description>The test runner stops awaiting the test task, allowing it to complete in the background.</description></item>
/// </list>
/// </para>
/// <para>
/// For best results, test methods should observe the cancellation token and cancel cooperatively.
/// If the test method does not handle cancellation properly, the task may continue running after the timeout,
/// which can still lead to issues, but the test runner will not block waiting for it to complete.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class TaskRunTestMethodAttribute : TestMethodAttribute
{
    private readonly TestMethodAttribute? _testMethodAttribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRunTestMethodAttribute"/> class.
    /// </summary>
    public TaskRunTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRunTestMethodAttribute"/> class.
    /// This constructor is intended to be called by test class attributes to wrap an existing test method attribute.
    /// </summary>
    /// <param name="testMethodAttribute">The wrapped test method.</param>
    public TaskRunTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        : base(testMethodAttribute.DeclaringFilePath, testMethodAttribute.DeclaringLineNumber ?? -1)
        => _testMethodAttribute = testMethodAttribute;

    /// <summary>
    /// Executes a test method by wrapping it in a <see cref="Task.Run"/> to allow timeout handling.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (_testMethodAttribute is not null)
        {
            return await ExecuteWithTaskRunAsync(() => _testMethodAttribute.ExecuteAsync(testMethod)).ConfigureAwait(false);
        }

        return await ExecuteWithTaskRunAsync(() => testMethod.InvokeAsync(null)).ConfigureAwait(false);
    }

    private static async Task<TestResult[]> ExecuteWithTaskRunAsync(Func<Task<TestResult>> executeFunc)
    {
        // Run the test method in Task.Run so that we can stop awaiting it on timeout
        // while allowing it to complete in the background
        Task<TestResult> testTask = Task.Run(executeFunc);

        TestResult result = await testTask.ConfigureAwait(false);
        return [result];
    }

    private static async Task<TestResult[]> ExecuteWithTaskRunAsync(Func<Task<TestResult[]>> executeFunc)
    {
        // Run the test method in Task.Run so that we can stop awaiting it on timeout
        // while allowing it to complete in the background
        Task<TestResult[]> testTask = Task.Run(executeFunc);

        TestResult[] results = await testTask.ConfigureAwait(false);
        return results;
    }
}
