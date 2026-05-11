// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    // ---- IEnumerable<T> overload ----
    [TestMethod]
    public void ValidateUniqueExtension_Generic_NullExistingExtensions_Throws()
    {
        IEnumerable<IExtension> existing = null!;
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(newExt.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_NullNewExtension_Throws()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_NullSelector_Throws()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension<IExtension>(newExt.Object, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        existing.ValidateUniqueExtension(newExt.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_NoDuplicateUid_DoesNotThrow()
    {
        Mock<IExtension> existing1 = new();
        existing1.Setup(e => e.Uid).Returns("other-uid");

        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        new[] { existing1.Object }.ValidateUniqueExtension(newExt.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_OneDuplicateUid_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing1 = new();
        existing1.Setup(e => e.Uid).Returns("duplicate-uid");

        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("duplicate-uid");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { existing1.Object }.ValidateUniqueExtension(newExt.Object, x => x));

        Assert.Contains("duplicate-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_OneDuplicateUid_ErrorMessageContainsTypeNames()
    {
        IExtension existing1 = new DuplicateExtensionA("duplicate-uid");
        IExtension newExt = new DuplicateExtensionB("duplicate-uid");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { existing1 }.ValidateUniqueExtension(newExt, x => x));

        Assert.Contains(existing1.GetType().ToString(), ex.Message);
        Assert.Contains(newExt.GetType().ToString(), ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_MultipleDuplicates_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing1 = new();
        existing1.Setup(e => e.Uid).Returns("dup-uid");

        Mock<IExtension> existing2 = new();
        existing2.Setup(e => e.Uid).Returns("dup-uid");

        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("dup-uid");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { existing1.Object, existing2.Object }.ValidateUniqueExtension(newExt.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_CustomSelector_DetectsDuplicate()
    {
        var wrapper1 = new { Extension = CreateExtension("uid-x") };
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid-x");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { wrapper1 }.ValidateUniqueExtension(newExt.Object, w => w.Extension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Generic_CustomSelector_NoDuplicate_DoesNotThrow()
    {
        var wrapper1 = new { Extension = CreateExtension("uid-a") };
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid-b");

        new[] { wrapper1 }.ValidateUniqueExtension(newExt.Object, w => w.Extension);
    }

    // ---- IEnumerable<IExtension> simple overload ----
    [TestMethod]
    public void ValidateUniqueExtension_Simple_NullExistingExtensions_Throws()
    {
        IEnumerable<IExtension> existing = null!;
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(newExt.Object));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Simple_NullNewExtension_Throws()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_Simple_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        existing.ValidateUniqueExtension(newExt.Object);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Simple_NoDuplicate_DoesNotThrow()
    {
        Mock<IExtension> existing1 = new();
        existing1.Setup(e => e.Uid).Returns("other-uid");

        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("uid");

        new[] { existing1.Object }.ValidateUniqueExtension(newExt.Object);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Simple_DuplicateUid_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing1 = new();
        existing1.Setup(e => e.Uid).Returns("dup-uid");

        Mock<IExtension> newExt = new();
        newExt.Setup(e => e.Uid).Returns("dup-uid");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { existing1.Object }.ValidateUniqueExtension(newExt.Object));

        Assert.Contains("dup-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_Simple_DuplicateUid_ErrorMessageContainsTypeNames()
    {
        IExtension existing1 = new DuplicateExtensionA("dup-uid");
        IExtension newExt = new DuplicateExtensionB("dup-uid");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new[] { existing1 }.ValidateUniqueExtension(newExt));

        Assert.Contains(existing1.GetType().ToString(), ex.Message);
        Assert.Contains(newExt.GetType().ToString(), ex.Message);
    }

    private static IExtension CreateExtension(string uid)
    {
        Mock<IExtension> ext = new();
        ext.Setup(e => e.Uid).Returns(uid);
        return ext.Object;
    }

    private sealed class DuplicateExtensionA(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => nameof(DuplicateExtensionA);

        public string Description => nameof(DuplicateExtensionA);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class DuplicateExtensionB(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => nameof(DuplicateExtensionB);

        public string Description => nameof(DuplicateExtensionB);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
