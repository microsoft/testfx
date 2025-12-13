// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// TestResult object to be returned to adapter.
/// </summary>
[Serializable]
public class TestResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestResult"/> class.
    /// </summary>
    public TestResult() => DatarowIndex = -1;

    /// <summary>
    /// Gets or sets the display name of the result. Useful when returning multiple results.
    /// If null then Method name is used as DisplayName.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the test execution.
    /// </summary>
    public UnitTestOutcome Outcome { get; set; }

    internal string? IgnoreReason { get; set; }

    // NOTE: As TestResult can cross appdomain boundary, the exception should generally be serializable.
    // But that's not always the case and we can't see good guarantees.
    // Alternatively, we set ExceptionMessage and ExceptionStackTrace, and serialize those instead of the exception.
    // That means, after crossing app domain, you shouldn't access TestFailureException.

    /// <summary>
    /// Gets or sets the exception thrown when test is failed.
    /// </summary>
    [field: NonSerialized]
    public Exception? TestFailureException
    {
        get
        {
            if ((ExceptionMessage is not null || ExceptionStackTrace is not null) && field is null)
            {
                // That means this property is accessed after crossing appdomain boundary.
                // So, we fail.
                throw new InvalidOperationException();
            }

            return field;
        }

        set
        {
            if (value is null)
            {
                // If the field is already null, we don't need to do anything.
                // If the field is non-null, it means we are trying to clear an exception, which is something we shouldn't do.
                // If it happened that we attempted to set it to null after it was non-null, we return and do
                // nothing. This is better than potentially masking real failures silently.
                Debug.Assert(field is null, "TestFailureException should not be set to null after it was non-null");
                return;
            }

            field = field is null
                ? value
                : field is AggregateException aggregateException
                    ? new AggregateException(aggregateException.InnerExceptions.Concat([value]))
                    : new AggregateException(field, value);

            ExceptionMessage = field.Message;
            ExceptionStackTrace = field.StackTrace;

            if (field.Data.Contains("assert.actual"))
            {
                ExceptionAssertActual = field.Data["assert.actual"]?.ToString();
            }

            if (field.Data.Contains("assert.expected"))
            {
                ExceptionAssertExpected = field.Data["assert.expected"]?.ToString();
            }
        }
    }

    internal string? ExceptionMessage { get; set; }

    internal string? ExceptionStackTrace { get; set; }

    internal string? ExceptionAssertActual { get; set; }

    internal string? ExceptionAssertExpected { get; set; }

    /// <summary>
    /// Gets or sets the output of the message logged by test code.
    /// </summary>
    public string? LogOutput { get; set; }

    /// <summary>
    /// Gets or sets the output of the message logged by test code.
    /// </summary>
    public string? LogError { get; set; }

    /// <summary>
    /// Gets or sets the debug traces by test code.
    /// </summary>
    public string? DebugTrace { get; set; }

    /// <summary>
    /// Gets or sets the debug traces by test code.
    /// </summary>
    public string? TestContextMessages { get; set; }

    /// <summary>
    /// Gets or sets the execution id of the result.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the parent execution id of the result.
    /// </summary>
    public Guid ParentExecId { get; set; }

    /// <summary>
    /// Gets or sets the inner results count of the result.
    /// </summary>
    public int InnerResultsCount { get; set; }

    /// <summary>
    /// Gets or sets the duration of test execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the data row index in data source. Set only for results of individual
    /// run of data row of a data driven test.
    /// </summary>
    public int DatarowIndex { get; set; }

    /// <summary>
    /// Gets or sets the return value of the test method. (Currently null always).
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// Gets or sets the result files attached by the test.
    /// </summary>
    public IList<string>? ResultFiles { get; set; }

    internal static TestResult CreateIgnoredResult(string? ignoreReason)
        => new()
        {
            Outcome = UnitTestOutcome.Ignored,
            IgnoreReason = ignoreReason,
        };
}
