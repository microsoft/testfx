// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias FirstAlias;
extern alias SecondAlias;

namespace Bazel.ReferenceAliases.UnitTests;

[TestClass]
public sealed class AliasTests
{
    [TestMethod]
    public void GlobalAndNamedAliasesReferToTheSameAssembly()
    {
        Assert.AreEqual("first", global::Bazel.ReferenceAliasFixtures.SharedType.Value);
        Assert.AreSame(
            typeof(global::Bazel.ReferenceAliasFixtures.SharedType).Assembly,
            typeof(FirstAlias::Bazel.ReferenceAliasFixtures.SharedType).Assembly);
    }

    [TestMethod]
    public void NamedOnlyReferenceDoesNotMakeTheGlobalTypeAmbiguous()
    {
        Assert.AreEqual("first", global::Bazel.ReferenceAliasFixtures.SharedType.Value);
        Assert.AreEqual("second", SecondAlias::Bazel.ReferenceAliasFixtures.SharedType.Value);
        Assert.AreNotSame(
            typeof(global::Bazel.ReferenceAliasFixtures.SharedType).Assembly,
            typeof(SecondAlias::Bazel.ReferenceAliasFixtures.SharedType).Assembly);
    }

    [TestMethod]
    public void AliasesPreserveFriendAssemblyAccess()
    {
        Assert.AreEqual("first-internal", FirstAlias::Bazel.ReferenceAliasFixtures.SharedType.InternalValue);
        Assert.AreEqual("second-internal", SecondAlias::Bazel.ReferenceAliasFixtures.SharedType.InternalValue);
    }

    [TestMethod]
    [DataRow(typeof(ITestApplicationBuilder), DisplayName = "PlatformPublicKey")]
    [DataRow(typeof(Assert), DisplayName = "FrameworkPublicKey")]
    [DataRow(typeof(AliasTests), DisplayName = "TestHostPublicKey")]
    [DataRow(typeof(FirstAlias::Bazel.ReferenceAliasFixtures.SharedType), DisplayName = "GlobalAliasPublicKey")]
    [DataRow(typeof(SecondAlias::Bazel.ReferenceAliasFixtures.SharedType), DisplayName = "NamedAliasPublicKey")]
    public void NativeAssembliesHaveThePublishedPublicKey(Type type)
    {
        byte[]? publicKeyToken = type.Assembly.GetName().GetPublicKeyToken();

        Assert.IsNotNull(publicKeyToken);
        Assert.AreEqual("B03F5F7F11D50A3A", Convert.ToHexString(publicKeyToken));
    }

    [TestMethod]
    public void SatellitesPreserveTheParentAssemblyIdentity()
    {
        Assembly parent = typeof(Assert).Assembly;
        AssemblyName parentName = parent.GetName();
        AssemblyName satelliteName = parent.GetSatelliteAssembly(CultureInfo.GetCultureInfo("fr")).GetName();
        byte[]? publicKeyToken = satelliteName.GetPublicKeyToken();

        Assert.AreEqual(parentName.Name + ".resources", satelliteName.Name);
        Assert.AreEqual(parentName.Version, satelliteName.Version);
        Assert.AreEqual("fr", satelliteName.CultureName);
        Assert.IsNotNull(publicKeyToken);
        Assert.AreEqual("B03F5F7F11D50A3A", Convert.ToHexString(publicKeyToken));
    }
}
