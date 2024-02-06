// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class MethodRunner
{
    internal static TestFailedException? RunWithTimeoutAndCancellation(
        Action action, CancellationTokenSource cancellationTokenSource, int? timeout, MethodInfo methodInfo,
        string methodCancelledMessageFormat, string methodTimedOutMessageFormat)
    {
        try
        {
            var executionTask = Task.Run(action, cancellationTokenSource.Token);

            if (!timeout.HasValue)
            {
                executionTask.Wait(cancellationTokenSource.Token);
                return cancellationTokenSource.IsCancellationRequested
                    ? new(
                        UnitTestOutcome.Timeout,
                        string.Format(CultureInfo.InvariantCulture, methodCancelledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name))
                    : null;
            }

            if (executionTask.Wait(timeout.Value, cancellationTokenSource.Token))
            {
                return null;
            }

            // Timed out or cancelled.
            return new(
                UnitTestOutcome.Timeout,
                string.Format(
                    CultureInfo.InvariantCulture,
                    cancellationTokenSource.IsCancellationRequested ? methodCancelledMessageFormat : methodTimedOutMessageFormat,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name));
        }
        catch (OperationCanceledException)
        {
            return new(
                UnitTestOutcome.Timeout,
                string.Format(CultureInfo.InvariantCulture, methodCancelledMessageFormat, methodInfo.DeclaringType!.FullName, methodInfo.Name));
        }
    }
}
