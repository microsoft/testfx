using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Defines the discovery criterion
    /// </summary>
    public class DiscoveryCriteria
    {
        /// <summary>
        /// Criteria used for test discovery
        /// </summary>
        /// <param name="sources">Sources from which the tests should be discovered</param>
        /// <param name="frequencyOfDiscoveredTestsEvent">Frequency of discovered test event</param>
        /// <param name="runSettings">Run Settings for the discovery.</param>
        public DiscoveryCriteria(IEnumerable<string> sources, long frequencyOfDiscoveredTestsEvent, string testSettings)
            : this(sources, frequencyOfDiscoveredTestsEvent, TimeSpan.MaxValue, testSettings)
        {
        }

        /// <summary>
        /// Criteria used for test discovery
        /// </summary>
        /// <param name="sources">Sources from which the tests should be discovered</param>
        /// <param name="frequencyOfDiscoveredTestsEvent">Frequency of discovered test event</param>
        /// <param name="discoveredTestEventTimeout">Timeout that triggers the discovered test event regardless of cache size.</param>
        /// <param name="runSettings">Run Settings for the discovery.</param>
        public DiscoveryCriteria(IEnumerable<string> sources, long frequencyOfDiscoveredTestsEvent, TimeSpan discoveredTestEventTimeout, string runSettings)
        {
            ValidateArg.NotNullOrEmpty(sources, "sources");
            if (frequencyOfDiscoveredTestsEvent <= 0) throw new ArgumentOutOfRangeException("frequencyOfDiscoveredTestsEvent", Resources.NotificationFrequencyIsNotPositive);
            if (discoveredTestEventTimeout <= TimeSpan.MinValue) throw new ArgumentOutOfRangeException("discoveredTestEventTimeout", Resources.NotificationTimeoutIsZero);

            Sources = sources;
            FrequencyOfDiscoveredTestsEvent = frequencyOfDiscoveredTestsEvent;
            DiscoveredTestEventTimeout = discoveredTestEventTimeout;

            RunSettings = runSettings;            
        }

        /// <summary>
        /// Test Containers (e.g. DLL/EXE/artifacts to scan)
        /// </summary>
        public IEnumerable<string> Sources { get; private set; }

        /// <summary>
        /// Defines the frequency of discovered test event. 
        /// </summary>
        /// <remarks>
        /// Discovered test event will be raised after discovering these number of tests. 
        /// 
        /// Note that this event is raised asynchronously and the underlying discovery process is not 
        /// paused during the listener invocation. So if the event handler, you try to query the 
        /// next set of tests, you may get more than 'FrequencyOfDiscoveredTestsEvent'.
        /// </remarks>        
        public long FrequencyOfDiscoveredTestsEvent { get; private set; }

        /// <summary>
        /// Timeout that triggers the discovered test event regardless of cache size.
        /// </summary>
        public TimeSpan DiscoveredTestEventTimeout { get; private set; }

        /// <summary>
        /// Settings used for the discovery request. 
        /// </summary>
        public string RunSettings { get; private set; }
    }
}
