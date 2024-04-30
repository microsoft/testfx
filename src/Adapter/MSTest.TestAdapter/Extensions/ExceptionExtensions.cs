// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Extensions to <see cref="Exception"/> type.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// TargetInvocationException and TypeInitializationException do not carry any useful information
    /// to the user. Find the first inner exception that has useful information.
    /// </summary>
    internal static Exception GetRealException(this Exception exception)
    {
        // TargetInvocationException: Because .NET Framework wraps method.Invoke() into TargetInvocationException.
        // TypeInitializationException: Because AssemblyInitialize is static, and often helpers that are also static
        // are used to implement it, and they fail in constructor.
        while (exception is TargetInvocationException or TypeInitializationException
            && exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        return exception;
    }

    /// <summary>
    /// Get the exception message if available, empty otherwise.
    /// </summary>
    /// <param name="exception">An <see cref="Exception"/> object.</param>
    /// <returns>Exception message.</returns>
    internal static string TryGetMessage(this Exception exception)
    {
        if (exception == null)
        {
            return string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, "null");
        }

        // It is safe to retrieve an exception message, it should not throw in any case.
        return exception.Message ?? string.Empty;
    }

    /// <summary>
    /// Gets the <see cref="StackTraceInformation"/> for an exception.
    /// </summary>
    /// <param name="exception">An <see cref="Exception"/> instance.</param>
    /// <returns>StackTraceInformation for the exception.</returns>
    internal static StackTraceInformation? TryGetStackTraceInformation(this Exception exception) => !StringEx.IsNullOrEmpty(exception?.StackTrace)
            ? ExceptionHelper.CreateStackTraceInformation(exception, false, exception.StackTrace)
            : null;

    /// <summary>
    /// Checks whether exception is an Assert exception.
    /// </summary>
    /// <param name="exception">An <see cref="Exception"/> instance.</param>
    /// <param name="outcome"> Framework's Outcome depending on type of assertion.</param>
    /// <param name="exceptionMessage">Exception message.</param>
    /// <param name="exceptionStackTrace">StackTraceInformation for the exception.</param>
    /// <returns>True, if Assert exception. False, otherwise.</returns>
    internal static bool TryGetUnitTestAssertException(this Exception exception, out UTF.UnitTestOutcome outcome,
        [NotNullWhen(true)] out string? exceptionMessage, out StackTraceInformation? exceptionStackTrace)
    {
        if (exception is UTF.UnitTestAssertException)
        {
            outcome = exception is UTF.AssertInconclusiveException
                ? UTF.UnitTestOutcome.Inconclusive
                : UTF.UnitTestOutcome.Failed;

            exceptionMessage = exception.TryGetMessage();
            exceptionStackTrace = exception.TryGetStackTraceInformation();
            return true;
        }

        outcome = UTF.UnitTestOutcome.Failed;
        exceptionMessage = null;
        exceptionStackTrace = null;
        return false;
    }
}
