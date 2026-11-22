// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

#if !NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
#endif

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestElement = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

[DoNotParallelize]
public sealed class AssemblyEnumeratorWrapperPass2MutationTests : TestContainer
{
    private static readonly string Source = typeof(Fixture).Assembly.Location;

    private readonly Mock<ITestSourceHandler> _sourceHandler = new();

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = null;
        }
    }

    public void ADataRowIsNotSerializedForMtpWhenTheIsolationHostDoesNotUseAnAppDomain()
    {
        Install(new TestablePlatformServiceProvider());

        UnitTestElement row = DiscoverTheRow(isMTP: true);

        row.TestMethod.ActualData.Should().Equal(17);
        row.TestMethod.SerializedData.Should().BeNull();
    }

    public void ADataRowIsSerializedForVSTest()
    {
        Install(new TestablePlatformServiceProvider());

        UnitTestElement row = DiscoverTheRow(isMTP: false);

        row.TestMethod.ActualData.Should().Equal(17);
        row.TestMethod.SerializedData.Should().NotBeNullOrEmpty();
    }

#if !NETFRAMEWORK
    public void TestsAreDiscoveredWithAClassInstanceFactoryWhenTheRealIsolationHostDoesNotUseAnAppDomain()
    {
        Install(new RealIsolationHostPlatformServiceProvider());

        // Injecting the test class instances is only incompatible with an isolation host that creates an AppDomain.
        using IDisposable factoryScope = TestClassInstanceFactoryProvider.Push(new UnusedTestClassInstanceFactory());
        UnitTestElement row = DiscoverTheRow(isMTP: true);

        row.TestMethod.ActualData.Should().Equal(17);
        row.TestMethod.SerializedData.Should().BeNull();
    }
#endif

    private void Install(TestablePlatformServiceProvider provider)
    {
        PlatformServiceProvider.Instance = provider;
        provider.MockFileOperations.Setup(fileOperations => fileOperations.GetFullFilePath(Source)).Returns(Source);
        provider.MockFileOperations.Setup(fileOperations => fileOperations.DoesFileExist(Source)).Returns(true);
        provider.MockFileOperations.Setup(fileOperations => fileOperations.LoadAssembly(It.IsAny<string>())).Returns(typeof(Fixture).Assembly);
        provider.MockTestSourceHost
            .Setup(host => host.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());
        _sourceHandler.Setup(handler => handler.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
    }

    private UnitTestElement DiscoverTheRow(bool isMTP)
    {
        ICollection<UnitTestElement>? tests = AssemblyEnumeratorWrapper.GetTests(Source, null, _sourceHandler.Object, isMTP, [typeof(Fixture)], null, out _);

        return tests.Should().ContainSingle().Which;
    }

#if !NETFRAMEWORK
    private sealed class RealIsolationHostPlatformServiceProvider : TestablePlatformServiceProvider, IPlatformServiceProvider
    {
        public new ITestSourceHost CreateTestSourceHost(string source, string? settingsXml)
            => new TestSourceHost(source, settingsXml);
    }

    private sealed class UnusedTestClassInstanceFactory : ITestClassInstanceFactory
    {
        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
            => throw new NotSupportedException();
    }
#endif

    [TestClass]
    public sealed class Fixture
    {
        [TestMethod]
        [DataRow(17)]
        public void Row(int value)
        {
        }
    }
}
