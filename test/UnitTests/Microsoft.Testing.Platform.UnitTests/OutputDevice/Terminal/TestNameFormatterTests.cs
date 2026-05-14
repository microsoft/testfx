// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice.Terminal;

[TestClass]
public sealed class TestNameFormatterTests
{
    [TestMethod]
    public void Format_WithDisplayPlaceholderOnly_ReturnsDisplayName()
    {
        var formatter = new TestNameFormatter("{display}");
        TestNode testNode = CreateTestNode("MyTestMethod");

        Assert.AreEqual("MyTestMethod", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithFullyQualifiedName_ReturnsNamespaceTypeMethod()
    {
        var formatter = new TestNameFormatter("{fqn}");
        TestNode testNode = CreateTestNode("MyTestMethod", CreateMethodIdentifier());

        Assert.AreEqual("MyNamespace.MyTestClass.MyTestMethod", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithFullyQualifiedName_WithParameters_IncludesParameterTypes()
    {
        var formatter = new TestNameFormatter("{fqn}");
        TestNode testNode = CreateTestNode(
            "MyTestMethod(1, \"test\")",
            CreateMethodIdentifier(parameterTypeFullNames: ["System.Int32", "System.String"]));

        Assert.AreEqual("MyNamespace.MyTestClass.MyTestMethod(System.Int32, System.String)", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithNamespacePlaceholder_ReturnsNamespace()
    {
        var formatter = new TestNameFormatter("{ns}");
        TestNode testNode = CreateTestNode("MyTestMethod", CreateMethodIdentifier(@namespace: "MyNamespace.SubNamespace"));

        Assert.AreEqual("MyNamespace.SubNamespace", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithTypePlaceholder_ReturnsTypeName()
    {
        var formatter = new TestNameFormatter("{type}");
        TestNode testNode = CreateTestNode("MyTestMethod", CreateMethodIdentifier());

        Assert.AreEqual("MyTestClass", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithMethodPlaceholder_ReturnsMethodName()
    {
        var formatter = new TestNameFormatter("{method}");
        TestNode testNode = CreateTestNode("MyTestMethod", CreateMethodIdentifier());

        Assert.AreEqual("MyTestMethod", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithAssemblyPlaceholder_ReturnsShortAssemblyName()
    {
        var formatter = new TestNameFormatter("{asm}");
        TestNode testNode = CreateTestNode(
            "MyTestMethod",
            CreateMethodIdentifier(assemblyFullName: "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));

        Assert.AreEqual("MyAssembly", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithMixedPlaceholders_ReplacesAll()
    {
        var formatter = new TestNameFormatter("{display} ({fqn}) [{asm}]");
        TestNode testNode = CreateTestNode("Test Method Display Name", CreateMethodIdentifier());

        Assert.AreEqual("Test Method Display Name (MyNamespace.MyTestClass.MyTestMethod) [MyAssembly]", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithoutTestMethodIdentifierProperty_FallsBackForFqn()
    {
        var formatter = new TestNameFormatter("{fqn}");
        TestNode testNode = CreateTestNode("MyTestMethod");

        Assert.AreEqual("MyTestMethod", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithoutTestMethodIdentifierProperty_UsesEmptyForGranularPlaceholders()
    {
        var formatter = new TestNameFormatter("{ns}.{type}.{method}");
        TestNode testNode = CreateTestNode("MyTestMethod");

        Assert.AreEqual("..", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_WithEmptyNamespace_DoesNotAddExtraDotInFqn()
    {
        var formatter = new TestNameFormatter("{fqn}");
        TestNode testNode = CreateTestNode("MyTestMethod", CreateMethodIdentifier(@namespace: string.Empty));

        Assert.AreEqual("MyTestClass.MyTestMethod", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_UnknownPlaceholder_IsLeftAsIs()
    {
        var formatter = new TestNameFormatter("{display} - {unknown}");
        TestNode testNode = CreateTestNode("MyTestMethod");

        Assert.AreEqual("MyTestMethod - {unknown}", formatter.Format(testNode));
    }

    [TestMethod]
    public void Format_LiteralCurlyBracesAroundUnknownPlaceholder_AreNotTreatedAsToken()
    {
        var formatter = new TestNameFormatter("[{display}]");
        TestNode testNode = CreateTestNode("MyTestMethod");

        Assert.AreEqual("[MyTestMethod]", formatter.Format(testNode));
    }

    private static TestMethodIdentifierProperty CreateMethodIdentifier(
        string assemblyFullName = "MyAssembly, Version=1.0.0.0",
        string @namespace = "MyNamespace",
        string typeName = "MyTestClass",
        string methodName = "MyTestMethod",
        string[]? parameterTypeFullNames = null)
        => new(
            assemblyFullName,
            @namespace,
            typeName,
            methodName,
            methodArity: 0,
            parameterTypeFullNames ?? [],
            returnTypeFullName: "System.Void");

    private static TestNode CreateTestNode(string displayName, TestMethodIdentifierProperty? methodIdentifier = null)
        => new()
        {
            Uid = new TestNodeUid("test-uid"),
            DisplayName = displayName,
            Properties = methodIdentifier is null ? new PropertyBag() : new PropertyBag(methodIdentifier),
        };
}
