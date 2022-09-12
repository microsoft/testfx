// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Moq;

using TestFramework.ForTestingMSTest;

public class PlatformServiceProviderTests : TestContainer
{
    [TestCleanup]
    public void Cleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    public void ProviderServiceInstanceShouldReturnAnObjectOfItselfByDefault()
    {
        Assert.IsInstanceOfType(PlatformServiceProvider.Instance, typeof(PlatformServiceProvider));
    }

    public void ProviderServiceInstanceShouldReturnTheInstanceSet()
    {
        // If this test fails most other tests would too since this
        // defines our mocking for the Service provider.
        PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
        Assert.IsInstanceOfType(PlatformServiceProvider.Instance, typeof(TestablePlatformServiceProvider));
    }

    public void TestSourceShouldReturnANonNullInstance()
    {
        Verify(PlatformServiceProvider.Instance is not null);
    }

    public void TestSourceShouldReturnAValidTestSource()
    {
        Assert.IsInstanceOfType(PlatformServiceProvider.Instance.TestSource, typeof(TestSource));
    }

    public void TestSourceShouldBeCached()
    {
        var testSourceInstance = PlatformServiceProvider.Instance.TestSource;

        Verify(testSourceInstance is not null);
        Assert.AreEqual(testSourceInstance, PlatformServiceProvider.Instance.TestSource);
    }

    public void ReflectionOperationsShouldReturnAValidInstance()
    {
        Assert.IsInstanceOfType(PlatformServiceProvider.Instance.ReflectionOperations, typeof(ReflectionOperations));
    }

    public void ReflectionOperationsShouldBeCached()
    {
        var reflectionOperationsInstance = PlatformServiceProvider.Instance.ReflectionOperations;

        Verify(reflectionOperationsInstance is not null);
        Assert.AreEqual(reflectionOperationsInstance, PlatformServiceProvider.Instance.ReflectionOperations);
    }

    public void GetTestContextShouldReturnAValidTestContext()
    {
        // Arrange.
        var testMethod = new Mock<Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod>();
        var writer = new ThreadSafeStringWriter(null, "test");
        var properties = new Dictionary<string, object> { { "prop", "value" } };
        testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
        testMethod.Setup(tm => tm.Name).Returns("M");

        // Act.
        var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod.Object, writer, properties);

        // Assert.
        Assert.AreEqual("A.C.M", testContext.Context.FullyQualifiedTestClassName);
        Assert.AreEqual("M", testContext.Context.TestName);
        Verify(testContext.Context.Properties.Contains(properties.ToArray()[0].Key));
        Verify(((IDictionary<string, object>)testContext.Context.Properties).Contains(properties.ToArray()[0]));
    }
}
