// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsTestNodeIdentityTests
{
    [TestMethod]
    public void GetTestName_WhenTestMethodIdentifierPropertyPresent_ReturnsFullyQualifiedName()
    {
        TestNode testNode = CreateNode(
            displayName: "My display name",
            new TestMethodIdentifierProperty("Assembly", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetTestName_WhenTestMethodIdentifierPropertyHasEmptyNamespace_OmitsLeadingDot()
    {
        TestNode testNode = CreateNode(
            displayName: "My display name",
            new TestMethodIdentifierProperty("Assembly", string.Empty, "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("MyType.MyMethod", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetTestName_WhenTestMethodIdentifierPropertyPresent_IsPreferredOverVSTestFullyQualifiedName()
    {
        TestNode testNode = CreateNode(
            displayName: "My display name",
            new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", "Some.Vstest.Fqn"),
            new TestMethodIdentifierProperty("Assembly", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetTestName_WhenOnlyVSTestFullyQualifiedNamePresent_ReturnsIt()
    {
        TestNode testNode = CreateNode(
            displayName: "My display name",
            new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", "My.Namespace.MyType.MyMethod"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetTestName_WhenNoIdentityProperty_FallsBackToDisplayName()
    {
        TestNode testNode = CreateNode(displayName: "My display name");

        Assert.AreEqual("My display name", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetTestName_IgnoresUnrelatedSerializableProperties()
    {
        TestNode testNode = CreateNode(
            displayName: "My display name",
            new SerializableKeyValuePairStringProperty("vstest.TestCase.Id", "some-guid"));

        Assert.AreEqual("My display name", TestNodeIdentity.GetTestName(testNode));
    }

    [TestMethod]
    public void GetDisplayLabel_WhenNonParameterized_ReturnsFullyQualifiedNameUnchanged()
    {
        // Display name is just the method's simple name, already part of the fully-qualified name.
        TestNode testNode = CreateNode(
            displayName: "MyMethod",
            new TestMethodIdentifierProperty("Assembly", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod", TestNodeIdentity.GetDisplayLabel(testNode));
    }

    [TestMethod]
    public void GetDisplayLabel_WhenDataDriven_AppendsOnlyTheDistinguishingSuffix()
    {
        // Default data-driven display name is 'MethodName (args)'; only '(args)' should be appended.
        TestNode testNode = CreateNode(
            displayName: "MyMethod (net8.0)",
            new TestMethodIdentifierProperty("Assembly", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod (net8.0)", TestNodeIdentity.GetDisplayLabel(testNode));
    }

    [TestMethod]
    public void GetDisplayLabel_WhenCustomDisplayName_SurfacesItAlongsideIdentity()
    {
        // A fully custom display name that does not embed the method name is appended in parentheses.
        TestNode testNode = CreateNode(
            displayName: "Custom scenario",
            new TestMethodIdentifierProperty("Assembly", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod (Custom scenario)", TestNodeIdentity.GetDisplayLabel(testNode));
    }

    [TestMethod]
    public void GetDisplayLabel_WhenDisplayNameEqualsIdentity_ReturnsIdentity()
    {
        TestNode testNode = CreateNode(
            displayName: "My.Namespace.MyType.MyMethod",
            new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", "My.Namespace.MyType.MyMethod"));

        Assert.AreEqual("My.Namespace.MyType.MyMethod", TestNodeIdentity.GetDisplayLabel(testNode));
    }

    private static TestNode CreateNode(string displayName, params IProperty[] properties)
    {
        PropertyBag propertyBag = new();
        foreach (IProperty property in properties)
        {
            propertyBag.Add(property);
        }

        return new TestNode
        {
            Uid = "uid",
            DisplayName = displayName,
            Properties = propertyBag,
        };
    }
}
