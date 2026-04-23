// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests;

[TestClass]
public sealed class SynchronizedSingleSessionVSTestBridgedTestFrameworkTests
{
#if NETCOREAPP
    [TestMethod]
    public void GetAssemblyPath_WhenLocationIsNonEmpty_ReturnsLocation()
    {
        // Arrange - Assembly.Location is virtual on .NET Core, so Moq can mock it
        var assembly = new Mock<Assembly>();
        assembly.Setup(a => a.Location).Returns(@"C:\path\to\MyTests.dll");

        // Act
        string result = SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assembly.Object);

        // Assert
        Assert.AreEqual(@"C:\path\to\MyTests.dll", result);
    }

    [TestMethod]
    public void GetAssemblyPath_WhenLocationIsEmpty_ReturnsSyntheticPathFromAssemblyName()
    {
        // Arrange - simulate Android CoreCLR where Assembly.Location returns ""
        var assembly = new Mock<Assembly>();
        assembly.Setup(a => a.Location).Returns(string.Empty);
        assembly.Setup(a => a.GetName()).Returns(new AssemblyName("MyTests"));

        // Act
        string result = SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assembly.Object);

        // Assert
        Assert.AreEqual("MyTests.dll", result);
    }

    [TestMethod]
    public void GetAssemblyPath_WhenLocationIsNull_ReturnsSyntheticPathFromAssemblyName()
    {
        // Arrange
        var assembly = new Mock<Assembly>();
        assembly.Setup(a => a.Location).Returns((string)null!);
        assembly.Setup(a => a.GetName()).Returns(new AssemblyName("MyTests"));

        // Act
        string result = SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assembly.Object);

        // Assert
        Assert.AreEqual("MyTests.dll", result);
    }

    [TestMethod]
    public void GetAssemblyPath_WhenLocationIsEmpty_AndAssemblyNameIsNull_Throws()
    {
        // Arrange
        var assembly = new Mock<Assembly>();
        assembly.Setup(a => a.Location).Returns(string.Empty);
        assembly.Setup(a => a.GetName()).Returns(new AssemblyName());

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(
            () => SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assembly.Object));
    }

    [TestMethod]
    public void GetAssemblyPath_WithDynamicInMemoryAssembly_ReturnsSyntheticPath()
    {
        // Arrange - create a real in-memory assembly that has empty Location
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("InMemoryTestAssembly"),
            AssemblyBuilderAccess.Run);

        // Verify our assumption: dynamic assemblies have empty Location
        Assert.AreEqual(string.Empty, assemblyBuilder.Location);

        // Act
        string result = SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assemblyBuilder);

        // Assert
        Assert.AreEqual("InMemoryTestAssembly.dll", result);
    }
#endif

    [TestMethod]
    public void GetAssemblyPath_WithRealAssembly_ReturnsActualLocation()
    {
        // Arrange - use the currently executing assembly which has a real file-backed location
        Assembly assembly = typeof(SynchronizedSingleSessionVSTestBridgedTestFrameworkTests).Assembly;

        // Act
        string result = SynchronizedSingleSessionVSTestBridgedTestFramework.GetAssemblyPath(assembly);

        // Assert - should return the real path ending with .dll or .exe
        Assert.AreEqual(assembly.Location, result);
        Assert.IsTrue(
            result.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || result.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }
}
