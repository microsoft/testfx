// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using MSTest.PlatformServices.Execution;
using MSTest.PlatformServices.Interface;
using MSTest.PlatformServices.Interface.ObjectModel;

using ISettingsProvider = MSTest.PlatformServices.Interface.ISettingsProvider;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.PlatformServices;

/// <summary>
/// The main service provider class that exposes all the platform services available.
/// </summary>
internal sealed class PlatformServiceProvider : IPlatformServiceProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformServiceProvider"/> class - a singleton.
    /// </summary>
    private PlatformServiceProvider()
    {
    }

    /// <summary>
    /// Gets an instance to the platform service validator for data sources for tests.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public ITestDataSource TestDataSource
    {
        get => field ??= new TestDataSource();
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service for file operations.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public IFileOperations FileOperations
    {
        get => field ??= new FileOperations();
        private set;
    }

    /// <summary>
    /// Gets or sets an instance to the platform service for trace logging.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public IAdapterTraceLogger AdapterTraceLogger { get => field ??= new AdapterTraceLogger(); set; }

    /// <summary>
    /// Gets an instance of the test deployment service.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public ITestDeployment TestDeployment
    {
        get => field ??= new TestDeployment();
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service for a Settings Provider.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public ISettingsProvider SettingsProvider
    {
        get => field ??= new MSTestSettingsProvider();
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service for thread operations.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public IThreadOperations ThreadOperations
    {
        get => field ??= new ThreadOperations();
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service for reflection operations specific to a platform.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public IReflectionOperations2 ReflectionOperations
    {
        get => field ??= new ReflectionOperations2();
        private set;
    }

    /// <summary>
    /// Gets or sets an instance to the platform service for cancellation token supporting cancellation of a test run.
    /// </summary>
    public TestRunCancellationToken? TestRunCancellationToken { get; set; }

    public bool IsGracefulStopRequested { get; set; }

    /// <summary>
    /// Gets or sets the instance for the platform service.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    internal static IPlatformServiceProvider Instance
    {
        get => field ??= new PlatformServiceProvider();
        set;
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
        IRunSettings? runSettings,
        IFrameworkHandle? frameworkHandle)
    {
        var testSourceHost = new TestSourceHost(source, runSettings, frameworkHandle);
        testSourceHost.SetupHost();

        return testSourceHost;
    }

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
    public ITestContext GetTestContext(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IMessageLogger messageLogger, UTF.UnitTestOutcome outcome)
    {
        var testContextImplementation = new TestContextImplementation(testMethod, testClassFullName, properties, messageLogger, TestRunCancellationToken);
        testContextImplementation.SetOutcome(outcome);
        return testContextImplementation;
    }
}
