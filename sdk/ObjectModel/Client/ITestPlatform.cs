// ---------------------------------------------------------------------------
// <copyright file="ITestPlatform.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    public interface ITestPlatform : IDisposable
    {
        /// <summary>
        /// Initialize the test platform with the path to additional unit test extensions. 
        /// 
        /// If no additional extension is available, then specify null or empty list. 
        /// </summary>
        /// <param name="additionalUnitTestExtensions">Specifies the path to unit test extensions.</param>
        /// <param name="loadOnlyWellKnownExtensions">Specifies whether only well known extensions should be loaded.</param>
        /// <param name="forceX86Discoverer">Forces test discovery in x86 Discoverer process.</param>
        void Initialize(IEnumerable<string> pathToAdditionalExtensions, bool loadOnlyWellKnownExtensions, bool forceX86Discoverer);

        /// <summary>
        /// Update the extensions to be used by the test service
        /// </summary>
        /// <param name="pathToAdditionalExtensions">
        /// Specifies the path to unit test extensions. 
        /// If no additional extension is available, then specify null or empty list.
        /// </param>
        /// <param name="loadOnlyWellKnownExtensions">Specifies whether only well known extensions should be loaded.</param>
        void UpdateExtensions(IEnumerable<string> pathToAdditionalExtensions, bool loadOnlyWellKnownExtensions);

        /// <summary>
        /// Creates a discovery request
        /// </summary>
        /// <param name="discoveryCriteria">Specifies the discovery parameters</param>
        /// <returns></returns>
        IDiscoveryRequest CreateDiscoveryRequest(DiscoveryCriteria discoveryCriteria);

        /// <summary>
        /// Creates a test run request.
        /// </summary>
        /// <param name="testRunCriteria">Specifies the test run criteria</param>
        /// <returns></returns>
        ITestRunRequest CreateTestRunRequest(TestRunCriteria testRunCriteria);

        /// <summary>
        /// Create a multi-test run request
        /// </summary>
        /// <param name="baseTestRunCriteria">Specifies the base test run criteria</param>
        IMultipleTestRunRequest CreateMultipleTestRunRequest(BaseTestRunCriteria baseTestRunCriteria);

        /// <summary>
        /// Starts preparing for the first test run with the parameter settings. 
        /// </summary>
        /// <param name="testRunSettings">Specifies the settings with which framework should initialize.</param>
        /// <param name="testExecutorLauncher">Custom test executor launcher. If null then default will be used.</param>
        IAsyncResult StartPreparingForFirstTestRunRequest(string testRunSettings, ITestExecutorLauncher testExecutorLauncher);
    }
}
