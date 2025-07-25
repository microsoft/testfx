// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1716 // Do not use reserved keywords
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#pragma warning restore CA1716 // Do not use reserved keywords

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
internal interface IAdapterTraceLogger
{
    /// <summary>
    /// Gets a value indicating whether informational logging is enabled.
    /// </summary>
    bool IsInfoEnabled { get; }

    /// <summary>
    /// Logs an error message formatted with the specified arguments.
    /// </summary>
    /// <remarks>This method formats the error message using the specified format string and arguments, and
    /// logs it as an error. Ensure that the format string and arguments are correctly aligned to avoid format
    /// exceptions.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with format items, which correspond to objects in the
    /// <paramref name="args"/> array.</param>
    /// <param name="args">An array of objects to format. These objects are formatted using the format items in the <paramref
    /// name="format"/> string.</param>
    void LogError(string format, params object?[] args);

    /// <summary>
    /// Logs a warning message with a specified format and arguments.
    /// </summary>
    /// <remarks>This method formats the warning message using the specified format string and arguments, and
    /// logs it at the warning level.  Ensure that the format string and arguments are correctly aligned to avoid format
    /// exceptions.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with format items, which correspond to objects in the
    /// <paramref name="args"/> array.</param>
    /// <param name="args">An array of objects to format. Each object in the array is formatted according to the corresponding format item
    /// in the <paramref name="format"/> string.</param>
    void LogWarning(string format, params object?[] args);

    /// <summary>
    /// Logs an informational message with a specified format and arguments.
    /// </summary>
    /// <remarks>This method formats the message using the specified format string and arguments, and logs it
    /// at the informational level. Ensure that the format string and arguments are correctly aligned to avoid format
    /// exceptions.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with zero or more format items.</param>
    /// <param name="args">An array of objects to format. Each format item in the format string is replaced by the string representation of
    /// the corresponding object in this array.</param>
    void LogInfo(string format, params object?[] args);

    /// <summary>
    /// Logs a verbose message with the specified format and arguments.
    /// </summary>
    /// <remarks>This method is intended for detailed logging that may be useful for debugging or tracing the
    /// execution of the application.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with zero or more format items.</param>
    /// <param name="args">An array of objects to format. Each format item in <paramref name="format"/> is replaced by the string
    /// representation of the corresponding object in <paramref name="args"/>.</param>
    void LogVerbose(string format, params object?[] args);
}
