// ---------------------------------------------------------------------------
// <copyright file="UnitTestEventSource.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
// Fires the log events for event listener
// </summary>
// <owner>svajjala</owner> 
// ---------------------------------------------------------------------------
using System.Diagnostics.Tracing;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
#if !dotnet
    /// <summary>
    /// Event Source trigger for log trace events
    /// </summary>
    internal sealed class UnitTestEventSource : EventSource
    {
        public static UnitTestEventSource Log = new UnitTestEventSource();

        [Event(1, Level = EventLevel.Verbose)]
        public void Debug(string message)
        {
            this.WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Informational)]
        public void Info(string message)
        {
            this.WriteEvent(2, message);
        }

        [Event(3, Level = EventLevel.Warning)]
        public void Warn(string message)
        {
            this.WriteEvent(3, message);
        }

        [Event(4, Level = EventLevel.Error)]
        public void Error(string message)
        {
            this.WriteEvent(4, message);
        }

        [Event(5, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            this.WriteEvent(5, message);
        }
    }
#endif
}
