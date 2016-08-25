// ---------------------------------------------------------------------------
// <copyright file="IFrameworkHandle.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Handle to the framework which is passed to the test executors.
// </summary>
// <owner>aseemb</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    public interface IFrameworkHandle : ITestExecutionRecorder
    {
        /// <summary>
        /// Give a hint to the execution framework to enable the shutdown of execution process after the test run is complete. This should be used only in out of process test runs when IRunContext.KeepAlive is true 
        /// and should be used only when absolutely required as using it degrades the performance of the subsequent run. 
        ///
        /// It throws InvalidOperationException when it is attempted to be enabled when keepAlive is false. 
        /// </summary

        bool EnableShutdownAfterTestRun { get; set; }

        /// <summary>
        /// Launch the specified process with the debugger attached.
        /// </summary>
        /// <param name="filePath">File path to the exe to launch.</param>
        /// <param name="workingDirectory">Working directory that process should use.</param>
        /// <param name="arguments">Command line arguments the process should be launched with.</param>
        /// <param name="environmentVariables">Environment variables to be set in target process</param>
        /// <returns>Process ID of the started process.</returns>
        int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables);
    }
}