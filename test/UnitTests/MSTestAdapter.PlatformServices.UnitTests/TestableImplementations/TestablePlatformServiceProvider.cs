// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using ITestDataSource = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestDataSource;
using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

internal class TestablePlatformServiceProvider : IPlatformServiceProvider
{
    #region Mock Implementations

    public Mock<ITestSource> MockTestSourceValidator { get; } = new();

    public Mock<IFileOperations> MockFileOperations { get; } = new();

    public Mock<IAdapterTraceLogger> MockTraceLogger { get; } = new();

    public Mock<ITestSourceHost> MockTestSourceHost { get; } = new();

    public Mock<ITestDeployment> MockTestDeployment { get; } = new();

    public Mock<ISettingsProvider> MockSettingsProvider { get; } = new();

    public Mock<ITestDataSource> MockTestDataSource { get; } = new();

    public Mock<ITraceListener> MockTraceListener { get; } = new();

    public Mock<ITraceListenerManager> MockTraceListenerManager { get; } = new();

    public Mock<IThreadOperations> MockThreadOperations { get; } = new();

    public Mock<IReflectionOperations2> MockReflectionOperations { get; set; } = null!;

    #endregion

    public ITestSource TestSource => MockTestSourceValidator.Object;

    public IFileOperations FileOperations => MockFileOperations.Object;

    public IAdapterTraceLogger AdapterTraceLogger { get => MockTraceLogger.Object; set => throw new NotSupportedException(); }

    public ITestDeployment TestDeployment => MockTestDeployment.Object;

    public ISettingsProvider SettingsProvider => MockSettingsProvider.Object;

    public IThreadOperations ThreadOperations => MockThreadOperations.Object;

    [field: AllowNull]
    [field: MaybeNull]
    public IReflectionOperations2 ReflectionOperations
    {
        get => MockReflectionOperations != null
            ? MockReflectionOperations.Object
            : field ??= new ReflectionOperations2();
        private set;
    }

    public ITestDataSource TestDataSource => MockTestDataSource.Object;

    public TestRunCancellationToken? TestRunCancellationToken { get; set; }

    public bool IsGracefulStopRequested { get; set; }

    public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object?> properties, IMessageLogger messageLogger, UnitTestOutcome outcome)
    {
        var testContextImpl = new TestContextImplementation(testMethod, writer, properties, messageLogger, testRunCancellationToken: null);
        testContextImpl.SetOutcome(outcome);
        return testContextImpl;
    }

    public ITestSourceHost CreateTestSourceHost(string source, TestPlatform.ObjectModel.Adapter.IRunSettings? runSettings, TestPlatform.ObjectModel.Adapter.IFrameworkHandle? frameworkHandle) => MockTestSourceHost.Object;

    public ITraceListener GetTraceListener(TextWriter textWriter) => MockTraceListener.Object;

    [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration", Justification = "Part of the public API")]
    public ITraceListenerManager GetTraceListenerManager(TextWriter standardOutputWriter, TextWriter standardErrorWriter) => MockTraceListenerManager.Object;

    public void SetupMockReflectionOperations() => MockReflectionOperations = new Mock<IReflectionOperations2>();
}
