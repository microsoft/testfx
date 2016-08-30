// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using MSTestAdapter.PlatformServices.Interface.ObjectModel;
    /// <summary>
    /// The definition of a PlatformServiceProvider with a hook to all the services.
    /// </summary>
    internal interface IPlatformServiceProvider
    {
        /// <summary>
        /// Gets an instance to the platform service validator for test sources.
        /// </summary>
        ITestSource TestSource { get; }

        /// <summary>
        /// Gets an instance to the platform service to data drive a test.
        /// </summary>
        ITestDataSource TestDataSource { get; }

        /// <summary>
        /// Gets an instance to the platform service for file operations.
        /// </summary>
        IFileOperations FileOperations { get; }

        /// <summary>
        /// Gets an instance to the platform service for trace logging.
        /// </summary>
        IAdapterTraceLogger AdapterTraceLogger { get; }

        /// <summary>
        /// Gets an instance to the platform service for a test source host.
        /// </summary>
        ITestSourceHost TestSourceHost { get; }

        /// <summary>
        /// Gets an instance of the test deployment service.
        /// </summary>
        ITestDeployment TestDeployment { get; }

        /// <summary>
        /// Gets an instance to the platform service for a Settings Provider.
        /// </summary>
        ISettingsProvider SettingsProvider { get; }

        /// <summary>
        /// Gets an instance to the platform service listener who monitors trace and debug output
        /// on provided text writer.
        /// </summary>
        ITraceListener GetTraceListener(TextWriter textWriter);

        /// <summary>
        /// Gets an instance to the platform service tracelistener manager which updates the output/error streams 
        /// with redirected streams and performs operations on the listener provided as arguement.
        /// <param name="outputWriter"> The redirected output stream writer. </param>
        /// <param name="errorWriter"> The redirected error stream writer. </param>
        /// </summary>
        ITraceListenerManager GetTraceListenerManager(TextWriter outputWriter, TextWriter errorWriter);

        ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties);

    }
}
