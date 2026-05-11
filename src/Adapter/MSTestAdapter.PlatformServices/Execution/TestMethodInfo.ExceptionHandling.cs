// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Handles the exception that is thrown by a test method. The exception can either
    /// be expected or not expected.
    /// </summary>
    /// <param name="ex">Exception that was thrown.</param>
    /// <param name="realException">Real exception thrown by the test method.</param>
    /// <param name="className">The class name.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>Test framework exception with details.</returns>
    private TestFailedException HandleMethodException(Exception ex, Exception realException, string className, string methodName)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        string errorMessage;
        if (ex is TargetInvocationException && ex.InnerException == null)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
            return new TestFailedException(UnitTestOutcome.Error, errorMessage);
        }

        if (ex is TestFailedException testFailedException)
        {
            return testFailedException;
        }

        // If we are in hot reload context and the exception is a MissingMethodException and the first line of the stack
        // trace contains the method name then it's likely that the current method was removed and the test is failing.
        // For cases where the content of the test would throw a MissingMethodException, the first line of the stack trace
        // would not be the test method name, so we can safely assume this is a proper test failure.
        if (ex is MissingMethodException missingMethodException
            && RuntimeContext.IsHotReloadEnabled
            && missingMethodException.StackTrace?.IndexOf(Environment.NewLine, StringComparison.Ordinal) is { } lineReturnIndex
            && lineReturnIndex >= 0
#pragma warning disable IDE0057 // Use range operator
            && missingMethodException.StackTrace.Substring(0, lineReturnIndex).Contains($"{className}.{methodName}"))
#pragma warning restore IDE0057 // Use range operator
        {
            return new TestFailedException(UnitTestOutcome.NotFound, missingMethodException.Message, missingMethodException);
        }

        // Get the real exception thrown by the test method
        if (realException.TryGetUnitTestAssertException(out UnitTestOutcome outcome, out string? exceptionMessage, out StackTraceInformation? exceptionStackTraceInfo))
        {
            return new TestFailedException(outcome, exceptionMessage, exceptionStackTraceInfo, realException);
        }

        errorMessage = _classInstance is null
            ? string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_InstanceCreationError,
                TestClassName,
                realException.GetFormattedExceptionMessage())
            : string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestMethodThrows,
                className,
                methodName,
                realException.GetFormattedExceptionMessage());

        // Handle special case of UI objects in TestMethod to suggest UITestMethod
        if (realException.HResult == -2147417842)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_WrongThread, errorMessage);
        }

        StackTraceInformation? stackTrace = null;

        // For ThreadAbortException (that can be thrown only by aborting a thread as there's no public constructor)
        // there's no inner exception and exception itself contains reflection-related stack trace
        // (_RuntimeMethodHandle.InvokeMethodFast <- _RuntimeMethodHandle.Invoke <- UnitTestExecuter.RunTestMethod)
        // which has no meaningful info for the user. Thus, we do not show call stack for ThreadAbortException.
        if (realException.GetType().Name != "ThreadAbortException")
        {
            stackTrace = realException.GetStackTraceInformation();
        }

        return new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace, realException);
    }
}
