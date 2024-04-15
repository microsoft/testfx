// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

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
    /// Gets an instance of the test deployment service.
    /// </summary>
    ITestDeployment TestDeployment { get; }

    /// <summary>
    /// Gets an instance to the platform service for a Settings Provider.
    /// </summary>
    ISettingsProvider SettingsProvider { get; }

    /// <summary>
    /// Gets an instance to the platform service for thread operations.
    /// </summary>
    IThreadOperations ThreadOperations { get; }

    /// <summary>
    /// Gets an instance to the platform service for reflection operations specific to a platform.
    /// </summary>
    IReflectionOperations ReflectionOperations { get; }

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
    ITestSourceHost CreateTestSourceHost(
        string source,
        TestPlatform.ObjectModel.Adapter.IRunSettings? runSettings,
        TestPlatform.ObjectModel.Adapter.IFrameworkHandle? frameworkHandle);

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
    ITraceListener GetTraceListener(TextWriter textWriter);

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
    ITraceListenerManager GetTraceListenerManager(TextWriter outputWriter, TextWriter errorWriter);

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
    ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object?> properties, IProgressReporter progressReporter);
}
