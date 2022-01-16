// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private IThreadOperations threadOperations;
        private IReflectionOperations reflectionOperations;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformServiceProvider"/> class - a singleton.
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
        /// Gets an instance of the test deployment service.
        /// </summary>
        public ITestDeployment TestDeployment
        {
            get
            {
                return this.testDeployment ?? (this.testDeployment = new TestDeployment());
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

        /// <summary>
        /// Gets an instance to the platform service for thread operations.
        /// </summary>
        public IThreadOperations ThreadOperations
        {
            get
            {
                return this.threadOperations ?? (this.threadOperations = new ThreadOperations());
            }
        }

        /// <summary>
        /// Gets an instance to the platform service for reflection operations specific to a platform.
        /// </summary>
        public IReflectionOperations ReflectionOperations
        {
            get
            {
                return this.reflectionOperations ?? (this.reflectionOperations = new ReflectionOperations());
            }
        }

        /// <summary>
        /// Gets or sets the instance for the platform service.
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

        /// <summary>
        /// Creates an instance to the platform service for a test source host.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="runSettings">
        /// The run Settings for the session.
        /// </param>
        /// <param name="frameworkHandle">
        /// The handle to the test platform.
        /// </param>
        /// <returns>
        /// Returns the host for the source provided.
        /// </returns>
        public ITestSourceHost CreateTestSourceHost(
            string source,
            TestPlatform.ObjectModel.Adapter.IRunSettings runSettings,
            TestPlatform.ObjectModel.Adapter.IFrameworkHandle frameworkHandle)
        {
            var testSourceHost = new TestSourceHost(source, runSettings, frameworkHandle);
            testSourceHost.SetupHost();

            return testSourceHost;
        }

        /// <summary>
        /// Gets an instance to the platform service listener who monitors trace and debug output
        /// on provided text writer.
        /// </summary>
        /// <param name="textWriter">
        /// The text Writer.
        /// </param>
        /// <returns>
        /// The <see cref="ITraceListener"/>.
        /// </returns>
        public ITraceListener GetTraceListener(TextWriter textWriter)
        {
           return new TraceListenerWrapper(textWriter);
        }

        /// <summary>
        /// Gets an instance to the platform service trace-listener manager which updates the output/error streams
        /// with redirected streams and performs operations on the listener provided as argument.
        /// </summary>
        /// <param name="outputWriter">
        /// The redirected output stream writer.
        /// </param>
        /// <param name="errorWriter">
        /// The redirected error stream writer.
        /// </param>
        /// <returns>
        /// The manager for trace listeners.
        /// </returns>
        public ITraceListenerManager GetTraceListenerManager(TextWriter outputWriter, TextWriter errorWriter)
        {
            return new TraceListenerManager(outputWriter, errorWriter);
        }

        /// <summary>
        /// Gets the TestContext object for a platform.
        /// </summary>
        /// <param name="testMethod">
        /// The test method.
        /// </param>
        /// <param name="writer">
        /// The writer instance for logging.
        /// </param>
        /// <param name="properties">
        /// The default set of properties the test context needs to be filled with.
        /// </param>
        /// <returns>
        /// The <see cref="ITestContext"/> instance.
        /// </returns>
        /// <remarks>
        /// This was required for compatibility reasons since the TestContext object that the V1 adapter had for desktop is not .Net Core compliant.
        /// </remarks>
        public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties)
        {
            return new TestContextImplementation(testMethod, writer, properties);
        }
    }
}
