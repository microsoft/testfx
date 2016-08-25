// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    /// <summary>
    /// The main service provider class that exposes all the platform services available.
    /// </summary>
    internal class PlatformServiceProvider : IPlatformServiceProvider
    {
        private static IPlatformServiceProvider instance;
        private ITestSource testSource;
        private IFileOperations fileOperations;
        private IAdapterTraceLogger traceLogger;
        private ITestDeployment testDeployment;
        private ISettingsProvider settingsProvider;
        private ITestDataSource testDataSource;

        /// <summary>
        /// Singleton class.
        /// </summary>
        private PlatformServiceProvider()
        {
        }

        /// <summary>
        /// Gets an instance to the platform service validator for test sources.
        /// </summary>
        public ITestSource TestSource
        {
            get
            {
                return this.testSource ?? (this.testSource = new TestSource());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service validator for data sources for tests.
        /// </summary>
        public ITestDataSource TestDataSource
        {
            get
            {
                return this.testDataSource ?? (this.testDataSource = new TestDataSource());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service for file operations.
        /// </summary>
        public IFileOperations FileOperations
        {
            get
            {
                return this.fileOperations ?? (this.fileOperations = new FileOperations());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service for trace logging.
        /// </summary>
        public IAdapterTraceLogger AdapterTraceLogger
        {
            get
            {
                return this.traceLogger ?? (this.traceLogger = new AdapterTraceLogger());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service for an isolation host.
        /// </summary>
        /// <remarks> This property is not cached since IsolationHost is a IDisposable object.</remarks>.
        public ITestSourceHost TestSourceHost
        {
            get
            {
                return new TestSourceHost();
            }
        }

        /// <summary>
        /// Gets an instance of the test deployment service.
        /// </summary>
        public ITestDeployment TestDeployment
        {
            get
            {
                return this.testDeployment?? (this.testDeployment = new TestDeployment());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service for a Settings Provider.
        /// </summary>
        public ISettingsProvider SettingsProvider
        {
            get
            {
                return this.settingsProvider ?? (this.settingsProvider = new MSTestSettingsProvider());
            }
        }

        public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties)
        {
            return new TestContextImplementation(testMethod, writer, properties);
        }

        /// <summary>
        /// The instance for the platform service.
        /// </summary>
        internal static IPlatformServiceProvider Instance
        {
            get
            {
                return instance ?? (instance = new PlatformServiceProvider());
            }
            set
            {
                instance = value;
            }
        }
    }
}
