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
            field = value;
            ExceptionMessage = value?.Message;
            ExceptionStackTrace = value?.StackTrace;
        }
    }

    internal string? ExceptionMessage { get; set; }

    internal string? ExceptionStackTrace { get; set; }

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
