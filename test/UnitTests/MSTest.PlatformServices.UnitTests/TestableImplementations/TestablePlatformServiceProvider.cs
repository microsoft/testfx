// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using MSTest.PlatformServices.Execution;
using MSTest.PlatformServices.Interface;

using ISettingsProvider = MSTest.PlatformServices.Interface.ISettingsProvider;
using ITestDataSource = MSTest.PlatformServices.Interface.ITestDataSource;

namespace MSTest.PlatformServices.UnitTests;

internal class TestablePlatformServiceProvider : IPlatformServiceProvider
{
    #region Mock Implementations

    public Mock<IFileOperations> MockFileOperations { get; } = new();

    public Mock<IAdapterTraceLogger> MockTraceLogger { get; } = new();

    public Mock<ITestSourceHost> MockTestSourceHost { get; } = new();

    public Mock<ITestDeployment> MockTestDeployment { get; } = new();

    public Mock<ISettingsProvider> MockSettingsProvider { get; } = new();

    public Mock<ITestDataSource> MockTestDataSource { get; } = new();

    public Mock<IThreadOperations> MockThreadOperations { get; } = new();

    public Mock<IReflectionOperations2> MockReflectionOperations { get; set; } = null!;

    #endregion

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

    public ITestContext GetTestContext(Interface.ObjectModel.ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IMessageLogger messageLogger, UnitTestOutcome outcome)
    {
        var testContextImpl = new TestContextImplementation(testMethod, testClassFullName, properties, messageLogger, testRunCancellationToken: null);
        testContextImpl.SetOutcome(outcome);
        return testContextImpl;
    }

    public ITestSourceHost CreateTestSourceHost(string source, IRunSettings? runSettings, IFrameworkHandle? frameworkHandle) => MockTestSourceHost.Object;

    public void SetupMockReflectionOperations() => MockReflectionOperations = new Mock<IReflectionOperations2>();
}
