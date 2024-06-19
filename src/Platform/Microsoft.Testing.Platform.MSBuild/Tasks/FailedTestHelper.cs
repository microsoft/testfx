// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions.Serializers;

namespace Microsoft.Testing.Platform.MSBuild;

internal static class FailedTestHelper
{
    internal static void FromFailedTest(this FailedTestInfoRequest failedTestInfoRequest, bool outputSupportsMultiline,
        string targetPath,
        out string errorCode, out string file, out int lineNumber, out string message, out string? lowPriorityMessage)
    {
        errorCode = failedTestInfoRequest.IsCancelled ? "test cancelled" : "test failed";
        if (StackTraceHelper.TryFindLocationFromStackFrame(failedTestInfoRequest.ErrorStackTrace, out string? filePath, out lineNumber, out string? place))
        {
        }
        else if (!string.IsNullOrEmpty(failedTestInfoRequest.CodeFilePath))
        {
            // If there is no frame with location, but we collect source info, use the source info.
            filePath = failedTestInfoRequest.CodeFilePath;
            lineNumber = failedTestInfoRequest.LineNumber;
        }
        else
        {
            // Use the produced dll.
            filePath = targetPath;
        }

        // MSBuild Log needs this to be non-null.
        file = filePath ?? string.Empty;

        // When multiline is supported, the output will go to screen, and we need to localize it.
        // Otherwise it goes to binary log, and that is a "log" and it is better to be in English.
        CultureInfo culture = outputSupportsMultiline ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
        StringBuilder errorMessage = new();
        errorMessage.Append(failedTestInfoRequest.DisplayName);
        if (failedTestInfoRequest.Duration is not null)
        {
            errorMessage.Append(" (");
            errorMessage.Append(failedTestInfoRequest.Duration);
            errorMessage.Append(')');
        }

        errorMessage.Append(": ");
        errorMessage.AppendLine(failedTestInfoRequest.ErrorMessage);

        if (!string.IsNullOrEmpty(failedTestInfoRequest.Expected))
        {
            errorMessage.AppendLine(string.Format(culture, Resources.MSBuildResources.ExpectedValue, failedTestInfoRequest.Expected));
            errorMessage.AppendLine(string.Format(culture, Resources.MSBuildResources.ActualValue, failedTestInfoRequest.Actual));
        }

        if (!string.IsNullOrEmpty(failedTestInfoRequest.ErrorStackTrace))
        {
            errorMessage.AppendLine(string.Format(culture, Resources.MSBuildResources.StackTrace));
            errorMessage.AppendLine(failedTestInfoRequest.ErrorStackTrace);
        }

        if (outputSupportsMultiline)
        {
            // We put all the info to screen via the (high priority) error message, there is no additional info to put into binlog.
            lowPriorityMessage = null;

            message = errorMessage.ToString();
        }
        else
        {
            lowPriorityMessage = errorMessage.ToString();

            string nameAndPlace = place == null
                ? $"{failedTestInfoRequest.DisplayName} ({failedTestInfoRequest.Duration})"
                    : $"{failedTestInfoRequest.DisplayName} ({failedTestInfoRequest.Duration}): {place}";
            string? singleLineError = JoinSingleLineAndShorten(nameAndPlace, failedTestInfoRequest.ErrorMessage);

            message = singleLineError!;
        }
    }

    private static string? JoinSingleLineAndShorten(string? first, string? second)
        => first != null && second != null
            ? SingleLineAndShorten(first) + " " + SingleLineAndShorten(second)
            : SingleLineAndShorten(first) ?? SingleLineAndShorten(second);

    private static string? SingleLineAndShorten(string? text)
#pragma warning disable IDE0057 // Use range operator
        => text == null ? null : (text.Length <= 1000 ? text : text.Substring(0, 1000)).Replace('\r', ' ').Replace('\n', ' ');
#pragma warning restore IDE0057 // Use range operator
}
