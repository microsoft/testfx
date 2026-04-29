// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggerFactoryProxyTests
{
    [TestMethod]
    public void CreateLogger_WhenNotInitialized_ThrowsInvalidOperationException()
    {
        LoggerFactoryProxy proxy = new();

        Assert.ThrowsExactly<InvalidOperationException>(() => proxy.CreateLogger("test"));
    }

    [TestMethod]
    public void SetLoggerFactory_WithNull_ThrowsArgumentNullException()
    {
        LoggerFactoryProxy proxy = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => proxy.SetLoggerFactory(null!));
    }

    [TestMethod]
    public void CreateLogger_WhenInitialized_DelegatesToInnerFactory()
    {
        Mock<ILogger> mockLogger = new();
        Mock<ILoggerFactory> mockFactory = new();
        mockFactory.Setup(f => f.CreateLogger("category")).Returns(mockLogger.Object);

        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(mockFactory.Object);

        ILogger result = proxy.CreateLogger("category");

        Assert.AreSame(mockLogger.Object, result);
        mockFactory.Verify(f => f.CreateLogger("category"), Times.Once);
    }
}
