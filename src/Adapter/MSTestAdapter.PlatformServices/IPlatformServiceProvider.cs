// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// The definition of a PlatformServiceProvider with a hook to all the services.
/// </summary>
internal interface IPlatformServiceProvider
{
    /// <summary>
    /// Gets an instance to the platform service to data drive a test.
    /// </summary>
    ITestDataSource TestDataSource { get; }

    /// <summary>
    /// Gets an instance to the platform service for file operations.
    /// </summary>
    IFileOperations FileOperations { get; }

    /// <summary>
    /// Gets or sets an instance to the platform service for trace logging.
    /// </summary>
    IAdapterTraceLogger AdapterTraceLogger { get; set; }

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Gets an instance of the test deployment service.
    /// </summary>
    ITestDeployment TestDeployment { get; }
#endif

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
    /// Gets or sets an instance to the platform service for cancellation token supporting cancellation of a test run.
    /// </summary>
    TestRunCancellationToken? TestRunCancellationToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a graceful stop is requested.
    /// </summary>
    bool IsGracefulStopRequested { get; set; }

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
    /// Gets the TestContext object for a platform.
    /// </summary>
    /// <param name="testMethod">
    /// The test method.
    /// </param>
    /// <param name="testClassFullName">
    /// The test class full name.
    /// </param>
    /// <param name="properties">
    /// The default set of properties the test context needs to be filled with.
    /// </param>
    /// <param name="messageLogger">The message logger.</param>
    /// <param name="outcome">The test outcome.</param>
    /// <returns>
    /// The <see cref="ITestContext"/> instance.
    /// </returns>
    /// <remarks>
    /// This was required for compatibility reasons since the TestContext object that the V1 adapter had for desktop is not .Net Core compliant.
    /// </remarks>
    ITestContext GetTestContext(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IMessageLogger messageLogger, UTF.UnitTestOutcome outcome);
}
