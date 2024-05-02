// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

using Moq;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

public class TestablePlatformServiceProvider : IPlatformServiceProvider
{
    // Using the actual reflection operations implementation since this does not need mocking for existing tests.
    private IReflectionOperations _reflectionOperations;

    public TestablePlatformServiceProvider()
    {
        MockTestSourceValidator = new Mock<ITestSource>();
        MockFileOperations = new Mock<IFileOperations>();
        MockTraceLogger = new Mock<IAdapterTraceLogger>();
        MockTestSourceHost = new Mock<ITestSourceHost>();
        MockTestDeployment = new Mock<ITestDeployment>();
        MockSettingsProvider = new Mock<ISettingsProvider>();
        MockTestDataSource = new Mock<ITestDataSource>();
        MockTraceListener = new Mock<ITraceListener>();
        MockTraceListenerManager = new Mock<ITraceListenerManager>();
        MockThreadOperations = new Mock<IThreadOperations>();
    }

    #region Mock Implementations

    public Mock<ITestSource> MockTestSourceValidator
    {
        get;
        set;
    }

    public Mock<IFileOperations> MockFileOperations
    {
        get;
        set;
    }

    public Mock<IAdapterTraceLogger> MockTraceLogger
    {
        get;
        set;
    }

    public Mock<ITestSourceHost> MockTestSourceHost
    {
        get;
        set;
    }

    public Mock<ITestDeployment> MockTestDeployment
    {
        get;
        set;
    }

    public Mock<ISettingsProvider> MockSettingsProvider
    {
        get;
        set;
    }

    public Mock<ITestDataSource> MockTestDataSource
    {
        get;
        set;
    }

    public Mock<ITraceListener> MockTraceListener
    {
        get;
        set;
    }

    public Mock<ITraceListenerManager> MockTraceListenerManager
    {
        get;
        set;
    }

    public Mock<IThreadOperations> MockThreadOperations
    {
        get;
        set;
    }

    public Mock<IReflectionOperations> MockReflectionOperations
    {
        get;
        set;
    }

    #endregion

    public ITestSource TestSource => MockTestSourceValidator.Object;

    public IFileOperations FileOperations => MockFileOperations.Object;

    public IAdapterTraceLogger AdapterTraceLogger => MockTraceLogger.Object;

    public ITestDeployment TestDeployment => MockTestDeployment.Object;

    public ISettingsProvider SettingsProvider => MockSettingsProvider.Object;

    public IThreadOperations ThreadOperations => MockThreadOperations.Object;

    public IReflectionOperations ReflectionOperations => MockReflectionOperations != null ? MockReflectionOperations.Object : (_reflectionOperations ??= new ReflectionOperations());

    public ITestDataSource TestDataSource => MockTestDataSource.Object;

    public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties) => new TestContextImplementation(testMethod, writer, properties);

    public ITestSourceHost CreateTestSourceHost(string source, TestPlatform.ObjectModel.Adapter.IRunSettings runSettings, TestPlatform.ObjectModel.Adapter.IFrameworkHandle frameworkHandle) => MockTestSourceHost.Object;

    public ITraceListener GetTraceListener(TextWriter textWriter) => MockTraceListener.Object;

    [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration", Justification = "Part of the public API")]
    public ITraceListenerManager GetTraceListenerManager(TextWriter standardOutputWriter, TextWriter standardErrorWriter) => MockTraceListenerManager.Object;

    public void SetupMockReflectionOperations() => MockReflectionOperations = new Mock<IReflectionOperations>();
}
