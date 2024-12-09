// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SourceGeneration;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// The main service provider class that exposes all the platform services available.
/// </summary>
internal sealed class PlatformServiceProvider : IPlatformServiceProvider
{
    private static readonly Action<object?> CancelDelegate = static state => ((TestContextImplementation)state!).Context.CancellationTokenSource.Cancel();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformServiceProvider"/> class - a singleton.
    /// </summary>
    private PlatformServiceProvider() =>
#if !WINDOWS_UWP
        // Set the provider that is used by DynamicDataAttribute when generating data, to allow substituting functionality
        // in TestFramework without having to put all the stuff in that library.
        TestTools.UnitTesting.DynamicDataProvider.Instance = SourceGeneratorToggle.UseSourceGenerator
            ? new SourceGeneratedDynamicDataOperations()
            : new DynamicDataOperations();
#else
        TestTools.UnitTesting.DynamicDataProvider.Instance = new DynamicDataOperations();
#endif

    /// <summary>
    /// Gets an instance to the platform service validator for test sources.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public ITestSource TestSource
    {
        get => field ??= new TestSource();
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service validator for data sources for tests.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
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
    public IFileOperations FileOperations
    {
        get => field ??=
#if !WINDOWS_UWP
            SourceGeneratorToggle.UseSourceGenerator
                ? new SourceGeneratedFileOperations()
                : new FileOperations();
#else
            new FileOperations();
#endif
        private set;
    }

    /// <summary>
    /// Gets an instance to the platform service for trace logging.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public IAdapterTraceLogger AdapterTraceLogger
    {
        get => field ??= new AdapterTraceLogger();
        private set;
    }

    /// <summary>
    /// Gets an instance of the test deployment service.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
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
    public IReflectionOperations2 ReflectionOperations
    {
        get => field ??=
#if !WINDOWS_UWP
             SourceGeneratorToggle.UseSourceGenerator
                 ? new SourceGeneratedReflectionOperations()
                 : new ReflectionOperations2();
#else
            new ReflectionOperations2();
#endif
        private set;
    }

    /// <summary>
    /// Gets or sets an instance to the platform service for cancellation token supporting cancellation of a test run.
    /// </summary>
    public TestRunCancellationToken? TestRunCancellationToken { get; set; }

    /// <summary>
    /// Gets or sets the instance for the platform service.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
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
        TestPlatform.ObjectModel.Adapter.IRunSettings? runSettings,
        TestPlatform.ObjectModel.Adapter.IFrameworkHandle? frameworkHandle)
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
    public ITraceListener GetTraceListener(TextWriter textWriter) => new TraceListenerWrapper(textWriter);

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
    public ITraceListenerManager GetTraceListenerManager(TextWriter outputWriter, TextWriter errorWriter) => new TraceListenerManager(outputWriter, errorWriter);

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
    public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object?> properties)
    {
        var testContextImplementation = new TestContextImplementation(testMethod, writer, properties);
        TestRunCancellationToken?.Register(CancelDelegate, testContextImplementation);
        return testContextImplementation;
    }
}
