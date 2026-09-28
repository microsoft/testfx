#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PathContainmentTests
{
    [TestMethod]
    public void IsUnderDirectory_DistinguishesChildrenFromRootAndSiblingPaths()
    {
        string root = Path.GetFullPath("root");
        string child = Path.Combine(root, "child", "artifact.txt");
        string sibling = Path.GetFullPath("root-sibling");

        Assert.IsTrue(PathContainment.IsUnderDirectory(child, root));
        Assert.IsFalse(PathContainment.IsUnderDirectory(root, root));
        Assert.IsFalse(PathContainment.IsUnderDirectory(sibling, root));
    }

    [TestMethod]
    public void IsUnderDirectory_RootWithTrailingSeparator_IsUnderDirectory()
    {
        string root = Path.GetFullPath("root");

        Assert.IsTrue(PathContainment.IsUnderDirectory(root + Path.DirectorySeparatorChar, root));
    }
}
