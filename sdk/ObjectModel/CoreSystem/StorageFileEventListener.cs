// ---------------------------------------------------------------------------
// <copyright file="StorageFileEventListener.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
// Writes the log traces to a log file inside app storage
// </summary>
// <owner>svajjala</owner> 
// ---------------------------------------------------------------------------

#if !dotnet
using Windows.Storage;


namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;


    internal sealed class StorageFileEventListener : EventListener
    {
        /// <summary> 
        /// Storage file to be used to write logs 
        /// </summary> 
        private StorageFile m_StorageFile = null;

        /// <summary> 
        /// Name of the current event listener 
        /// </summary> 
        private string m_Name;

        /// <summary> 
        /// The format to be used by logging. 
        /// </summary> 
        private string m_Format = "{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}\tType: {1}\tId: {2}\tMessage: '{3}'";

        private SemaphoreSlim m_SemaphoreSlim = new SemaphoreSlim(1);

        public StorageFileEventListener(string name)
        {
            this.m_Name = name;
            AssignLocalFile();
        }

        /// <summary>
        /// Create a local storage file for writing log
        /// </summary>
        private async void AssignLocalFile()
        {
            m_StorageFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(m_Name.Replace(" ", "_") + ".TpTrace.log",
                                                                                      CreationCollisionOption.OpenIfExists);
        }

        /// <summary>
        /// Write the lines to the log
        /// </summary>
        /// <param name="lines"></param>
        private async void WriteToFile(IEnumerable<string> lines)
        {
            await m_SemaphoreSlim.WaitAsync();

            await Task.Run(async () =>
            {
                try
                {
                    await FileIO.AppendLinesAsync(m_StorageFile, lines);
                }
                catch (Exception)
                {
                    // ignore
                }
                finally
                {
                    m_SemaphoreSlim.Release();
                }
            });
        }

        /// <summary>
        /// Fired when the event is triggered
        /// </summary>
        /// <param name="eventData"></param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (m_StorageFile == null) return;

            var lines = new List<string>();

            var newFormatedLine = string.Format(m_Format, DateTime.Now, eventData.Level, eventData.EventId, eventData.Payload[0]);

            lines.Add(newFormatedLine);

            WriteToFile(lines);
        }

        /// <summary>
        /// Just a way to find if event source registration succeeded
        /// </summary>
        /// <param name="eventSource"></param>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // To verify creation during debugging
        }
    }

}
#endif
