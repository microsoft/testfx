// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

    /// <summary>
    /// Contains the execution logic for this adapter.
    /// </summary>
    [ExtensionUri(TestAdapter.Constants.ExecutorUriString)]
    public class MSTestExecutor : ITestExecutor
    {
        /// <summary>
        /// Token for cancelling the test run.
        /// </summary>
        private TestRunCancellationToken cancellationToken = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestExecutor"/> class.
        /// </summary>
        public MSTestExecutor()
        {
            this.TestExecutionManager = new TestExecutionManager();
            this.MSTestDiscoverer = new MSTestDiscoverer();
        }
        
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");
            ValidateArg.NotNullOrEmpty(tests, "tests");

            if (!this.MSTestDiscoverer.AreValidSources(from test in tests select test.Source))
            {
                throw new NotSupportedException();
            }

            // Populate the runsettings.
            MSTestSettings.PopulateSettings(runContext);

            // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
            if (MSTestSettings.IsLegacyScenario(frameworkHandle))
                return;

            this.cancellationToken = new TestRunCancellationToken();
            this.TestExecutionManager.RunTests(tests, runContext, frameworkHandle, this.cancellationToken);
            this.cancellationToken = null;
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");
            ValidateArg.NotNullOrEmpty(sources, "sources");

            if (!this.MSTestDiscoverer.AreValidSources(sources))
            {
                throw new NotSupportedException();
            }

            // Populate the runsettings.
            MSTestSettings.PopulateSettings(runContext);

            // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
            if (MSTestSettings.IsLegacyScenario(frameworkHandle))
                return;

            sources = PlatformServiceProvider.Instance.TestSource.GetTestSources(sources);
            this.cancellationToken = new TestRunCancellationToken();
            this.TestExecutionManager.RunTests(sources, runContext, frameworkHandle, this.cancellationToken);

            this.cancellationToken = null;
        }

        public void Cancel()
        {
            this.cancellationToken?.Cancel();
        }

        /// <summary>
        /// Gets or sets the ms test execution manager.
        /// </summary>
        public TestExecutionManager TestExecutionManager{ get; protected set; }

        /// <summary>
        /// Discoverer used for validating the sources.
        /// </summary>
        private MSTestDiscoverer MSTestDiscoverer { get; }
    }
    
    /// <summary>
    /// Cancellation token supporting cancellation of a test run.
    /// </summary>
    public class TestRunCancellationToken
    {
        /// <summary>
        /// Stores whether the test run is canceled or not.
        /// </summary>
        private bool cancelled;

        /// <summary>
        /// Callback to be invoked when canceled.
        /// </summary>
        private Action registeredCallback;

        /// <summary>
        /// Gets if the test run is canceled.
        /// </summary>
        public bool Canceled
        {
            get
            {
                return this.cancelled;
            }
            private set
            {
                this.cancelled = value;
                if (this.cancelled)
                {
                    this.registeredCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Cancels the execution of a test run.
        /// </summary>
        public void Cancel()
        {
            this.Canceled = true;
        }

        /// <summary>
        /// Registers a callback method to be invoked when canceled.
        /// </summary>
        /// <param name="callback">Callback delegate for handling cancellation.</param>
        public void Register(Action callback)
        {
            ValidateArg.NotNull(callback, "callback");

            Debug.Assert(this.registeredCallback == null, "Callback delegate is already registered, use a new cancellationToken");

            this.registeredCallback = callback;
        }

        /// <summary>
        /// Unregister the callback method.
        /// </summary>
        public void Unregister()
        {
            this.registeredCallback = null;
        }
    }
}