// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    [TestMethod]
    public void ValidateUniqueExtension_WithNullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existing = null!;
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => existing.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithNullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() => existing.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithEmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        TestExtension newExtension = new("uid1");

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithNonDuplicateUid_DoesNotThrow()
    {
        List<IExtension> existing = [new TestExtension("uid1")];
        TestExtension newExtension = new("uid2");

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithDuplicateUid_ThrowsInvalidOperationException()
    {
        List<IExtension> existing = [new TestExtension("uid1")];
        TestExtension newExtension = new("uid1");

        Assert.ThrowsExactly<InvalidOperationException>(() => existing.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithDuplicateUid_ErrorMessageContainsDuplicateUid()
    {
        List<IExtension> existing = [new TestExtension("uid1")];
        TestExtension newExtension = new("uid1");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => existing.ValidateUniqueExtension(newExtension));

        Assert.Contains("uid1", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithDuplicateUid_ErrorMessageContainsBothTypeNames()
    {
        List<IExtension> existing = [new TestExtension("uid1")];
        TestExtension newExtension = new("uid1");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => existing.ValidateUniqueExtension(newExtension));

        Assert.Contains(typeof(TestExtension).ToString(), ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithMultipleDuplicates_ThrowsInvalidOperationException()
    {
        List<IExtension> existing = [new TestExtension("uid1"), new TestExtension("uid1")];
        TestExtension newExtension = new("uid1");

        Assert.ThrowsExactly<InvalidOperationException>(() => existing.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithNullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existing = null!;
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => existing.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithNullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() => existing.ValidateUniqueExtension(null!, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithNullSelector_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => existing.ValidateUniqueExtension<IExtension>(newExtension, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithNonDuplicateUid_DoesNotThrow()
    {
        List<(string Tag, IExtension Ext)> existing = [("a", new TestExtension("uid1"))];
        TestExtension newExtension = new("uid2");

        existing.ValidateUniqueExtension(newExtension, item => item.Ext);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithDuplicateUid_ThrowsInvalidOperationException()
    {
        List<(string Tag, IExtension Ext)> existing = [("a", new TestExtension("uid1"))];
        TestExtension newExtension = new("uid1");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => existing.ValidateUniqueExtension(newExtension, item => item.Ext));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithDuplicateUid_ErrorMessageContainsDuplicateUid()
    {
        List<(string Tag, IExtension Ext)> existing = [("a", new TestExtension("uid1"))];
        TestExtension newExtension = new("uid1");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => existing.ValidateUniqueExtension(newExtension, item => item.Ext));

        Assert.Contains("uid1", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Selector_WithEmptyCollection_DoesNotThrow()
    {
        List<(string Tag, IExtension Ext)> existing = [];
        TestExtension newExtension = new("uid1");

        existing.ValidateUniqueExtension(newExtension, item => item.Ext);
    }
}
