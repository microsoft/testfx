// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

/// <summary>
/// Extension methods for the exception class.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Returns an exception message with all inner exceptions messages.
    /// </summary>
    /// <param name="exception"> The exception. </param>
    /// <returns> Custom exception message that includes inner exceptions. </returns>
    internal static string GetExceptionMessage(this Exception? exception)
    {
        if (exception == null)
        {
            return string.Empty;
        }

        string exceptionString = exception.Message;
        Exception? inner = exception.InnerException;
        while (inner != null)
        {
            exceptionString += Environment.NewLine + inner.Message;
            inner = inner.InnerException;
        }

        return exceptionString;
    }
}
#endif
