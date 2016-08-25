// ---------------------------------------------------------------------------
// <copyright file="SuspendCodeCoverage.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Utility classes/methods required for code coverage functionality in the adapters 
// </summary>
// <owner>anugupta</owner> 
// ---------------------------------------------------------------------------
// This file is a DUPLICATE of ~\alm\qtools_core\Common\Utility\SuspendCodeCoverage.cs (difference being that here we set env var - TRUE instead of proc id)

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Suspends the instrumentation (for code coverage) of the modules which are loaded
    /// during this object is created and disposed
    /// exceeded.
    /// </summary>
    public class SuspendCodeCoverage : IDisposable
    {
        /// <summary>
        /// Whether the object is disposed or not.
        /// </summary>
        private bool m_disposed = false;

        /// <summary>
        /// Constructor. Code Coverage instrumentation of the modules, which are loaded
        /// during this object is created and disposed, is disabled.
        /// </summary>
        public SuspendCodeCoverage()
        {
            prevEnvValue = Environment.GetEnvironmentVariable(SuspendCodeCoverageEnvVarName, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(SuspendCodeCoverageEnvVarName, SuspendCodeCoverageEnvVarTrueValue, EnvironmentVariableTarget.Process);
        }

        #region IDisposable

        /// <summary>
        /// Disposes this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    Environment.SetEnvironmentVariable(SuspendCodeCoverageEnvVarName, prevEnvValue, EnvironmentVariableTarget.Process);
                }

                m_disposed = true;
            }
        }

        #endregion IDisposable

        public const string SuspendCodeCoverageEnvVarName = "__VANGUARD_SUSPEND_INSTRUMENT__";
        public const string SuspendCodeCoverageEnvVarTrueValue = "TRUE";

        #region private members
        private string prevEnvValue;
        #endregion
    }
}