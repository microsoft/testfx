// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice.Terminal;

[TestClass]
public sealed class TestNameFormatterTests
{
    [TestMethod]
    public void Format_WithDisplayNameOnly_ReturnsDisplayName()
    {
        // Arrange
        var formatter = new TestNameFormatter("<display>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod"
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyTestMethod", result);
    }

    [TestMethod]
    public void Format_WithFullyQualifiedName_ReturnsCorrectFormat()
    {
        // Arrange
        var formatter = new TestNameFormatter("<fqn>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyNamespace.MyTestClass.MyTestMethod", result);
    }

    [TestMethod]
    public void Format_WithFullyQualifiedName_WithParameters_IncludesParameters()
    {
        // Arrange
        var formatter = new TestNameFormatter("<fqn>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod(1, \"test\")",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly, Version=1.0.0.0",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: ["System.Int32", "System.String"],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyNamespace.MyTestClass.MyTestMethod(System.Int32, System.String)", result);
    }

    [TestMethod]
    public void Format_WithNamespace_ReturnsNamespace()
    {
        // Arrange
        var formatter = new TestNameFormatter("<ns>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly",
                    @namespace: "MyNamespace.SubNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyNamespace.SubNamespace", result);
    }

    [TestMethod]
    public void Format_WithType_ReturnsTypeName()
    {
        // Arrange
        var formatter = new TestNameFormatter("<type>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyTestClass", result);
    }

    [TestMethod]
    public void Format_WithMethod_ReturnsMethodName()
    {
        // Arrange
        var formatter = new TestNameFormatter("<method>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyTestMethod", result);
    }

    [TestMethod]
    public void Format_WithAssembly_ReturnsShortAssemblyName()
    {
        // Arrange
        var formatter = new TestNameFormatter("<asm>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyAssembly", result);
    }

    [TestMethod]
    public void Format_WithMixedPlaceholders_ReplacesAll()
    {
        // Arrange
        var formatter = new TestNameFormatter("<display> (<fqn>) [<asm>]");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "Test Method Display Name",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly, Version=1.0.0.0",
                    @namespace: "MyNamespace",
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("Test Method Display Name (MyNamespace.MyTestClass.MyTestMethod) [MyAssembly]", result);
    }

    [TestMethod]
    public void Format_WithoutTestMethodIdentifierProperty_UsesDisplayNameForFqn()
    {
        // Arrange
        var formatter = new TestNameFormatter("<fqn>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod"
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyTestMethod", result);
    }

    [TestMethod]
    public void Format_WithoutTestMethodIdentifierProperty_UsesEmptyForOtherPlaceholders()
    {
        // Arrange
        var formatter = new TestNameFormatter("<ns>.<type>.<method>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod"
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("..", result);
    }

    [TestMethod]
    public void Format_WithEmptyNamespace_DoesNotAddExtraDot()
    {
        // Arrange
        var formatter = new TestNameFormatter("<fqn>");
        var testNode = new TestNode
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(
                new TestMethodIdentifierProperty(
                    assemblyFullName: "MyAssembly",
                    @namespace: string.Empty,
                    typeName: "MyTestClass",
                    methodName: "MyTestMethod",
                    methodArity: 0,
                    parameterTypeFullNames: [],
                    returnTypeFullName: "System.Void"))
        };

        // Act
        string result = formatter.Format(testNode);

        // Assert
        Assert.AreEqual("MyTestClass.MyTestMethod", result);
    }
}
