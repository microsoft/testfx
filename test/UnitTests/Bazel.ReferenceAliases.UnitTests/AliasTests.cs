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
}
