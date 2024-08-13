// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides helper functionality for the unit test framework.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
internal static class UtfHelper
{
    /// <summary>
    /// Gets the exception messages, including the messages for all inner exceptions
    /// recursively.
    /// </summary>
    /// <param name="ex">Exception to get messages for.</param>
    /// <returns>string with error message information.</returns>
    internal static string GetExceptionMsg(Exception ex)
    {
        DebugEx.Assert(ex != null, "exception is null");

        StringBuilder result = new();
        bool first = true;
        for (Exception? curException = ex;
             curException != null;
             curException = curException.InnerException)
        {
            // Get the exception message. Need to check for errors because the Message property
            // may have been overridden by the exception type in user code.
            string msg;
            Type type = curException.GetType();
            try
            {
                msg = curException.Message;
            }
            catch
            {
                msg = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.UTF_FailedToGetExceptionMessage,
                    type);
            }

            if (first)
            {
                result.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "{0}: {1}",
                    type,
                    msg);
            }
            else
            {
                result.AppendFormat(
                    CultureInfo.CurrentCulture,
                    " ---> {0}: {1}",
                    type,
                    msg);
            }

            first = false;
        }

        return result.ToString();
    }
}
