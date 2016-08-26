// Copyright (c) Microsoft. All rights reserved.

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
        /// Stack Trace associated with the test failure
        /// </summary>
        public string ErrorStackTrace { get; private set; }

        /// <summary>
        /// Source code FilePath where the error occured
        /// </summary>
        public string ErrorFilePath { get; private set; }

        /// <summary>
        /// Line number in the source code file where the error occured.
        /// </summary>
        public int ErrorLineNumber { get; private set; }

        /// <summary>
        /// Column number in the source code file where the error occured.
        /// </summary>
        public int ErrorColumnNumber { get; private set; }
    }
}
