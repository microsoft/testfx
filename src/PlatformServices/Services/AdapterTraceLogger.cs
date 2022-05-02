// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// The trace logger for .Net Core.
    /// </summary>
    public class AdapterTraceLogger : IAdapterTraceLogger
    {
        /// <summary>
        /// Log an error in a given format.
        /// </summary>
        /// <param name="format"> The format. </param>
        /// <param name="args"> The args. </param>
        public void LogError(string format, params object[] args)
        {
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, format, args);
        }

        /// <summary>
        /// Log a warning in a given format.
        /// </summary>
        /// <param name="format"> The format. </param>
        /// <param name="args"> The args. </param>
        public void LogWarning(string format, params object[] args)
        {
            EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, this.PrependAdapterName(format), args);
        }

        /// <summary>
        /// Log an information message in a given format.
        /// </summary>
        /// <param name="format"> The format. </param>
        /// <param name="args"> The args. </param>
        public void LogInfo(string format, params object[] args)
        {
            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, format, args);
        }

        private string PrependAdapterName(string format)
        {
            return $"MSTest - {format}";
        }
    }
}
