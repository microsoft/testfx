// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using System.Diagnostics;

    [Serializable]
    internal class StackTraceInformation
    {
        public StackTraceInformation(string stackTrace)
            : this(stackTrace, null, 0, 0)
        {
        }

        public StackTraceInformation(string stackTrace, string filePath, int lineNumber, int columnNumber)
        {
            Debug.Assert(!string.IsNullOrEmpty(stackTrace), "StackTrace message should not be empty");
            Debug.Assert(lineNumber >= 0, "Line number should be greater than or equal to 0");
            Debug.Assert(columnNumber >= 0, "Column number should be greater than or equal to 0");

            this.ErrorStackTrace = stackTrace;
            this.ErrorFilePath = filePath;
            this.ErrorLineNumber = lineNumber;
            this.ErrorColumnNumber = columnNumber;
        }

        /// <summary>
        /// Gets stack Trace associated with the test failure
        /// </summary>
        public string ErrorStackTrace { get; private set; }

        /// <summary>
        /// Gets source code FilePath where the error occurred
        /// </summary>
        public string ErrorFilePath { get; private set; }

        /// <summary>
        /// Gets line number in the source code file where the error occurred.
        /// </summary>
        public int ErrorLineNumber { get; private set; }

        /// <summary>
        /// Gets column number in the source code file where the error occurred.
        /// </summary>
        public int ErrorColumnNumber { get; private set; }
    }
}
