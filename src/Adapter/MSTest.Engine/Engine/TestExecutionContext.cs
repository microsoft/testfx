// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework;

internal sealed class TestExecutionContext : ITestExecutionContext
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly TestNodeUpdateMessage? _testNodeUpdateMessage;

    private readonly ITrxReportCapability? _trxReportCapability;
    private readonly CancellationToken _originalCancellationToken;

    public TestExecutionContext(IConfiguration configuration, TestNode testNode, TestNodeUpdateMessage? testNodeUpdateMessage,
        ITrxReportCapability? trxReportCapability, CancellationToken cancellationToken)
    {
        Configuration = configuration;
        _testNodeUpdateMessage = testNodeUpdateMessage;
        _trxReportCapability = trxReportCapability;
        TestInfo = new TestInfo(testNode);
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _originalCancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public IConfiguration Configuration { get; }

    public ITestInfo TestInfo { get; }

    public void CancelTestExecution()
        => _cancellationTokenSource.Cancel();

    public void CancelTestExecution(int millisecondsDelay)
        => _cancellationTokenSource.CancelAfter(millisecondsDelay);

    public void CancelTestExecution(TimeSpan delay)
        => _cancellationTokenSource.CancelAfter(delay);

    public void ReportException(Exception exception, CancellationToken? timeoutCancellationToken = null)
    {
        if (_trxReportCapability is not null && _trxReportCapability.IsSupported && _testNodeUpdateMessage is not null)
        {
            AddTrxExceptionInformation(_testNodeUpdateMessage.Properties, exception);
        }

        TestNodeStateProperty executionState = exception switch
        {
            // We want to consider user timeouts as failures if they didn't use our cancellation token
            OperationCanceledException canceledException
                when canceledException.CancellationToken == _originalCancellationToken || canceledException.CancellationToken == CancellationToken
                => new CancelledTestNodeStateProperty(ExceptionFlattener.FlattenOrUnwrap(exception)),
            OperationCanceledException canceledException when canceledException.CancellationToken == timeoutCancellationToken
                => new TimeoutTestNodeStateProperty(ExceptionFlattener.FlattenOrUnwrap(exception)),
            AssertFailedException => new FailedTestNodeStateProperty(ExceptionFlattener.FlattenOrUnwrap(exception), exception.Message),

            // TODO: Filter exceptions that are to be considered as failures and return ErrorReason for the others
            _ => new ErrorTestNodeStateProperty(exception),
        };

        // TODO: We need to be able to modify the execution state of a test node
        if (_testNodeUpdateMessage is not null && !_testNodeUpdateMessage.Properties.Any<TestNodeStateProperty>())
        {
            _testNodeUpdateMessage.Properties.Add(executionState);
        }
    }

    private static void AddTrxExceptionInformation(PropertyBag propertyBag, Exception? exception)
    {
        Exception? flatException = exception != null
            ? ExceptionFlattener.FlattenOrUnwrap(exception)
            : null;
        if (flatException is null)
        {
            return;
        }

        propertyBag.Add(new TrxExceptionProperty(StringifyMessage(flatException), StringifyStackTrace(flatException)));

        static string StringifyMessage(Exception exception)
        {
            string message = exception.Message;
            if (exception.Data["assert.expected"] is string expected)
            {
                message += $"{Environment.NewLine}Expected:{Environment.NewLine}{expected}";
            }

            if (exception.Data["assert.actual"] is string actual)
            {
                message += $"{Environment.NewLine}Actual:{Environment.NewLine}{actual}";
            }

            return message;
        }

        static string StringifyStackTrace(Exception exception)
        {
            if (exception is not AggregateException aggregateException)
            {
                return exception.StackTrace ?? string.Empty;
            }

            string separator = "---End of inner exception ---";
            StringBuilder builder = new();
            foreach (Exception ex in aggregateException.InnerExceptions)
            {
                builder.AppendLine(ex.StackTrace);
                builder.AppendLine(separator);
            }

            return builder.ToString();
        }
    }
}
